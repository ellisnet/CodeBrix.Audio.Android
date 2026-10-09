================================================================================
AGENT-README: CodeBrix.Audio.Android
A Guide for AI Coding Agents - CONSUMING the
CodeBrix.Audio.Android.ApacheLicenseForever NuGet package
================================================================================

OVERVIEW
========
CodeBrix.Audio.Android is the ANDROID PLATFORM PACKAGE for CodeBrix.Audio. It
targets .NET 10 for Android, on Android 13 (API level 33) and newer, for ARM64
and x64 devices.

CodeBrix.Audio's managed audio library and engine ship in
CodeBrix.Audio.Core.MitLicenseForever and are platform-neutral: file readers
and writers, mixing, sample providers, effects, metadata, MIDI files and
synthesis all run anywhere. What Core does NOT carry is a way to reach the
device: the audio devices and the native codecs come from a PLATFORM package -
CodeBrix.Audio.MitLicenseForever on Windows, Linux and macOS, and THIS package
on Android. An Android application references this package instead of the
desktop one, and every existing CodeBrix.Audio API works unchanged.

What this package adds on Android:

  * Playback, capture and full-duplex DEVICES through Android's native
    low-latency audio path, with device enumeration, hot-plug notification and
    switching a running device to another route.
  * NATIVE CODECS - WAV, MP3, FLAC and Ogg Vorbis decoding, WAV encoding -
    through the same codec seam the desktop package uses. Core's managed codecs
    and the CodeBrix.Audio.Opus add-on plug in exactly as on desktop.
  * Capture of OTHER APPLICATIONS' playback through an application-owned
    MediaProjection, within Android's capture policy.
  * AndroidDeviceConfig for the stream options Android exposes,
    AndroidAudioFocus for focus, and per-device DIAGNOSTICS (callback counts,
    underruns, buffer sizes, errors, and the allocations and time observed
    inside callbacks).
  * MIDI 1.0 byte-stream input and output ports (USB, virtual and paired
    Bluetooth) through the engine's shared MIDI API.
  * AndroidPackagedAssets: one call that copies a file or folder packaged as an
    Android asset out of the APK into private storage, once per installed
    build, and returns its path - the bridge between sample-library packages
    that deliver a SoundFont, SFZ or Decent Sampler folder as FILES and an APK
    that holds them only as assets.

One call at start-up wires it in:

    CodeBrixAndroidAudio.Initialize(applicationContext);

After that the shared output (SharedAudioOutput) and everything built on it -
AudioFilePlayer, SoundEffectClip, the sampler and synthesizer players, the
recorders and file writers - runs on Android. Applications that drive the
engine-level API construct an AndroidAudioEngine themselves.

Provenance: the managed assembly is original CodeBrix code. The native library
it P/Invokes is built in this repository from vendored third-party sources
(the device path and the codecs); THIRD-PARTY-NOTICES.txt is the authoritative
record of what came from where, under which licences, and what was changed.


INSTALLATION
============
NuGet package:   CodeBrix.Audio.Android.ApacheLicenseForever
Command:         dotnet add package CodeBrix.Audio.Android.ApacheLicenseForever

Note that the PACKAGE id carries the ".ApacheLicenseForever" suffix, but the
NAMESPACE is simply "CodeBrix.Audio.Android" (no suffix).

  License:        Apache-2.0 (licence acceptance is required)
  Depends on:     CodeBrix.Audio.Core.MitLicenseForever (pulled in
                  automatically; the current package pins the Core version it
                  was built against, so do not pin Core yourself)
  Target:         .NET 10 for Android; the consuming project must be an Android
                  APPLICATION or Android class-library project. A plain net10.0
                  project cannot reference this package.
  Android:        API level 33 (Android 13) or newer at run time.
  ABIs:           arm64-v8a and x86_64. The native library supports 16 KB
                  memory pages. There is no armeabi-v7a or x86 build.
  Native libs:    one, libcodebrix_miniaudio.so per ABI, packed inside the
                  package's Android archive and installed with the APK. Nothing
                  to copy, nothing to configure.
  Manifest:       android.permission.RECORD_AUDIO for microphone capture (and
                  a RUNTIME grant, which the application requests). Playback
                  capture additionally needs the user's MediaProjection consent
                  and the mediaProjection foreground service Android requires.
                  MIDI hardware: <uses-feature android:name=
                  "android.software.midi" android:required="false" />.

Optional add-ons, referenced by the application when it needs them, exactly as
on desktop - there is no Android variant of these add-ons:

  CodeBrix.Audio.ModestSynth.MitLicenseForever   synthesis and instruments
  CodeBrix.Audio.Opus.BsdLicenseForever          Opus decoding and encoding

These depend on Core only and are registered with their own Register() calls.
For the ModestSynthGm instrument library, call
GeneralMidiInstrumentLibrary.Register(). For Opus, call CodeBrixAudioOpus
.Register() before loading an Opus file.

CodeBrix.Audio.Samples.FluidR3Gm.MitLicenseForever is an optional SoundFont
instrument library. Use its Core-dependent package, not the older
desktop-dependent release. See Example 8 for APK asset delivery.
No desktop package or exclusion of desktop binaries is needed.
When you drive an explicit AndroidAudioEngine, register a codec add-on ON THAT
ENGINE too (for example CodeBrixAudioOpus.Register(engine)); the shared-output
registration does not reach engines you construct yourself.

See also: the CodeBrix.Audio package's own guide, at
https://github.com/ellisnet/CodeBrix.Audio/blob/main/AGENT-README.txt - it
documents SharedAudioOutput, AudioFilePlayer, the engine's AudioEngine,
AudioPlaybackDevice, AudioCaptureDevice, FullDuplexDevice, DeviceInfo,
AudioFormat, MasterMixer, SoundComponent, Recorder and the MIDI device types
that this package's classes derive from and hand back.


APPLICATION SETUP AND OWNERSHIP
===============================
Keep the shared CodeBrix.Audio API: AudioFilePlayer for recorded audio,
SoundEffectClip for preloaded effects, and MidiMusicPlayer for MIDI songs.
Android supplies the engine, asset delivery, focus and lifecycle integration.

Initialize once during application startup, before constructing players. If
you want a specific shared format, call SharedAudioOutput.Configure (for
example Configure(48000)) before the shared output opens. Register the needed
codecs and instrument libraries before loading files. Registration is not
the same as loading samples or preparing voices.

Use one application-owned initialization/preparation task so activity
recreation cannot start competing SoundFont loads or playback. Await asset
extraction and prepare expensive instruments off the UI thread. Handle errors
and cancellation, and check that the activity is still foregrounded before an
asynchronous load completes and starts playback.

Request audio focus before playing; pause or stop on focus loss. Only resume
after a gain if the user still wants playback and the application is allowed
to play in its current lifecycle state. For a foreground-only app, pause or
stop in OnStop. SharedAudioOutput is process-wide: Shutdown affects ALL its
players, so coordinate it with the owner instead of calling it whenever an
unrelated activity is destroyed.

