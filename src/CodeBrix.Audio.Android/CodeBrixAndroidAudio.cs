using CodeBrix.Audio.Android.Internal;
using CodeBrix.Audio.Wave;
using global::Android.Content;

namespace CodeBrix.Audio.Android;

/// <summary>Installs the Android backend used by the existing CodeBrix playback APIs.</summary>
public static class CodeBrixAndroidAudio
{
    private static readonly object Gate = new();
    private static Context? _context;

    /// <summary>Registers Oboe and native codecs before the application's first playback.</summary>
    /// <param name="context">Any Android context; only its application context is retained.</param>
    /// <remarks>Does not open an audio device, request permissions, acquire focus or start a
    /// service. Calling repeatedly is harmless. Registration survives SharedAudioOutput.Shutdown.</remarks>
    public static void Initialize(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (Gate)
        {
            if (_context != null) return;
            if (Native.AbiVersion() != 1) throw new InvalidOperationException("The Android audio native ABI version is incompatible.");
            var app = context.ApplicationContext ?? throw new ArgumentException("An application context is required.", nameof(context));
            SharedAudioOutput.UseEngineFactory(() => new AndroidAudioEngine(app));
            _context = app;
        }
    }

    /// <summary>Whether application startup has installed the Android backend.</summary>
    public static bool IsInitialized { get { lock (Gate) return _context != null; } }
}
