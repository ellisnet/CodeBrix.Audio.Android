using System;
using System.IO;
using System.Threading;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Enums;
using global::Android.Media;
using global::Android.Media.Projection;
using AudioFormat = CodeBrix.Audio.Engine.Structs.AudioFormat;

namespace CodeBrix.Audio.Android.Internal;

internal sealed class PlaybackCaptureDevice : AudioCaptureDevice, IServiceDevice
{
    private readonly AudioRecord _record;
    private readonly float[] _buffer;
    private readonly object _gate = new();
    private Thread _thread;
    private int _run;
    private Exception _failure;

    internal PlaybackCaptureDevice(AndroidAudioEngine engine, MediaProjection projection, AudioFormat format, AndroidDeviceConfig config)
        : base(engine, format, config)
    {
        if (format.Channels is not (1 or 2)) throw new NotSupportedException("Android playback capture supports mono or stereo through this adapter.");
        Capability = Capability.Loopback;
        var mask = format.Channels == 1 ? ChannelIn.Mono : ChannelIn.Stereo;
        using var captureBuilder = new AudioPlaybackCaptureConfiguration.Builder(projection);
        using var capture = captureBuilder.AddMatchingUsage(AudioUsageKind.Media)
            .AddMatchingUsage(AudioUsageKind.Game).AddMatchingUsage(AudioUsageKind.Unknown).Build();
        using var formatBuilder = new global::Android.Media.AudioFormat.Builder();
        using var audioFormat = formatBuilder.SetEncoding(Encoding.PcmFloat)
            .SetSampleRate(format.SampleRate).SetChannelMask((ChannelOut)(int)mask).Build();
        int minimum = AudioRecord.GetMinBufferSize(format.SampleRate, mask, Encoding.PcmFloat);
        if (minimum <= 0) throw new NotSupportedException("Android cannot capture the requested format.");
        int frames = Math.Max(format.SampleRate / 100, (minimum + 4 * format.Channels - 1) / (4 * format.Channels));
        _buffer = new float[frames * format.Channels];
        using var builder = new AudioRecord.Builder();
        _record = builder.SetAudioPlaybackCaptureConfig(capture).SetAudioFormat(audioFormat)
            .SetBufferSizeInBytes(_buffer.Length * sizeof(float) * 2).Build();
        if (_record.State != State.Initialized)
        {
            _record.Dispose();
            throw new InvalidOperationException("Android playback capture could not be initialized.");
        }
    }
    public override void Start()
    {
        CheckControlThread();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (IsRunning) return;
            ((AndroidAudioEngine)Engine).EnsureRecordPermission();
            _failure = null;
            _record.StartRecording();
            Volatile.Write(ref _run, 1);
            _thread = new Thread(Read) { IsBackground = true, Name = "CodeBrix playback capture" };
            IsRunning = true;
            _thread.Start();
        }
        Engine.RaiseDeviceStarted(this);
    }
    private void Read()
    {
        try
        {
            global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.Audio);
            while (Volatile.Read(ref _run) != 0)
            {
                int samples = _record.Read(_buffer, 0, _buffer.Length, 0); // READ_BLOCKING
                if (Volatile.Read(ref _run) == 0) break;
                if (samples < 0) throw new IOException($"Android playback capture read failed: {samples}. The projection may have been revoked.");
                if (samples > 0)
                {
                    // Apply the same control-thread guard as Oboe callbacks, including
                    // engine disposal initiated from a playback-capture subscriber.
                    OboeStream.EnterCaptureCallback();
                    try { InvokeOnAudioProcessed(_buffer.AsSpan(0, samples)); }
                    finally { OboeStream.ExitCaptureCallback(); }
                }
            }
        }
        catch (Exception ex) { Volatile.Write(ref _failure, ex); }
        finally { Volatile.Write(ref _run, 0); }
    }
    public override void Stop()
    {
        CheckControlThread();
        lock (_gate)
        {
            if (!IsRunning || IsDisposed) return;
            Volatile.Write(ref _run, 0);
            _record.Stop();
            _thread?.Join(); _thread = null; IsRunning = false;
        }
        Engine.RaiseDeviceStopped(this);
    }
    public override void Dispose()
    {
        CheckControlThread();
        Stop();
        lock (_gate) { if (IsDisposed) return; IsDisposed = true; _record.Release(); _record.Dispose(); }
        OnDisposedHandler();
    }
    private void CheckControlThread()
    {
        OboeStream.AssertControlThread();
        if (Thread.CurrentThread == _thread) throw new InvalidOperationException("Control playback capture from outside its audio callback.");
    }
    public void Service()
    {
        var error = Interlocked.Exchange(ref _failure, null);
        if (error != null) { Stop(); throw new InvalidOperationException("Playback capture stopped.", error); }
    }
}