AudioFilePlayer's path overload needs a real filesystem path. An Android
document-picker content:// URI is not one. Open it through ContentResolver
and copy it to an application-private file before passing that path to the
player. Likewise, APK assets need materialization (Example 8). Do not request
broad storage or microphone permissions just to play private audio files.


KEY NAMESPACES / USINGS
=======================
  using CodeBrix.Audio.Android;          // everything public in this package

Consumers will also use the CodeBrix.Audio namespaces they already know:

  using CodeBrix.Audio.Wave;             // SharedAudioOutput
  using CodeBrix.Audio.Playback;         // AudioFilePlayer, SoundEffectClip
  using CodeBrix.Audio.Engine.Abstracts;         // AudioEngine, SoundComponent
  using CodeBrix.Audio.Engine.Abstracts.Devices; // AudioPlaybackDevice,
                                                 //   AudioCaptureDevice,
                                                 //   FullDuplexDevice,
                                                 //   DeviceConfig
  using CodeBrix.Audio.Engine.Structs;   // AudioFormat, DeviceInfo
  using CodeBrix.Audio.Engine.Components;// SoundPlayer, Recorder

and the Android platform namespaces some members take or return:

  using Android.Content;                 // Context
  using Android.Media;                   // AudioUsageKind, AudioSource,
                                         //   AudioFocus
  using Android.Media.Projection;        // MediaProjection

CodeBrix.Audio.Android.Internal exists but is entirely internal: the native
stream, the device classes, the MIDI byte parser and the P/Invoke layer. Never
reference it.


CORE API REFERENCE
==================
Every public type in this package is listed here.

--------------------------------------------------------------------------------
CodeBrixAndroidAudio  (static)  -  the start-up entry point
--------------------------------------------------------------------------------
  static void Initialize(Context context)
      Installs the Android backend behind SharedAudioOutput, so every
      CodeBrix.Audio API that plays through the shared output uses an
      AndroidAudioEngine. Only the APPLICATION context is retained (any
      context will do; its ApplicationContext is what is kept). It opens no
      device, asks for no permission, takes no audio focus and starts no
      service. Calling it repeatedly is harmless, and the registration
      survives SharedAudioOutput.Shutdown(). Call it once, before the first
      playback - from Application.OnCreate or the first activity's OnCreate.
      Throws ArgumentNullException (null context), ArgumentException (a
      context with no application context) and InvalidOperationException (the
      packed native library's ABI does not match this assembly - a broken
      install, not a run-time condition).

  static bool IsInitialized { get; }
      Whether Initialize has run.

--------------------------------------------------------------------------------
AndroidAudioEngine : AudioEngine  -  the engine-level API
--------------------------------------------------------------------------------
Construct one when you drive the engine directly rather than through
SharedAudioOutput. Everything AudioEngine offers (see the CodeBrix.Audio guide)
is available; this section lists what THIS engine adds or specialises.

  AndroidAudioEngine(Context context)
      Creates an engine without opening any stream. Registers the native
      codec factory and Core's managed codecs; if the device reports Android
      MIDI support, installs an AndroidMidiBackend and enumerates MIDI ports;
      enumerates audio devices; subscribes to Android's device-change
      callback; and starts a 250 ms service timer (see THE SERVICE LOOP).
      Throws ArgumentNullException, ArgumentException (no application
      context) and PlatformNotSupportedException (no AudioManager).

  event EventHandler AudioDevicesChanged
      Raised on Android's MAIN thread after a device is added or removed.
      PlaybackDevices and CaptureDevices have already been refreshed when it
      fires.

  Exception LastBackendError { get; }
      The most recent CONTROL-THREAD failure noticed by the service loop: a
      device whose callback threw, a disconnection with recovery disabled, a
      failed recovery, or a capture whose permission was revoked. Callback
      failures are also visible per device in AndroidAudioDiagnostics.

  override AudioPlaybackDevice InitializePlaybackDevice(
      DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig config = null)
  override AudioCaptureDevice InitializeCaptureDevice(
      DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig config = null)
  override FullDuplexDevice InitializeFullDuplexDevice(
      DeviceInfo? playbackDeviceInfo, DeviceInfo? captureDeviceInfo,
      AudioFormat format, DeviceConfig config = null)
      Open a device. deviceInfo null, or the entry with Id 0, means the system
      default route. format: sample rate 8000..384000 Hz, 1..8 channels
      (ArgumentOutOfRangeException otherwise; what the hardware honours is up
      to Android, and the stream converts). config: null for defaults, or an
      AndroidDeviceConfig - any other DeviceConfig subclass is an
      ArgumentException. Capture and full-duplex require the RECORD_AUDIO
      permission to be GRANTED already, or UnauthorizedAccessException.
      The returned playback and capture devices implement IAndroidAudioDevice.
      A full-duplex device is two INDEPENDENT streams (playback and capture);
      their clocks are not locked to each other.

  override AudioCaptureDevice InitializeLoopbackDevice(
      AudioFormat format, DeviceConfig config = null)
      ALWAYS throws NotSupportedException: Android requires user consent for
      playback capture. Use the overload below.

  AudioCaptureDevice InitializeLoopbackDevice(
      MediaProjection projection, AudioFormat format,
      AndroidDeviceConfig config = null)
      Captures the audio OTHER applications are playing, through a live
      MediaProjection the application obtained with the user's consent. Mono
      or stereo only (NotSupportedException otherwise). Requires RECORD_AUDIO
      (UnauthorizedAccessException) and the mediaProjection foreground service
      Android mandates, both the application's responsibility. Only audio
      Android permits is delivered - media, game and unknown-usage playback
      from applications that allow capture; protected content, calls and
      applications that opt out are excluded by the platform, silently. The
      device does NOT own the projection and never stops it; stop the
      projection yourself when done. Its Capability is Loopback. It does not
      implement IAndroidAudioDevice (it is not a native stream); a read failure
      - typically the projection being revoked - stops it and is reported
      through LastBackendError.

  override AudioPlaybackDevice SwitchDevice(AudioPlaybackDevice oldDevice,
      DeviceInfo newDeviceInfo, DeviceConfig config = null)
  override AudioCaptureDevice SwitchDevice(AudioCaptureDevice oldDevice,
      DeviceInfo newDeviceInfo, DeviceConfig config = null)
  override FullDuplexDevice SwitchDevice(FullDuplexDevice oldDevice,
      DeviceInfo? newPlaybackInfo, DeviceInfo? newCaptureInfo,
      DeviceConfig config = null)
      Move a device to another route. Opens the replacement, stops the old
      device, carries the MasterMixer's components and volume (playback) and
      the OnAudioProcessed subscriptions (capture) across, starts the
      replacement if the old one was running, disposes the old device and
      RETURNS THE REPLACEMENT - keep the return value, the old reference is
      dead. If anything fails the old device is restored and restarted and the
      exception propagates. config null keeps the old device's config.
      Android's routing policy may still override an explicit choice.

  override void UpdateAudioDevicesInfo()
      Refreshes PlaybackDevices and CaptureDevices (DeviceInfo[] on the base
      class). Each list starts with a "System default" entry, Id 0, IsDefault
      true, followed by Android's hardware device ids and product names.
      SupportedDataFormats is EMPTY for every entry - an Android route's
      advertised formats do not describe what the stream can negotiate, so
      ask for the format you want and let the stream convert.

  Dispose()
      Closes every device the engine opened, the MIDI backend and the device
      watcher. Must be called from a control thread (see THREADING).

  Inherited members you will use (documented in the CodeBrix.Audio guide):
      PlaybackDevices, CaptureDevices, MidiInputDevices, MidiOutputDevices,
      UpdateMidiDevicesInfo(), CreateDecoder(stream, formatId, format),
      RegisterCodecFactory(...), and the device-started / stopped events.

