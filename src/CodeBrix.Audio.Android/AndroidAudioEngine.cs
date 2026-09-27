using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CodeBrix.Audio.Android.Internal;
using CodeBrix.Audio.Codecs;
using CodeBrix.Audio.Engine.Abstracts;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Backends.MiniAudio;
using CodeBrix.Audio.Engine.Structs;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.Media;
using global::Android.Media.Projection;
using global::Android.OS;
using DeviceInfo = CodeBrix.Audio.Engine.Structs.DeviceInfo;
using AudioFormat = CodeBrix.Audio.Engine.Structs.AudioFormat;

namespace CodeBrix.Audio.Android;

/// <summary>Oboe devices with shared CodeBrix mixing, synthesis and native compressed-audio codecs.</summary>
/// <remarks>Dispose the engine to close all its devices. Application code controls audio focus,
/// permission prompts and foreground services. Audio callbacks must not perform blocking work.</remarks>
public sealed class AndroidAudioEngine : AudioEngine
{
    private readonly Context _context;
    private readonly AudioManager _manager;
    private readonly object _gate = new();
    private readonly HashSet<AudioDevice> _devices = [];
    private readonly DeviceWatcher _watcher;
    private readonly Handler _handler;
    private readonly Timer _timer;
    private bool _closing;
    private int _servicing;

    /// <summary>Creates an engine without opening a playback or recording stream.</summary>
    /// <param name="context">Android context; only the application context is retained.</param>
    public AndroidAudioEngine(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context.ApplicationContext ?? throw new ArgumentException("An application context is required.", nameof(context));
        _manager = (AudioManager)_context.GetSystemService(Context.AudioService)
            ?? throw new PlatformNotSupportedException("Android AudioManager is unavailable.");
        RegisterCodecFactory(new MiniAudioCodecFactory());
        ManagedCodecs.RegisterAll(this);
        if (_context.PackageManager?.HasSystemFeature(PackageManager.FeatureMidi) == true)
        {
            UseMidiBackend(new AndroidMidiBackend(_context));
            UpdateMidiDevicesInfo();
        }
        _handler = new Handler(Looper.MainLooper);
        _watcher = new DeviceWatcher(this);
        UpdateAudioDevicesInfo();
        _manager.RegisterAudioDeviceCallback(_watcher, _handler);
        _timer = new Timer(static state =>
        {
            if (((WeakReference<AndroidAudioEngine>)state).TryGetTarget(out var engine)) engine.ServiceDevices();
        }, new WeakReference<AndroidAudioEngine>(this), 250, 250);
    }

    /// <summary>Raised on Android's main thread after a device is added or removed.</summary>
    public event EventHandler AudioDevicesChanged;
    /// <summary>The most recent control-thread stream failure; callback failures are also in device diagnostics.</summary>
    public Exception LastBackendError { get; private set; }

