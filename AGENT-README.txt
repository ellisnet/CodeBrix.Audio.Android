CodeBrix.Audio.Android consumer guidance
======================================

Package: CodeBrix.Audio.Android.ApacheLicenseForever
License: Apache-2.0, with bundled MIT and Apache-2.0 upstream source notices.
Requires .NET 10 for Android; minimum Android 13/API 33; ARM64 and x64.
Depends on CodeBrix.Audio.Core.MitLicenseForever. Core retains the existing
CodeBrix.Audio.dll and CodeBrix.Audio.Engine.dll assembly names and APIs.

At application startup, call CodeBrixAndroidAudio.Initialize(applicationContext)
before opening SharedAudioOutput. For explicit engine usage create an
AndroidAudioEngine. Do not create MiniAudioEngine on Android.

Use the existing optional ModestSynth and Opus packages and their registration
APIs. Applications must reference the platform package themselves. For explicit
engines, register optional codecs on each engine as well.

The application owns RECORD_AUDIO permission prompts, audio-focus decisions,
activity lifecycle and foreground services. AndroidAudioFocus offers explicit
request/abandon operations and focus-change events; it does not pause playback.

Microphone capture requires RECORD_AUDIO. Playback capture additionally requires
application-owned MediaProjection consent and a mediaProjection foreground
service. Android capture policy still excludes protected or disallowed audio.
Pass the projection to AndroidAudioEngine.InitializeLoopbackDevice; the library
does not stop the supplied projection. Playback capture accepts mono/stereo.

Use AndroidDeviceConfig for backend options. Automatic disconnection recovery
reopens the default route. Use SwitchDevice for deliberate route selection.
Full duplex uses independent playback/capture streams, without a clock-lock promise.
AudioDevicesChanged is delivered on Android's main thread. Refresh MIDI ports
with UpdateMidiDevicesInfo. MIDI supports byte-stream MIDI 1.0, not UMP.

Call start/stop/dispose/switch/seek and GetDiagnostics outside audio callbacks.
IAndroidAudioDevice exposes stream diagnostics. LastBackendError on the engine
records control-thread failures. Callback exceptions stop rendering and are
retained in diagnostics. Dispose owned devices and engines.

WAV, MP3, FLAC and Ogg/Vorbis decoding uses native codecs. Optional Opus keeps
its existing codec implementation. Mixing and ModestSynth remain shared managed
code. Avoid blocking work and allocations in rendering callbacks; preload or
background-buffer file content when the application requires predictable latency.

See the package README for complete integration guidance and platform limitations.