--------------------------------------------------------------------------------
AndroidDeviceConfig : DeviceConfig  -  stream options (init-only)
--------------------------------------------------------------------------------
Build one before opening a device and pass it as the config argument. All
properties are requests: hardware and Android policy decide what is granted.

  bool PreferExclusive           = true
      Ask for exclusive, lowest-latency access; Android may grant shared.
  int BufferBursts               = 2      (1..16; ArgumentOutOfRangeException
                                           otherwise)
      Buffer size in hardware bursts. 2 is the low-latency default; raise it
      when diagnostics show underruns (XRunCount climbing).
  AudioUsageKind Usage           = AudioUsageKind.Media
      The Android usage attribute of playback streams. Media for music and
      general playback; Game for games; VoiceCommunication for calls.
  AudioSource InputPreset        = AudioSource.Unprocessed
      The capture preset. Unprocessed asks Android not to apply voice
      processing (where supported); VoiceRecognition or VoiceCommunication
      turn it on.
  bool RecoverDisconnectedStreams = true
      When a stream's route disappears (headphones unplugged, Bluetooth
      dropped), reopen it on the DEFAULT route automatically, keeping the
      managed graph - mixer components, subscriptions - intact. Set false to
      be told instead: the device then stops and LastBackendError reports the
      disconnection, and you decide what to open next.

--------------------------------------------------------------------------------
AndroidAudioFocus : IDisposable  -  explicit audio-focus handling
--------------------------------------------------------------------------------
Android expects an application to hold audio focus while it plays and to
react when another application takes it. This helper wraps the request; the
DECISIONS stay with the application.

  AndroidAudioFocus(Context context,
                    AudioUsageKind usage = AudioUsageKind.Media,
                    AudioFocus gain = AudioFocus.Gain)
      Prepares a focus request (content type Music, delayed gain not
      accepted) WITHOUT acquiring it. Match usage to the playback device's
      AndroidDeviceConfig.Usage. Throws ArgumentNullException and
      PlatformNotSupportedException.
  event Action<AudioFocus> FocusChanged
      Raised on Android's MAIN thread with the new focus state - Loss,
      LossTransient, LossTransientCanDuck, Gain. Pause, duck or resume your
      players here; the helper never touches playback itself.
  bool Request()
      Requests focus. TRUE means granted. A denied request is NOT permission
      to play. Throws ObjectDisposedException after Dispose.
  void Abandon()
      Releases focus; call when playback ends. Safe to call repeatedly.
  Dispose()
      Abandons focus and releases the Android request and listener.

--------------------------------------------------------------------------------
AndroidAudioDiagnostics (readonly record struct) and IAndroidAudioDevice
--------------------------------------------------------------------------------
  interface IAndroidAudioDevice
      AndroidAudioDiagnostics GetDiagnostics();
          Implemented by the playback and capture devices this engine
          returns (cast the AudioPlaybackDevice / AudioCaptureDevice). Take
          the snapshot from a control or UI thread, NEVER from an audio
          callback. Throws ObjectDisposedException once the device is
          disposed.

  readonly record struct AndroidAudioDiagnostics(
      long CallbackCount,            native data callbacks so far
      int XRunCount,                 underruns / overruns, -1 if unavailable
      int FramesPerBurst,            the hardware burst size
      int BufferFrames,              the current hardware buffer size
      int DeviceId,                  the Android device id the stream is on
      int NativeError,               the native error code; 0 = none
      Exception CallbackException,   the FIRST managed exception a callback
                                     threw, if any (rendering has stopped)
      long CallbackAllocatedBytes,   managed bytes allocated INSIDE callbacks
      double MaximumCallbackMicroseconds)  the longest callback observed

      The native counters (CallbackCount, XRunCount, FramesPerBurst,
      BufferFrames, DeviceId, NativeError) belong to the current stream and
      RESET when a stream is reopened by disconnection recovery. The managed
      counters (CallbackException, CallbackAllocatedBytes,
      MaximumCallbackMicroseconds) cover the device's whole lifetime.
      CallbackAllocatedBytes should stay at 0 for a well-behaved graph; a
      rising value means something in your components allocates on the audio
      thread.

--------------------------------------------------------------------------------
AndroidMidiBackend : IMidiBackend  -  MIDI 1.0 ports
--------------------------------------------------------------------------------
AndroidAudioEngine creates and installs one automatically when the device
reports android.software.midi, so ordinarily you use the ENGINE's shared MIDI
API - UpdateMidiDevicesInfo(), MidiInputDevices, MidiOutputDevices and the
input/output device types documented in the CodeBrix.Audio guide - and never
touch this class. It is public so an application can construct and inspect
one, or install it on another engine with UseMidiBackend.

  AndroidMidiBackend(Context context)
      Starts a dedicated control thread for device-open callbacks. Throws
      PlatformNotSupportedException when Android MIDI is unavailable.
  Exception LastReceiveError { get; }
      The most recent receive-side failure - a parser error (for example a
      SysEx over the 1 MiB safety limit) or an exception thrown by one of
      YOUR message subscribers - captured outside the JNI callback so it
      cannot crash the process.
  void Initialize(AudioEngine engine)
  void UpdateMidiDevicesInfo(out MidiDeviceInfo[] inputs,
                             out MidiDeviceInfo[] outputs)
      Enumerates byte-stream MIDI ports on USB, virtual and already-paired
      Bluetooth devices. NOTE the direction flip: an Android OUTPUT port is an
      INPUT to your application (you receive from it) and appears in inputs.
      Ids are stable for the backend's lifetime; names are "<device> / <port>".
  MidiInputDevice CreateMidiInputDevice(MidiDeviceInfo deviceInfo)
  MidiOutputDevice CreateMidiOutputDevice(MidiDeviceInfo deviceInfo)
      Open a port (control thread only). ArgumentException if the port is no
      longer listed - refresh first; IOException if Android does not open the
      device within five seconds or the port is busy.
      Received messages carry Android's monotonic-nanosecond timestamps;
      running status, fragmented SysEx and real-time bytes interleaved inside
      SysEx are all handled. An output's SendMessage(MidiMessage) returns a
      Result (failure for a data byte or 0xF0 as status - send SysEx with
      SendSysEx(byte[]), which adds the 0xF0 / 0xF7 framing itself).
  Dispose()
      Closes every open port and the control thread.

