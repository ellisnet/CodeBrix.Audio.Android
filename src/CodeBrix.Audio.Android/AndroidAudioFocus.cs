using System;
using global::Android.Content;
using global::Android.Media;
using global::Android.OS;

namespace CodeBrix.Audio.Android;

/// <summary>An application-controlled Android audio-focus request.</summary>
/// <remarks>Handle FocusChanged to pause/duck/resume your players. Request from a visible
/// activity or an appropriate running foreground service, and dispose when playback ends.
/// This helper never opens a permission dialog or starts a service.</remarks>
public sealed class AndroidAudioFocus : IDisposable
{
    private readonly AudioManager _manager;
    private readonly Listener _listener;
    private readonly Handler _handler;
    private readonly AudioFocusRequestClass _request;
    private bool _disposed;

    /// <summary>Creates a focus request without acquiring it.</summary>
    /// <param name="context">Application or activity context.</param>
    /// <param name="usage">Usage matching the playback device's AndroidDeviceConfig.</param>
    /// <param name="gain">Requested focus duration/type.</param>
    public AndroidAudioFocus(Context context, AudioUsageKind usage = AudioUsageKind.Media, AudioFocus gain = AudioFocus.Gain)
    {
        ArgumentNullException.ThrowIfNull(context);
        _manager = (AudioManager)context.ApplicationContext?.GetSystemService(Context.AudioService)
            ?? throw new PlatformNotSupportedException("Android AudioManager is unavailable.");
        _listener = new Listener(this); _handler = new Handler(Looper.MainLooper);
        using var attributesBuilder = new AudioAttributes.Builder();
        using var attributes = attributesBuilder.SetUsage(usage).SetContentType(AudioContentType.Music).Build();
        using var requestBuilder = new AudioFocusRequestClass.Builder(gain);
        _request = requestBuilder.SetAudioAttributes(attributes).SetAcceptsDelayedFocusGain(false)
            .SetOnAudioFocusChangeListener(_listener, _handler).Build();
    }
    /// <summary>Raised on Android's main thread. The application decides how to react.</summary>
    public event Action<AudioFocus> FocusChanged;
    /// <summary>Requests focus. A denied request must not be treated as permission to play.</summary>
    /// <returns>True when Android granted focus.</returns>
    public bool Request()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _manager.RequestAudioFocus(_request) == AudioFocusRequest.Granted;
    }
    /// <summary>Releases focus after playback stops.</summary>
    public void Abandon() { if (!_disposed) _manager.AbandonAudioFocusRequest(_request); }
    /// <summary>Abandons focus and releases the Android request and listener.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Abandon(); _disposed = true; _request.Dispose(); _listener.Dispose(); _handler.Dispose();
    }
    private sealed class Listener(AndroidAudioFocus owner) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange) => owner.FocusChanged?.Invoke(focusChange);
    }
}