    /// <inheritdoc />
    public override AudioPlaybackDevice InitializePlaybackDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig config = null)
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            Validate(format);
            return Track(new AndroidPlaybackDevice(this, deviceInfo, format, Options(config)));
        }
    }
    /// <inheritdoc />
    public override AudioCaptureDevice InitializeCaptureDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig config = null)
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            Validate(format); EnsureRecordPermission();
            return Track(new AndroidCaptureDevice(this, deviceInfo, format, Options(config)));
        }
    }
    /// <inheritdoc />
    public override FullDuplexDevice InitializeFullDuplexDevice(DeviceInfo? playbackDeviceInfo,
        DeviceInfo? captureDeviceInfo, AudioFormat format, DeviceConfig config = null)
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            Validate(format); EnsureRecordPermission();
            return Track(CreateFullDuplexDevice(playbackDeviceInfo, captureDeviceInfo, format, Options(config)));
        }
    }
    /// <summary>Explains the consent requirement for Android playback capture.</summary>
    /// <param name="format">Desired format.</param>
    /// <param name="config">Optional device configuration.</param>
    /// <returns>Never returns without a projection; use the overload accepting MediaProjection.</returns>
    /// <exception cref="NotSupportedException">Android requires explicit user-approved MediaProjection.</exception>
    public override AudioCaptureDevice InitializeLoopbackDevice(AudioFormat format, DeviceConfig config = null) =>
        throw new NotSupportedException("Android playback capture requires user-approved MediaProjection. Use InitializeLoopbackDevice(projection, format). Only audio allowed by Android and the source application's capture policy can be recorded.");

    /// <summary>Captures permitted audio from other apps using an application-owned projection.</summary>
    /// <param name="projection">A live MediaProjection obtained with the user's consent.</param>
    /// <param name="format">Desired PCM format.</param>
    /// <param name="config">Optional Android options.</param>
    /// <returns>A device that does not own or stop the supplied projection.</returns>
    /// <remarks>The app must hold RECORD_AUDIO and maintain the required mediaProjection foreground
    /// service. Protected audio, calls and apps that forbid capture remain unavailable.</remarks>
    public AudioCaptureDevice InitializeLoopbackDevice(MediaProjection projection, AudioFormat format, AndroidDeviceConfig config = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            Validate(format); EnsureRecordPermission();
            return Track(new PlaybackCaptureDevice(this, projection, format, Options(config)));
        }
    }

    /// <inheritdoc />
    public override AudioPlaybackDevice SwitchDevice(AudioPlaybackDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig config = null)
    {
        ValidateOwner(oldDevice);
        var replacement = InitializePlaybackDevice(newDeviceInfo, oldDevice.Format, config ?? oldDevice.Config);
        bool running = oldDevice.IsRunning;
        try
        {
            oldDevice.Stop();
            foreach (var component in oldDevice.MasterMixer.Components) replacement.MasterMixer.AddComponent(component);
            replacement.MasterMixer.Volume = oldDevice.MasterMixer.Volume;
            if (running) replacement.Start();
        }
        catch
        {
            replacement.Dispose();
            foreach (var component in oldDevice.MasterMixer.Components) component.Parent = oldDevice.MasterMixer;
            if (running) oldDevice.Start();
            throw;
        }
        oldDevice.Dispose(); return replacement;
    }
    /// <inheritdoc />
    public override AudioCaptureDevice SwitchDevice(AudioCaptureDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig config = null)
    {
        ValidateOwner(oldDevice);
        var replacement = InitializeCaptureDevice(newDeviceInfo, oldDevice.Format, config ?? oldDevice.Config);
        bool running = oldDevice.IsRunning;
        try
        {
            oldDevice.Stop(); CopyCaptureSubscriptions(oldDevice, replacement);
            if (running) replacement.Start();
        }
        catch { replacement.Dispose(); if (running) oldDevice.Start(); throw; }
        oldDevice.Dispose(); return replacement;
    }
    /// <inheritdoc />
    public override FullDuplexDevice SwitchDevice(FullDuplexDevice oldDevice, DeviceInfo? newPlaybackInfo,
        DeviceInfo? newCaptureInfo, DeviceConfig config = null)
    {
        ValidateOwner(oldDevice);
        var replacement = InitializeFullDuplexDevice(newPlaybackInfo ?? oldDevice.PlaybackDevice.Info,
            newCaptureInfo ?? oldDevice.CaptureDevice.Info, oldDevice.Format, config ?? oldDevice.Config);
        bool running = oldDevice.IsRunning;
        try
        {
            oldDevice.Stop();
            foreach (var component in oldDevice.MasterMixer.Components) replacement.MasterMixer.AddComponent(component);
            replacement.MasterMixer.Volume = oldDevice.MasterMixer.Volume;
            CopyCaptureSubscriptions(oldDevice.CaptureDevice, replacement.CaptureDevice);
            if (running) replacement.Start();
        }
        catch
        {
            replacement.Dispose();
            foreach (var component in oldDevice.MasterMixer.Components) component.Parent = oldDevice.MasterMixer;
            if (running) oldDevice.Start();
            throw;
        }
        oldDevice.Dispose(); return replacement;
    }
    /// <inheritdoc />
    public override void UpdateAudioDevicesInfo()
    {
        lock (_gate)
        {
            if (_closing) return;
            PlaybackDevices = Enumerate(GetDevicesTargets.Outputs);
            CaptureDevices = Enumerate(GetDevicesTargets.Inputs);
        }
    }
    private DeviceInfo[] Enumerate(GetDevicesTargets targets) =>
        [new DeviceInfo { Id = 0, Name = "System default", IsDefault = true, SupportedDataFormats = [] },
         .. (_manager.GetDevices(targets) ?? []).Select(d => new DeviceInfo
         {
             Id = d.Id, Name = d.ProductName?.ToString() ?? d.Type.ToString(),
             IsDefault = false, SupportedDataFormats = []
         })];

    internal void EnsureRecordPermission()
    {
        if (_context.CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) != Permission.Granted)
            throw new UnauthorizedAccessException("The application must obtain Android RECORD_AUDIO permission before capture.");
    }
    private void Validate(AudioFormat format)
    {
        ObjectDisposedException.ThrowIf(_closing || IsDisposed, this);
        if (format.SampleRate is < 8000 or > 384000) throw new ArgumentOutOfRangeException(nameof(format), "Sample rate must be 8000 to 384000 Hz.");
        if (format.Channels is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(format), "One to eight channels are supported, subject to hardware availability.");
    }
    private void ValidateOwner(AudioDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.Engine != this) throw new ArgumentException("The device belongs to a different engine.", nameof(device));
        ObjectDisposedException.ThrowIf(device.IsDisposed, device);
    }
    private static AndroidDeviceConfig Options(DeviceConfig config)
    {
        if (config != null && config is not AndroidDeviceConfig) throw new ArgumentException("Use AndroidDeviceConfig for this backend.", nameof(config));
        var options = (AndroidDeviceConfig)config ?? new(); options.Validate(); return options;
    }
    private T Track<T>(T device) where T : AudioDevice
    {
        _devices.Add(device);
        device.OnDisposed += RemoveDevice;
        return device;
    }
    private void RemoveDevice(object sender, EventArgs args)
    {
        lock (_gate) { if (sender is AudioDevice device) _devices.Remove(device); }
    }
    private void ServiceDevices()
    {
        if (Interlocked.Exchange(ref _servicing, 1) != 0) return;
        try
        {
            AudioDevice[] devices;
            lock (_gate) { if (_closing) return; devices = _devices.ToArray(); }
            foreach (var device in devices)
            {
                try { (device as IServiceDevice)?.Service(); }
                catch (Exception ex) { LastBackendError = ex; }
            }
        }
        finally { Volatile.Write(ref _servicing, 0); }
    }
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // Reject callback disposal before the base class tears down MIDI or devices.
        if (disposing) OboeStream.AssertControlThread();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void CleanupBackend()
    {
        OboeStream.AssertControlThread();
        AudioDevice[] devices;
        lock (_gate) { _closing = true; devices = _devices.ToArray(); _devices.Clear(); }
        _timer.Dispose();
        _manager.UnregisterAudioDeviceCallback(_watcher);
        foreach (var device in devices) device.Dispose();
        _watcher.Dispose(); _handler.Dispose();
    }
    private sealed class DeviceWatcher(AndroidAudioEngine engine) : AudioDeviceCallback
    {
        private readonly WeakReference<AndroidAudioEngine> _engine = new(engine);
        public override void OnAudioDevicesAdded(AudioDeviceInfo[] addedDevices) => Changed();
        public override void OnAudioDevicesRemoved(AudioDeviceInfo[] removedDevices) => Changed();
        private void Changed()
        {
            if (!_engine.TryGetTarget(out var engine) || engine._closing) return;
            engine.UpdateAudioDevicesInfo(); engine.AudioDevicesChanged?.Invoke(engine, EventArgs.Empty);
        }
    }
}