USB permission prompts and Bluetooth discovery / pairing belong to the
application and to Android; this backend only opens what Android already
lists. MIDI 2.0 (UMP) ports are not exposed.

--------------------------------------------------------------------------------
AndroidPackagedAssets  (static)  -  packaged sample libraries, by path
--------------------------------------------------------------------------------
An instrument-library package delivers its samples as files and opens them by
path. On desktop its build targets copy the files beside the executable. On
Android the same files can only be packaged as ASSETS inside the APK, readable
as streams, from no folder at all. This class turns an asset back into a file:
it copies the asset into a private folder the first time, reuses that copy on
every later launch, re-extracts after the application is updated, and returns
the path for the instrument library to open.

Every member takes an optional Context. Omit it after
CodeBrixAndroidAudio.Initialize(context) has run - the application context
retained there is used - and pass one otherwise (any context; only its
ApplicationContext is used). Without either: InvalidOperationException.

  static string Materialize(string assetPath, Context context = null)
      Extracts the asset if no current copy exists and returns the copy's
      absolute path. assetPath is the asset's path under the project's assets,
      forward-slash, relative: a file ("FluidR3_GM.sf2", "instruments/x.sf2")
      or a FOLDER, which is extracted with everything under it (an SFZ or
      Decent Sampler set). Blocking file IO on a potentially large file: call
      it from a background thread. FileNotFoundException when no such asset is
      packaged (the message says to add it as an AndroidAsset item);
      ArgumentException for an empty, absolute or ".."-escaping path.

  static Task<string> MaterializeAsync(string assetPath,
      IProgress<long> progress = null, CancellationToken cancellationToken =
      default, Context context = null)
      The same on a thread-pool thread. progress receives the bytes copied so
      far - a running count, not a fraction, because the size of a compressed
      asset is not known before it is read. Cancellation discards the partial
      copy; nothing half-written is ever mistaken for a complete asset.

  static bool IsMaterialized(string assetPath, Context context = null)
      Whether a complete, CURRENT copy exists - current meaning stamped with
      this installed build of the application.

  static void Remove(string assetPath, Context context = null)
  static void RemoveAll(Context context = null)
      Delete one extracted copy, or all of them, to reclaim storage. The next
      Materialize extracts again.

  static string RootDirectory(Context context = null)
      The folder the copies live in: a "codebrix-audio-assets" folder under
      the application's private files directory. Copies mirror their asset
      path beneath it, so two packages whose files share a name but sit in
      different asset folders never collide.

  HOW "CURRENT" IS DECIDED. Each copy carries a stamp with the application's
  version code and its LastUpdateTime. Android changes LastUpdateTime on every
  install, so a package update - or a developer redeploy with the same version
  code - re-extracts, and an unchanged install never copies twice. The copy is
  written under a temporary name and renamed into place only when complete,
  so a crash or a cancellation mid-copy leaves nothing that passes as done.

  STORAGE. An extracted asset exists twice on the device: compressed inside the
  APK and copied out. That is the price of "open by path"; RemoveAll is the
  way back when an application stops needing a library.

--------------------------------------------------------------------------------
THE SERVICE LOOP
--------------------------------------------------------------------------------
An AndroidAudioEngine runs a 250 ms timer that visits each open device and:

  * if a managed audio callback threw, stops the device and records an
    InvalidOperationException (with the callback exception as its inner
    exception) in LastBackendError; the exception is also in the device's
    diagnostics as CallbackException. Rendering does NOT resume by itself:
    fix the component, then Start() the device again or open a new one.
  * if the stream reported a native error (a disconnection), reopens it on the
    default route when RecoverDisconnectedStreams is true - keeping components
    and subscriptions - or stops it and records the error when false.
  * for capture devices, re-checks the RECORD_AUDIO permission and stops the
    device if the user revoked it.

Everything the loop does is a control-thread operation, and it runs on a
thread-pool thread, never inside an audio callback.

--------------------------------------------------------------------------------
THREADING
--------------------------------------------------------------------------------
  Audio callbacks       The native real-time audio thread. Your SoundComponents'
                        rendering and capture OnAudioProcessed handlers run
                        here. No blocking, no allocation, no Android API calls.
  Control operations    Start, Stop, Dispose, SwitchDevice, seeking,
                        GetDiagnostics, opening MIDI ports and disposing the
                        engine. They may run on ANY thread EXCEPT an audio
                        callback. Android backend control methods enforce
                        this with InvalidOperationException; do not assume
                        every shared player/transport method has that guard.
                        Serialize application transport commands yourself.
  Main-thread events    AudioDevicesChanged and AndroidAudioFocus.FocusChanged
                        arrive on Android's main (UI) thread.
  Playback capture      Reads on its own background thread, which counts as a
                        callback for the rule above.

--------------------------------------------------------------------------------
ERROR MODEL
--------------------------------------------------------------------------------
  ArgumentNullException / ArgumentException / ArgumentOutOfRangeException
      Bad arguments, as listed per member above.
  UnauthorizedAccessException
      RECORD_AUDIO not granted when opening or starting capture.
  NotSupportedException
      The consent-free loopback overload; a playback-capture format that is
      not mono or stereo; a format Android cannot capture.
  InvalidOperationException
      A control operation from inside a callback; a native open/start/restart
      failure (message names the operation and the native error code);
      playback capture failing to initialise; and, via LastBackendError, a
      device stopped by a callback exception or an unrecovered disconnection.
  PlatformNotSupportedException
      The Android audio or MIDI service is unavailable.
  ObjectDisposedException
      Any use of a disposed engine, device or focus helper.
  IOException
      A MIDI device that would not open, or a busy port.
  Managed exceptions thrown INSIDE an audio callback are caught: the output
  buffer is silenced, the stream stops, and the exception surfaces through
  diagnostics and LastBackendError. They never cross into native code.


COMPLETE EXAMPLES
=================
All examples assume an Android application project referencing this package
and, where noted, an add-on. "context" is any Android Context (an Activity,
Application or Service); only its application context is retained.

