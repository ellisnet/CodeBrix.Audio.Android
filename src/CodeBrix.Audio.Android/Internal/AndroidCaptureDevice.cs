using System;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Enums;
using CodeBrix.Audio.Engine.Structs;

namespace CodeBrix.Audio.Android.Internal;

internal sealed class AndroidCaptureDevice : AudioCaptureDevice, IAndroidAudioDevice, IServiceDevice
{
    private readonly OboeStream _stream;
    private readonly object _gate = new();
    internal AndroidCaptureDevice(AndroidAudioEngine engine, DeviceInfo? info, AudioFormat format, AndroidDeviceConfig config)
        : base(engine, format, config)
    {
        Info = info; Capability = Capability.Record;
        _stream = new(true, (int)(info?.Id ?? 0), format, config, Process);
    }
    private void Process(Span<float> samples, Capability capability) => InvokeOnAudioProcessed(samples);
    public override void Start()
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (IsRunning) return;
            ((AndroidAudioEngine)Engine).EnsureRecordPermission();
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
                throw new InvalidOperationException("The capture callback failed; recording has stopped.", diagnostics.CallbackException);
            }
            try { ((AndroidAudioEngine)Engine).EnsureRecordPermission(); }
            catch { _stream.Stop(); IsRunning = false; throw; }
            if (diagnostics.NativeError != 0 && !((AndroidDeviceConfig)Config).RecoverDisconnectedStreams)
            {
                _stream.Stop(); IsRunning = false;
                throw new InvalidOperationException($"Capture disconnected with native error {diagnostics.NativeError}; automatic recovery is disabled.");
            }
            _stream.TryRecover();
        }
    }
}
