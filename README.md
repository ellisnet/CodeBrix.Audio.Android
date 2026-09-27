# CodeBrix.Audio.Android

The Android platform package for [CodeBrix.Audio](https://github.com/ellisnet/CodeBrix.Audio): device playback and capture through Android's native low-latency audio path, native WAV, MP3, FLAC and Ogg Vorbis codecs, audio-focus and device-change helpers, MIDI 1.0 ports, and one-call extraction of packaged sample libraries out of the APK, for the shared managed audio engine that ships in CodeBrix.Audio.Core. CodeBrix.Audio.Android is provided as a .NET 10 for Android library and associated `CodeBrix.Audio.Android.ApacheLicenseForever` NuGet package.

CodeBrix.Audio.Android supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.Audio.Android.ApacheLicenseForever
```

Note that the NuGet package ID and the namespace are different - there is no package named plain `CodeBrix.Audio.Android`:

* NuGet package ID: `CodeBrix.Audio.Android.ApacheLicenseForever`
* Assembly and primary namespace: `CodeBrix.Audio.Android` - i.e. `using CodeBrix.Audio.Android;`

XML documentation (IntelliSense) ships alongside the assembly.

The package pulls in the following automatically; no version pinning is needed in the consuming project:

* `CodeBrix.Audio.Core.MitLicenseForever` - the shared managed audio library and engine (the `CodeBrix.Audio` and `CodeBrix.Audio.Engine` assemblies) that this package supplies the Android devices and native codecs for.

This is the Android counterpart of `CodeBrix.Audio.MitLicenseForever`, the desktop platform package: an Android application references this package *instead of* that one, and every existing CodeBrix.Audio API - playback, mixing, sample providers, effects, metadata, MIDI files, synthesis and the file readers and writers - works unchanged. The optional add-ons, `CodeBrix.Audio.ModestSynth.MitLicenseForever` and `CodeBrix.Audio.Opus.BsdLicenseForever`, depend on Core only and work with this package as they do on desktop; there is no Android variant of either.

## CodeBrix.Audio.Android supports:

* One startup call, `CodeBrixAndroidAudio.Initialize(context)`, after which the shared output and all of CodeBrix.Audio's high-level playback APIs run on Android
* An explicit `AndroidAudioEngine` for applications that drive the engine-level API directly: playback, capture and full-duplex devices, device enumeration, and switching a running device to another route while keeping its mixer components and subscriptions
* Native decoding of WAV, MP3, FLAC and Ogg Vorbis, and native WAV encoding, through the same codec seam the desktop package uses; managed codecs and the Opus add-on plug in exactly as they do on desktop
* Microphone capture, once the application holds the `RECORD_AUDIO` permission
* Capture of other applications' playback through an application-owned `MediaProjection`, subject to Android's capture policy
* `AndroidDeviceConfig` for the stream options Android exposes: exclusive low-latency access, buffer size in hardware bursts, playback usage, capture preset, and automatic recovery of a disconnected stream on the default route
* `AndroidAudioFocus`, an explicit request / abandon helper with a focus-change event the application reacts to
* An `AudioDevicesChanged` event and refreshed device lists when routes are plugged or unplugged
* Per-device diagnostics through `IAndroidAudioDevice.GetDiagnostics()`: callback count, underruns, burst and buffer sizes, the selected device, native and managed errors, and the allocations and longest time observed inside callbacks
* MIDI 1.0 byte-stream input and output ports (USB, virtual and paired Bluetooth devices) through the engine's shared MIDI API, with running status, fragmented SysEx and interleaved real-time messages handled
* `AndroidPackagedAssets`, which copies a file or folder packaged as an Android asset out of the APK into private storage once per installed build and returns its path - the bridge between sample-library packages that deliver a SoundFont, SFZ or Decent Sampler folder as files and an APK that holds them only as assets
* ARM64 and x64 devices, with 16 KB memory pages supported by the native libraries

## Requirements

* Android 13 (API level 33) or newer
* A .NET Android application project; this package cannot be referenced from a plain `net10.0` library
* Microphone capture needs the `RECORD_AUDIO` manifest entry and a runtime grant; playback capture additionally needs the user's `MediaProjection` consent and the foreground service Android requires for it. The library checks the permission before recording; the application decides when to ask for it.

## What the application owns

The package never asks for a permission, requests audio focus on its own, creates a foreground service or reacts to the activity lifecycle. Those decisions belong to the application: request focus with `AndroidAudioFocus` before playing and handle its `FocusChanged` event; pause or stop when the activity leaves the foreground unless a foreground service keeps audio alive; obtain and hold the `MediaProjection` for playback capture; and manage USB permission and Bluetooth pairing for MIDI hardware. Start, stop, seek, switch, diagnostics and disposal are control-thread operations - never call them from inside an audio callback, and keep callbacks free of blocking work and allocations.

## Sample Code

### Start up and play a file

```csharp
using CodeBrix.Audio.Android;
using CodeBrix.Audio.Playback;

// Once, at application startup - before the first playback. It opens no device,
// asks for no permission and takes no audio focus.
CodeBrixAndroidAudio.Initialize(applicationContext);

// Everything above the engine is the ordinary CodeBrix.Audio API.
var player = new AudioFilePlayer();
player.Load(pathToAudioFile);   // .wav, .mp3, .flac, .ogg - and .opus with the Opus add-on
player.Play();
```

### Drive the engine directly

```csharp
using CodeBrix.Audio.Android;
using CodeBrix.Audio.Engine.Structs;

using var engine = new AndroidAudioEngine(applicationContext);

var config = new AndroidDeviceConfig { PreferExclusive = true, BufferBursts = 2 };
using var output = engine.InitializePlaybackDevice(null, AudioFormat.DvdHq, config);
output.MasterMixer.AddComponent(mySoundComponent);   // any CodeBrix.Audio.Engine component
output.Start();
```

Optional codec add-ons register on an explicit engine as well as on the shared output, for example `CodeBrixAudioOpus.Register(engine)`.

### Take and release audio focus

```csharp
using CodeBrix.Audio.Android;
using Android.Media;

using var focus = new AndroidAudioFocus(applicationContext);
focus.FocusChanged += change => { if (change < AudioFocus.None) output.Stop(); };

if (!focus.Request()) return;   // a denied request is not permission to play
output.Start();
// ... later, when playback ends:
focus.Abandon();
```

### Record from the microphone

```csharp
using CodeBrix.Audio.Android;
using CodeBrix.Audio.Engine.Structs;

// The application has already obtained the RECORD_AUDIO runtime grant.
using var microphone = engine.InitializeCaptureDevice(null, AudioFormat.DvdHq);
microphone.OnAudioProcessed += (samples, capability) =>
{
    // interleaved 32-bit floats; copy them out - do no blocking work here
};
microphone.Start();
```

### Use a packaged sample library

```csharp
using CodeBrix.Audio.Android;
using CodeBrix.Audio.Samples.FluidR3Gm;

// The SoundFont is packaged as an Android asset. Extract it once (a background
// thread; it is a large file), then point the instrument library at the copy.
var path = await AndroidPackagedAssets.MaterializeAsync("FluidR3_GM.sf2");
FluidR3GmInstrumentLibrary.UseSoundFontAt(path);
FluidR3GmInstrumentLibrary.Register();
```

The copy is reused on every later launch and refreshed automatically after the application is updated. A folder-based library, such as an SFZ or Decent Sampler set, is extracted the same way by naming its asset folder.

### Read the stream diagnostics

```csharp
using CodeBrix.Audio.Android;

// From a control or UI thread, never from inside a callback.
if (output is IAndroidAudioDevice device)
{
    var d = device.GetDiagnostics();
    Log($"callbacks {d.CallbackCount}, underruns {d.XRunCount}, " +
        $"longest callback {d.MaximumCallbackMicroseconds:F0} us, " +
        $"allocated in callbacks {d.CallbackAllocatedBytes} bytes");
}
```

## Documentation

The NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against this library.

The engine, device, mixer, component and MIDI types this package plugs into belong to CodeBrix.Audio; read that package's `AGENT-README.txt` for them. This package supplies the Android devices, codecs and helpers they use.

A complete diagnostics application that exercises every feature above on a physical device is in the `samples/AudioDiagnostics` project, and additional sample code is in the `CodeBrix.Audio.Android.Tests` project:
https://github.com/ellisnet/CodeBrix.Audio.Android/tree/main/samples/AudioDiagnostics
https://github.com/ellisnet/CodeBrix.Audio.Android/tree/main/tests/CodeBrix.Audio.Android.Tests

## License

CodeBrix.Audio.Android is licensed under the Apache License 2.0 - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.Audio.Android/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.Audio.Android/blob/main/THIRD-PARTY-NOTICES.txt).
