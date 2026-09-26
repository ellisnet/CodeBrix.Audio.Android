# CodeBrix.Audio.Android

Oboe audio devices and native codecs for CodeBrix.Audio on **.NET 10 for Android**,
Android 13/API 33 and newer, on **ARM64 and x64**. The library compiles against
Android SDK 36.1. Its native libraries support 16 KB memory pages.

Initial implementation and build verification are complete. Physical-device
listening, capture, routing and sustained workload validation are still required;
see [DEVICE-VALIDATION.md](DEVICE-VALIDATION.md). No emulator results are claimed.

## Packages

| Package ID | Dependency | Purpose |
| --- | --- | --- |
| CodeBrix.Audio.Core.MitLicenseForever | — | Existing CodeBrix.Audio and CodeBrix.Audio.Engine managed assemblies |
| CodeBrix.Audio.MitLicenseForever | Core | Existing Windows, Linux and macOS backend and native assets |
| CodeBrix.Audio.Android.ApacheLicenseForever | Core | Android Oboe devices and native codecs |
| CodeBrix.Audio.ModestSynth.MitLicenseForever | Core | Shared synthesis and instrument functionality |
| CodeBrix.Audio.Opus.BsdLicenseForever | Core | Shared Opus decoding and encoding |

Existing desktop consumers keep their existing package reference and startup code.
Android applications reference this package, plus whichever optional packages they
use. ModestSynth and Opus applications must reference the appropriate platform
package themselves. Core alone provides the shared managed APIs; native codecs and
device operations require a platform package. No Android variants of ModestSynth
or Opus are needed.

## Startup and playback

Call once from application startup, before using shared audio output:

```csharp
using CodeBrix.Audio.Android;

CodeBrixAndroidAudio.Initialize(applicationContext);
```

This installs the Android engine factory for `SharedAudioOutput` and retains only
the application context. The existing high-level playback, mixing, sample-provider,
effects, metadata, MIDI-file, synthesis and file APIs remain available. Register
optional ModestSynth and Opus functionality as described by those packages.

Applications using the lower-level engine API construct `AndroidAudioEngine`
instead of `MiniAudioEngine`:

```csharp
using var engine = new AndroidAudioEngine(applicationContext);
using var output = engine.InitializePlaybackDevice(null,
    CodeBrix.Audio.Engine.Structs.AudioFormat.DvdHq);
// Add existing SoundComponent instances to output.MasterMixer.
output.Start();
```

For an explicitly created engine, register optional codec factories on that engine
too (for example, `CodeBrix.Audio.Opus.CodeBrixAudioOpus.Register(engine)`). Startup does not ask
for permissions, obtain audio focus or create a foreground service.

## Devices and diagnostics

`PlaybackDevices` and `CaptureDevices` include a system-default entry with ID zero
and Android hardware device IDs. `AudioDevicesChanged` runs on Android's main
thread. `SupportedDataFormats` is empty because an Android route's advertised
formats do not describe every conversion the Oboe stream can negotiate.

`AndroidDeviceConfig` requests buffer bursts, exclusive sharing, usage and capture
preset. Hardware and Android policy determine whether requests can be honored.
Disconnection recovery is enabled by default and reopens the system-default route.
Use `SwitchDevice` for an explicit route; it preserves mixer components and volume
or recording subscriptions. Full-duplex devices use separate playback and capture
streams: hardware clock synchronization is not guaranteed.

Cast a playback or microphone device to `IAndroidAudioDevice` and call
`GetDiagnostics()` on a control thread for callback counts, xruns, burst/buffer
sizes, current device ID, native errors, managed callback failures, callback
allocations and maximum callback duration. Native counters reset when a stream is
reopened; managed allocation/time counters cover the device's lifetime.
`AndroidAudioEngine.LastBackendError` reports control-thread failures.

Call start, stop, seek, switch, diagnostics and disposal operations outside audio
callbacks. Exceptions in a rendering callback are caught and reported, and stop
the stream instead of crossing the native ABI. Dispose devices and engines when
finished.

## Android application responsibilities

Microphone capture requires the manifest declaration and an application-managed
runtime grant for `android.permission.RECORD_AUDIO`. The library checks permission
before recording. Applications decide when to request, explain or retry a grant.

`AndroidAudioFocus` provides focus request/abandon operations and a focus-change
event. The application handles a denied request and decides whether to pause,
duck or resume. It also owns activity lifecycle policy and any required foreground
services/notifications. See Android's [audio focus guidance](https://developer.android.com/media/optimize/audio-focus)
and [Android 17 background-audio changes](https://developer.android.com/about/versions/17/changes/bg-audio)
when updating application targets.

Playback capture uses
`engine.InitializeLoopbackDevice(mediaProjection, format)`. The application must
obtain MediaProjection consent and maintain the required foreground service.
The library neither owns nor stops the supplied projection. Capture supports mono
or stereo and eligible media/game/unknown-usage playback. Protected audio, calls,
other profiles and applications that opt out cannot be captured. Calling the
ordinary loopback overload without a projection explains this requirement.
See [Android playback capture](https://developer.android.com/media/platform/av-capture).

Android MIDI 1.0 byte-stream ports are available through the existing engine MIDI
API when the device exposes Android MIDI support. Refresh the device list with
`UpdateMidiDevicesInfo()`. USB permission and Bluetooth pairing belong to the app
and Android. Receive timestamps use Android's monotonic nanosecond time base;
fragmented SysEx and running status are supported, with a 1 MiB receive SysEx limit.
The shared MIDI API does not expose MIDI 2.0 UMP.

## Codecs, synthesis and real-time work

Oboe uses native AAudio for playback and microphone capture. A codec-only miniaudio
build supplies native WAV, MP3 and FLAC support, with stb_vorbis for Ogg/Vorbis.
The optional Opus package and managed codecs retain their existing implementation.
ModestSynth uses the same shared managed synthesis code on both platforms.

The native callback adapter passes float buffers directly to the shared engine.
This does not make every user graph allocation-free: existing synchronous stream
providers can decode or read files during rendering. For demanding real-time
workloads, preload audio or provide a bounded background decode/read queue, avoid
blocking and allocations in callbacks, and measure the actual application graph.

## Development and future targets

See [DEVELOPMENT.md](DEVELOPMENT.md) for reproducible native builds, local NuGet-feed
development and the physical-device diagnostics application. Pinned upstream source
and local patches are recorded in `native/PROVENANCE.txt` and
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

.NET 11/API 37 compilation will be a separate upgrade after the stable toolchain is
available. API 38 and Googlebook runtime compatibility require future testing; they
are not claimed verified by this implementation.
