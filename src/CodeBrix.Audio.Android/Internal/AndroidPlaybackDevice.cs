using System;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Enums;
using CodeBrix.Audio.Engine.Structs;

namespace CodeBrix.Audio.Android.Internal;

internal interface IServiceDevice { void Service(); }

internal sealed class AndroidPlaybackDevice : AudioPlaybackDevice, IAndroidAudioDevice, IServiceDevice
{
    private readonly OboeStream _stream;
    private readonly object _gate = new();
    internal AndroidPlaybackDevice(AndroidAudioEngine engine, DeviceInfo? info, AudioFormat format, AndroidDeviceConfig config)
        : base(engine, format, config)
    {
        Info = info; Capability = Capability.Playback;
        _stream = new(false, (int)(info?.Id ?? 0), format, config, Process);
    }
    private void Process(Span<float> samples, Capability capability)
    {
        NotifyFramesRendered(samples.Length / Format.Channels);
        var solo = Engine.GetSoloedComponent();
        if (solo != null) solo.Process(samples, Format.Channels);
        else MasterMixer.Process(samples, Format.Channels);
    }
    public override void Start()
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (IsRunning) return;
            _stream.Start(); IsRunning = true;
        }
        Engine.RaiseDeviceStarted(this);
    }
    public override void Stop()
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            if (IsDisposed || !IsRunning) return;
            _stream.Stop(); IsRunning = false;
        }
        Engine.RaiseDeviceStopped(this);
    }
    public override void Dispose()
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            if (IsDisposed) return;
            _stream.Dispose(); IsRunning = false; IsDisposed = true;
        }
        OnDisposedHandler();
    }
    public AndroidAudioDiagnostics GetDiagnostics() => _stream.Diagnostics();
    public void Service()
    {
        lock (_gate)
        {
            if (IsDisposed || !IsRunning) return;
            var diagnostics = _stream.Diagnostics();
            if (diagnostics.CallbackException != null)
            {
                _stream.Stop(); IsRunning = false;
                throw new InvalidOperationException("The playback callback failed; playback has stopped.", diagnostics.CallbackException);
            }
            if (diagnostics.NativeError != 0 && !((AndroidDeviceConfig)Config).RecoverDisconnectedStreams)
            {
                _stream.Stop(); IsRunning = false;
                throw new InvalidOperationException($"Playback disconnected with native error {diagnostics.NativeError}; automatic recovery is disabled.");
            }
            _stream.TryRecover();
        }
    }
}