--------------------------------------------------------------------------------
Example 1 - Start-up and file playback through the shared output
--------------------------------------------------------------------------------
    using Android.App;
    using Android.OS;
    using CodeBrix.Audio.Android;
    using CodeBrix.Audio.Playback;

    [Activity(MainLauncher = true)]
    public sealed class MainActivity : Activity
    {
        private AudioFilePlayer _player;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            CodeBrixAndroidAudio.Initialize(this);   // once per process

            _player = new AudioFilePlayer();
            _player.Load(FilesDir.AbsolutePath + "/music.mp3");
            _player.Play();
        }

        protected override void OnStop()
        {
            _player?.Stop();     // no foreground service, so stop when hidden
            base.OnStop();
        }

        protected override void OnDestroy()
        {
            _player?.Dispose();
            CodeBrix.Audio.Wave.SharedAudioOutput.Shutdown();
            base.OnDestroy();
        }
    }

--------------------------------------------------------------------------------
Example 2 - An explicit engine, a playback device and a component
--------------------------------------------------------------------------------
    using CodeBrix.Audio.Android;
    using CodeBrix.Audio.Engine.Components;
    using CodeBrix.Audio.Engine.Providers;
    using CodeBrix.Audio.Engine.Structs;

    var engine = new AndroidAudioEngine(context);
    var config = new AndroidDeviceConfig { BufferBursts = 3 };  // safer
    var output = engine.InitializePlaybackDevice(null, AudioFormat.DvdHq,
                                                 config);

    // Decode a file fully BEFORE playback: no decoding happens in the callback.
    float[] pcm = DecodeWholeFile(engine, pathToFile, "flac");
    var player = new SoundPlayer(engine, AudioFormat.DvdHq,
                                 new RawDataProvider(pcm))
    {
        IsLooping = true,
        Volume = 0.5f
    };
    player.Play();
    output.MasterMixer.AddComponent(player);
    output.Start();

    // ...

    output.Stop();
    output.MasterMixer.RemoveComponent(player);
    player.Dispose();
    output.Dispose();
    engine.Dispose();

    static float[] DecodeWholeFile(AndroidAudioEngine engine, string path,
                                   string formatId)
    {
        using var stream = File.OpenRead(path);
        using var decoder = engine.CreateDecoder(stream, formatId,
                                                 AudioFormat.DvdHq);
        var block = new float[4096];
        var samples = new List<float>();
        int read;
        while ((read = decoder.Decode(block)) > 0)
            samples.AddRange(block.AsSpan(0, read).ToArray());
        return samples.ToArray();
    }

--------------------------------------------------------------------------------
Example 3 - Audio focus around playback
--------------------------------------------------------------------------------
    using Android.Media;
    using CodeBrix.Audio.Android;

    var focus = new AndroidAudioFocus(context);   // usage Media, gain Gain
    focus.FocusChanged += change =>
    {
        // Runs on the main thread. Anything below AudioFocus.None is a loss.
        if (change < AudioFocus.None) output.Stop();
        // Resume on Gain only if still foregrounded and the user still
        // wants playback. This example leaves resuming to a user action.
    };

    if (!focus.Request())
        throw new InvalidOperationException("Android denied audio focus.");
    output.Start();

    // when playback is finished for good:
    output.Stop();
    focus.Abandon();
    focus.Dispose();

--------------------------------------------------------------------------------
Example 4 - Microphone capture with the permission check the app owns
--------------------------------------------------------------------------------
    using Android.Content.PM;
    using CodeBrix.Audio.Android;
    using CodeBrix.Audio.Engine.Structs;

    const int RecordRequest = 1;

    void StartRecording(Activity activity, AndroidAudioEngine engine)
    {
        const string RecordAudio = Android.Manifest.Permission.RecordAudio;
        if (activity.CheckSelfPermission(RecordAudio) != Permission.Granted)
        {
            activity.RequestPermissions([RecordAudio], RecordRequest);
            return;   // come back here from OnRequestPermissionsResult
        }

        var recorded = new float[48000 * 2 * 10];   // ten seconds, stereo
        int count = 0;

        var config = new AndroidDeviceConfig
        {
            InputPreset = Android.Media.AudioSource.VoiceRecognition
        };
        var microphone = engine.InitializeCaptureDevice(null, AudioFormat.DvdHq,
                                                        config);
        microphone.OnAudioProcessed += (samples, capability) =>
        {
            // audio thread: copy and leave
            int room = Math.Min(samples.Length, recorded.Length - count);
            samples[..room].CopyTo(recorded.AsSpan(count));
            count += room;
        };
        microphone.Start();
    }

--------------------------------------------------------------------------------
Example 5 - Reacting to route changes and switching devices
--------------------------------------------------------------------------------
    using CodeBrix.Audio.Android;

    engine.AudioDevicesChanged += (sender, e) =>
    {
        // main thread; the lists are already refreshed
        var names = engine.PlaybackDevices.Select(d => d.Name);
        Log("Outputs now: " + string.Join(", ", names));
    };

    // Move a running output to a specific route (index into PlaybackDevices).
    engine.UpdateAudioDevicesInfo();
    var target = engine.PlaybackDevices.First(d => d.Name.Contains("USB"));
    output = engine.SwitchDevice(output, target);   // KEEP the return value

--------------------------------------------------------------------------------
Example 6 - Capturing other apps' playback with a MediaProjection
--------------------------------------------------------------------------------
    using Android.Media.Projection;
    using CodeBrix.Audio.Android;
    using CodeBrix.Audio.Engine.Structs;

    // The application has: requested and been granted RECORD_AUDIO; started
    // its mediaProjection foreground service; and obtained "projection" from
    // MediaProjectionManager after the user's consent dialog.
    var capture = engine.InitializeLoopbackDevice(projection,
                                                  AudioFormat.DvdHq);
    capture.OnAudioProcessed += (samples, capability) =>
    {
        // stereo floats; copy out and return
    };
    capture.Start();

    // ... later
    capture.Stop();
    capture.Dispose();
    projection.Stop();     // the device never stops the projection for you

--------------------------------------------------------------------------------
Example 7 - Reading diagnostics on a timer
--------------------------------------------------------------------------------
    using CodeBrix.Audio.Android;

    var timer = new Timer(_ =>
    {
        if (output is IAndroidAudioDevice device && !output.IsDisposed)
        {
            var d = device.GetDiagnostics();
            Log($"callbacks {d.CallbackCount} xruns {d.XRunCount} " +
                $"burst {d.FramesPerBurst} buffer {d.BufferFrames} " +
                $"max {d.MaximumCallbackMicroseconds:F0} us " +
                $"alloc {d.CallbackAllocatedBytes} B " +
                $"err {d.NativeError} {engine.LastBackendError?.Message}");
        }
    }, null, 1000, 1000);

--------------------------------------------------------------------------------
Example 8 - A packaged SoundFont library on Android
--------------------------------------------------------------------------------
For CodeBrix.Audio.Samples.FluidR3Gm.MitLicenseForever,
copy-to-output alone does not put the SoundFont in the APK. Add the following
to the application's csproj; select the package version explicitly or through
central package management, as in the minimum project below:

    <PropertyGroup>
      <CodeBrixFluidR3GmCopyAssetsToOutput>false</CodeBrixFluidR3GmCopyAssetsToOutput>
    </PropertyGroup>
    <ItemGroup>
      <PackageReference
        Include="CodeBrix.Audio.Samples.FluidR3Gm.MitLicenseForever"
        GeneratePathProperty="true" />
      <AndroidAsset
        Include="$(PkgCodeBrix_Audio_Samples_FluidR3Gm_MitLicenseForever)/assets/*"
        Link="soundfont/%(Filename)%(Extension)" />
    </ItemGroup>

This includes the SoundFont AND its accompanying notices. The FluidR3Gm
package adds no AndroidAsset items itself, so these are not duplicated.

After CodeBrixAndroidAudio.Initialize(context), in the application's single
asynchronous preparation task:

    using System.IO;
    using CodeBrix.Audio.Android;
    using CodeBrix.Audio.Samples.FluidR3Gm;

    var folder = await AndroidPackagedAssets.MaterializeAsync("soundfont");
    if (!FluidR3GmInstrumentLibrary.IsLoaded)
        FluidR3GmInstrumentLibrary.UseSoundFontAt(Path.Combine(folder,
            FluidR3GmInstrumentLibrary.SoundFontFileName));
    FluidR3GmInstrumentLibrary.Register();

UseSoundFontAt must run before the first sample/coverage access loads the font;
Register itself is lazy and may run earlier. IsLoaded avoids attempting to
change an already-loaded font on activity recreation; it does not synchronize
competing initialization tasks. Keep this setup under one application owner.

The SF2 alone is about 142 MiB uncompressed. Budget for both packaged and
extracted storage, plus sample memory when loaded. MaterializeAsync copies on
a worker thread and reuses the extracted copy for the same installed build;
a reinstall/update invalidates that cache. Do not remove files while a library
is using them. Materialization does not itself load or warm the synthesizer.

MIDI songs use the shared MidiMusicPlayer API with a MidiSequence and a
synthesizer factory. Resolve "ModestSynthGm" or "FluidR3Gm" through
InstrumentLibraryRegistry and use CreateMultiTimbralSynthesizer(sampleRate)
to honor the song's programs and drum channel. Use the sample rate supplied
to the player's factory; a prepared synthesizer must match that rate.

For ModestSynth's GeneralMidiSynthesizer, prepare program 0 and every program
used by the song with Prepare(program), and prepare drums with
PreparePercussion(), on a worker thread BEFORE playback. Account for later
program changes, not just the first patch. Its multi-timbral factory does not
pre-warm every instrument. Size prepared voice counts for expected polyphony;
preparing a small pool is not a guarantee that heavy songs never allocate.
For FluidR3Gm, do sample loading before playback as well. File extraction,
sample loading and voice preparation are separate costs.

--------------------------------------------------------------------------------
Example 9 - MIDI in and out through the engine
--------------------------------------------------------------------------------
    using CodeBrix.Audio.Android;

    engine.UpdateMidiDevicesInfo();
    foreach (var port in engine.MidiInputDevices)
        Log("MIDI in:  " + port.Name);
    foreach (var port in engine.MidiOutputDevices)
        Log("MIDI out: " + port.Name);

    // Opening ports and subscribing to messages uses the engine's shared MIDI
    // API - see the CodeBrix.Audio guide. Refresh the lists after the user
    // plugs or unplugs hardware; a stale MidiDeviceInfo is an
    // ArgumentException.


MINIMUM VIABLE PROJECT TEMPLATE
===============================
An Android application that plays a file. Two files plus the manifest.
This abbreviated example assumes clip.wav ALREADY exists in FilesDir (copy or
materialize it first). Add focus handling from Example 3 before playing and
stop/pause in OnStop for a foreground-only app. The activity shown owns all
audio; move initialization/shutdown to an application owner for multiple
activities. Add error handling to report missing files and decode failures.

AudioDemo.csproj:

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0-android36.1</TargetFramework>
        <SupportedOSPlatformVersion>33.0</SupportedOSPlatformVersion>
        <OutputType>Exe</OutputType>
        <ApplicationId>com.example.audiodemo</ApplicationId>
        <RuntimeIdentifiers>android-arm64;android-x64</RuntimeIdentifiers>
        <Nullable>disable</Nullable>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference
          Include="CodeBrix.Audio.Android.ApacheLicenseForever" />
      </ItemGroup>
    </Project>

(Version attributes are omitted on purpose - add the current version, or use
central package management. Core comes in transitively. Add
CodeBrix.Audio.Opus.BsdLicenseForever for .opus, and
CodeBrix.Audio.ModestSynth.MitLicenseForever for synthesis.)

Properties/AndroidManifest.xml (only if you capture):

    <manifest xmlns:android="http://schemas.android.com/apk/res/android">
      <uses-permission android:name="android.permission.RECORD_AUDIO" />
      <uses-feature android:name="android.hardware.microphone"
                    android:required="false" />
      <application android:label="AudioDemo" />
    </manifest>

MainActivity.cs:

    using Android.App;
    using Android.OS;
    using CodeBrix.Audio.Android;
    using CodeBrix.Audio.Playback;

    [Activity(MainLauncher = true)]
    public sealed class MainActivity : Activity
    {
        private AudioFilePlayer _player;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            CodeBrixAndroidAudio.Initialize(this);
            _player = new AudioFilePlayer();
            _player.Load(System.IO.Path.Combine(FilesDir.AbsolutePath,
                                                "clip.wav"));
            _player.Play();
        }

        protected override void OnDestroy()
        {
            _player?.Dispose();
            CodeBrix.Audio.Wave.SharedAudioOutput.Shutdown();
            base.OnDestroy();
        }
    }

Build with `dotnet build -c Release` and install the APK from
bin/Release/<tfm>/ with `adb install`. Run it on a PHYSICAL device: nothing
about audio latency, routing or capture is meaningful on an emulator.


BUILD CONFIGURATION AND MEASUREMENT
===================================
The compile target and minimum device OS are different: net10.0-android36.1
needs the matching Android SDK platform installed even when running on API 33.
Use the SDK and JDK supported by your .NET Android workload. Missing generated
Java types can indicate a compile-platform mismatch, not an audio failure.

Measure a Release build on hardware before judging decoder or synth speed.
Debug interpreter execution can materially increase managed decoding and
loop/seek costs. In physical-device scratch tests, an optimized Debug APK
used:

    <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
      <UseInterpreter>false</UseInterpreter>
      <AndroidUseInterpreter>false</AndroidUseInterpreter>
      <Optimize>true</Optimize>
      <AndroidEnableMarshalMethods>false</AndroidEnableMarshalMethods>
    </PropertyGroup>

The last setting worked around a native activity-method registration failure
in that particular debuggable, non-interpreted build. These are application
troubleshooting settings, not library requirements or universal performance
defaults. Rebuild after changing runtime settings. If using an explicitly
debuggable manifest for adb run-as diagnostics, keep it out of shipping builds.


PERFORMANCE TIPS
================
  - PRELOAD. The one thing that reliably ruins Android audio is work inside
    the callback. Decode short sounds fully before playback (SoundEffectClip
    on the shared output, or decode to a float[] and use RawDataProvider +
    SoundPlayer on an explicit engine). For long files, keep a bounded
    background decode queue feeding the component; do not let a synchronous
    stream provider read the disk on the audio thread. The current shared
    AudioFilePlayer uses ChunkedDataProvider, whose refills decode
    synchronously and whose loop seek can run during rendering. It does NOT
    provide that background queue automatically. Test long files and looping
    with your codecs; a successful short playback is not a gapless guarantee.
  - MEASURE with diagnostics on the device actually playing your graph.
    IAndroidAudioDevice.GetDiagnostics describes that particular device; a
    second engine's results do not measure AudioFilePlayer's shared output.
    SharedAudioOutput currently exposes neither that device nor its Android
    buffer configuration. BufferBursts below applies to explicit devices.
  - Compare CallbackAllocatedBytes and XRunCount over a warmed-up interval,
    keeping startup results separately. Managed allocation counts and
    MaximumCallbackMicroseconds are device-lifetime values: the maximum
    cannot be subtracted to get an interval maximum. Native counters reset
    on stream recovery. Aim for zero additional allocations/underruns under
    sustained load, and callbacks well within the time budget for their
    frame count and sample rate. Record route, ABI, build mode and duration.
    Transport-position polling measures application-visible timing, not
    speaker latency or the audible gap at a loop boundary.
  - BufferBursts = 2 is the low-latency default. If XRunCount climbs on a
    given device, raise it to 3 or 4; latency grows by one burst per step.
  - PreferExclusive = true asks for the fastest path. Android grants it only
    when nothing else holds the device; a shared stream is the normal outcome
    while other apps play, and is fine for music.
  - Ask for the format you want (AudioFormat.DvdHq is 48 kHz stereo float,
    what the hardware usually runs at) and let the stream convert. Do not
    probe SupportedDataFormats: it is empty by design.
  - Prefer one engine per application. SharedAudioOutput already owns one
    when started; constructing another AndroidAudioEngine does not configure
    it. Explicit engines carry their own timer, watcher and MIDI backend.
  - Register codec add-ons once, at start-up, on the shared output AND on any
    explicit engine you construct.
  - Playback capture is delivered on a background thread with a sample-rate-
    sized buffer; copy it out and return. Treat its handler like a callback.


COMMON PITFALLS TO AVOID
========================
  - CALLING Initialize TOO LATE, or not at all. Anything that plays through
    SharedAudioOutput before Initialize has run will try to open a desktop
    engine that does not exist on Android. Call it in Application.OnCreate or
    the first activity's OnCreate, before any player is constructed.
  - CONSTRUCTING MiniAudioEngine on Android. That is the desktop engine. On
    Android construct AndroidAudioEngine, or use the shared output.
  - CONTROL OPERATIONS INSIDE A CALLBACK. Android backend control methods
    such as device Stop, Dispose and GetDiagnostics enforce a callback-thread
    guard. Do not assume shared player methods all enforce it. Post transport
    commands to the main or a control thread; never seek from your callback.
  - LOSING THE DEVICE AFTER SwitchDevice. It disposes the old device and
    returns the new one. `engine.SwitchDevice(output, info);` without keeping
    the result leaves you holding a disposed device; write
    `output = engine.SwitchDevice(output, info);`.
  - TREATING A DENIED FOCUS REQUEST AS FINE. Request() returning false means
    another app holds focus; play anyway and Android's audio policy and the
    user will both dislike it. Handle FocusChanged, too: a loss with no
    handler means your music keeps playing over a phone call.
  - FORGETTING THE RUNTIME PERMISSION. The manifest entry is not enough;
    capture needs a runtime grant, and the library checks it - you get
    UnauthorizedAccessException, not silence. The check is repeated while
    recording, so a revoked permission stops the device rather than
    delivering zeros.
  - EXPECTING A CALLBACK EXCEPTION TO RESUME. A component that throws stops
    its stream. The exception is preserved in diagnostics and
    LastBackendError, and the device stays stopped until you Start() it again.
  - EXPECTING RECOVERED STREAMS TO KEEP THEIR ROUTE. Disconnection recovery
    reopens on the DEFAULT route, not the one that vanished. Subscribe to
    AudioDevicesChanged and SwitchDevice if you want a specific route back.
  - READING DIAGNOSTICS COUNTERS ACROSS A RECOVERY. The native counters reset
    when the stream reopens; only the managed ones are lifetime totals.
  - RELYING ON A FULL-DUPLEX DEVICE FOR SAMPLE-ACCURATE ALIGNMENT. Its
    playback and capture streams are independent; measure and compensate if
    you need alignment.
  - CALLING THE CONSENT-FREE LOOPBACK OVERLOAD. InitializeLoopbackDevice(
    format) always throws on Android; only the MediaProjection overload works,
    and only within Android's capture policy. Do not expect to capture a
    streaming app that opts out, or a phone call.
  - STOPPING THE PROJECTION AND EXPECTING THE DEVICE TO NOTICE GRACEFULLY. It
    notices as a read failure, stops, and reports through LastBackendError.
    Stop and dispose the device first, then the projection.
  - MIDI DIRECTION. An Android "output" port is where YOU receive from; it is
    listed under MidiInputDevices. Opening a stale MidiDeviceInfo after a
    hot-plug is an ArgumentException - refresh the list first.
  - BLOCKING IN A MIDI SUBSCRIBER. Subscribers run on Android's MIDI delivery
    thread; an exception there is captured in LastReceiveError and the parser
    state stays consistent, but slow work there delays every later message.
  - EXTRACTING A SAMPLE LIBRARY ON THE UI THREAD. AndroidPackagedAssets
    .Materialize copies what may be a very large file; call it from a
    background thread or use MaterializeAsync. Extract and configure the
    library's path BEFORE first sample access, never inside a render callback.
    A lazy Register call alone need not load the instrument files.
  - EXPECTING AN INSTRUMENT PACKAGE'S DESKTOP DELIVERY TO WORK ON ANDROID. A
    package that lands its files beside the executable through copy-to-output
    puts nothing in an APK. The file has to be an AndroidAsset, extracted with
    AndroidPackagedAssets, and the library told the path.
  - NOTHING PAUSES FOR YOU when the activity goes to the background. Without
    a foreground service, stop in OnStop; with one, keep playing and make
    sure the service and notification meet the platform's requirements.


WHAT THIS PACKAGE DOES NOT DO
=============================
  - It does not request permissions, show consent dialogs, request audio
    focus on its own, create foreground services or notifications, or react
    to the activity lifecycle. Those are application decisions, and Android's
    rules for them change with each release.
  - It does not run on an emulator in any way that tells you about real
    devices. Latency, routing, capture and Bluetooth behaviour are only
    meaningful on physical hardware.
  - It does not ship 32-bit ABIs (armeabi-v7a, x86) and does not support
    Android older than API level 33.
  - It does not decode Opus (the CodeBrix.Audio.Opus add-on does) or any
    format beyond WAV, MP3, FLAC and Ogg Vorbis natively; Core's managed
    codecs and any add-on registered on the engine extend that list.
  - It does not encode anything but WAV natively; the Opus add-on writes
    .opus, and Core's managed writers cover the rest.
  - It does not lock the playback and capture clocks of a full-duplex device
    together.
  - It does not expose MIDI 2.0 (UMP) ports, perform Bluetooth MIDI discovery
    or pairing, or handle USB permission prompts.
  - It does not capture protected audio, calls, or applications that opt out
    of playback capture - Android excludes them before this package sees any
    audio.
  - It does not expose the native library's own API. Everything is reached
    through CodeBrix.Audio's engine abstractions.
  - It does not tell an instrument library where its extracted file is by
    itself: it installs no locator in CodeBrix.Audio.Core's PackagedAssets
    seam, so the application hands the path from AndroidPackagedAssets to the
    library's own "use the file at this path" call.


WORKING EXAMPLES ON GITHUB
==========================
The diagnostics sample is the executable documentation for the device side;
the test project covers what can run on a development machine.

  https://github.com/ellisnet/CodeBrix.Audio.Android/tree/main/samples/AudioDiagnostics

  MainActivity.cs           The whole start-up contract in one activity:
                            CodeBrixAndroidAudio.Initialize, an explicit
                            AndroidAudioEngine, add-on registration on both,
                            AndroidAudioFocus with a FocusChanged handler,
                            native-codec and Opus file playback decoded BEFORE
                            playback, seek and pause / resume, synthesis
                            workloads at 1 / 32 / 128 voices, stepping through
                            output routes with SwitchDevice, the RECORD_AUDIO
                            runtime grant and microphone record / replay,
                            audio and MIDI enumeration, a diagnostics panel
                            refreshed from GetDiagnostics() on a timer, and
                            the disposal order in OnDestroy.

  https://github.com/ellisnet/CodeBrix.Audio.Android/tree/main/tests/CodeBrix.Audio.Android.Tests

  CodecTests.cs             The native codec path against a host build of the
                            same C sources: WAV / MP3 / FLAC / Ogg Vorbis
                            decoding, rate conversion, seek to start and
                            mid-file, that the codec-only library exports no
                            device symbols, and WAV encoding that decodes back
                            to the input.
  AssetStampTests.cs        The platform-neutral half of AndroidPackagedAssets:
                            asset-path validation (relative, forward-slash,
                            nothing that escapes), the on-disk layout of a
                            copy, and the per-install stamp.
  MidiByteParserTests.cs    The MIDI byte parser behind AndroidMidiBackend:
                            running status, fragmentation across callbacks,
                            real-time bytes interleaved inside SysEx, system-
                            common messages cancelling running status, an
                            interrupted SysEx, a throwing subscriber leaving
                            the parser consistent, and per-status data lengths.


QUICK REFERENCE CARD
====================
  PACKAGE   CodeBrix.Audio.Android.ApacheLicenseForever   (Apache-2.0)
  NAMESPACE using CodeBrix.Audio.Android;
  TURN ON   CodeBrixAndroidAudio.Initialize(context);   // once, at start-up
            CodeBrixAndroidAudio.IsInitialized

  ENGINE    new AndroidAudioEngine(context)               : AudioEngine
            .InitializePlaybackDevice(info?, format, config?)
            .InitializeCaptureDevice(info?, format, config?)  needs RECORD_AUDIO
            .InitializeFullDuplexDevice(playInfo?, capInfo?, format, config?)
            .InitializeLoopbackDevice(projection, format, config?)  consent
            .InitializeLoopbackDevice(format, config?)   ALWAYS THROWS
            output = .SwitchDevice(output, info, config?)   KEEP THE RESULT
            .UpdateAudioDevicesInfo()  .PlaybackDevices  .CaptureDevices
            .AudioDevicesChanged (main thread)   .LastBackendError
            .UpdateMidiDevicesInfo()  .MidiInputDevices  .MidiOutputDevices
            .CreateDecoder(stream, "wav"|"mp3"|"flac"|"ogg", format)
            .Dispose()  closes everything

  CONFIG    new AndroidDeviceConfig { PreferExclusive = true, BufferBursts = 2,
                Usage = AudioUsageKind.Media,
                InputPreset = AudioSource.Unprocessed,
                RecoverDisconnectedStreams = true }
            BufferBursts 1..16; other DeviceConfig subclasses are rejected

  FOCUS     new AndroidAudioFocus(context, usage = Media, gain = Gain)
            .Request() -> bool   .Abandon()   .FocusChanged (main thread)
            .Dispose()

  DIAG      ((IAndroidAudioDevice)device).GetDiagnostics()   control thread
            CallbackCount XRunCount FramesPerBurst BufferFrames DeviceId
            NativeError            (native: reset on recovery)
            CallbackException CallbackAllocatedBytes
            MaximumCallbackMicroseconds   (managed: device lifetime)

  ASSETS    AndroidPackagedAssets.Materialize("soundfont")   -> folder path
            .MaterializeAsync(path, progress, token)   background thread
            .IsMaterialized(path)  .Remove(path)  .RemoveAll()  .RootDirectory()
            files OR folders; extracted once per installed build; then
            FluidR3GmInstrumentLibrary.UseSoundFontAt(Path.Combine(folder,
                FluidR3GmInstrumentLibrary.SoundFontFileName))

  MIDI      new AndroidMidiBackend(context)   (the engine makes one itself)
            .UpdateMidiDevicesInfo(out inputs, out outputs)
            .CreateMidiInputDevice(info)  .CreateMidiOutputDevice(info)
            .LastReceiveError            Android OUTPUT port = your INPUT

  RULES     control operations never inside a callback (they throw)
            no blocking, no allocation on the audio thread - measure it
            the app owns permissions, focus decisions, services, lifecycle
            recovery reopens on the DEFAULT route
            a throwing callback STOPS the stream; Start() it again
            physical devices only - the emulator proves nothing

  THE PUBLIC TYPES
    CodeBrixAndroidAudio     Initialize(context) / IsInitialized
    AndroidAudioEngine       AudioEngine over native devices + native codecs
    AndroidDeviceConfig      init-only stream options (DeviceConfig)
    AndroidAudioFocus        Request / Abandon / FocusChanged (IDisposable)
    AndroidAudioDiagnostics  readonly record struct snapshot
    IAndroidAudioDevice      GetDiagnostics() on playback / capture devices
    AndroidMidiBackend       IMidiBackend for MIDI 1.0 byte-stream ports
    AndroidPackagedAssets    packaged assets out of the APK, by path
================================================================================
