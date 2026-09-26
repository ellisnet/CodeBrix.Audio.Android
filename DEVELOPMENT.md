# Android audio implementation

This repository provides the Apache-2.0 Android backend for the MIT CodeBrix.Audio.Core
package. Work is coordinated with the sibling CodeBrix.Audio and CodeBrix.Audio.Opus
repositories. CodeBrix.Android is read-only. Do not commit or push on Jeremy's behalf.
Do not use an Android emulator/AVD for this project on this workstation.

## Required contract

- Existing CodeBrix.Audio.MitLicenseForever desktop consumers need zero changes.
- Core owns CodeBrix.Audio.dll and CodeBrix.Audio.Engine.dll, preserving public APIs.
- Desktop Audio, Android Audio, ModestSynth and Opus depend on Core.
- Android: .NET 10, SDK 36.1, minimum API 33, arm64-v8a and x86_64.
- Oboe handles devices; native miniaudio/stb_vorbis handles codecs. Shared managed
  mixing, synthesis, effects, metadata, MIDI and file APIs retain their behavior.
- One explicit Android startup registration using application context. Permission
  prompts, foreground services and UI lifecycle decisions belong to the application.
- Inter-repository development uses pinned packages. Published packages come from
  nuget.org; a local feed is used only for packages not yet published.

## Work and verification

- [x] Core/desktop package split and backend selection seam.
- [x] Opus/Core and ModestSynth/Core package dependencies.
- [x] Pinned Oboe and miniaudio/stb sources with licenses and reproducible native builds.
- [x] Oboe playback, capture, duplex, routing and stream diagnostics implementation.
- [x] Android focus, lifecycle, device changes, MIDI and permitted playback capture implementation.
- [x] Diagnostics application and documentation.
- [x] Desktop tests and package/binary compatibility verification.
- [x] Android Debug/Release builds, both architectures, native ABI and 16 KB alignment.
- [ ] Physical ARM64 and x64 listening, lifecycle, capture and workload validation
      (separate follow-up with Jeremy; no device results claimed by initial development).

Development toolchain verified: .NET SDK 10.0.401, Android workload 36.1.69,
NDK 30.0.16248370, CMake 3.31.6, JDK 21. Android SDK is
`/home/jeremy/Android/Sdk`; the system Java default is newer than the workload accepts,
so build commands explicitly select `/usr/lib/jvm/java-21-openjdk-amd64`.

Future .NET 11/API 37 compilation, API 38 and Googlebook runtime validation remain
future compatibility work; these targets are not claimed tested here.

## Package versions

Package versions are literal `PackageReference` versions in the csproj files; there
is no Directory.Build.props. The library csproj pins Core, and it must name a version
that is on nuget.org whenever this package is published. The host-test csproj and the
sample carry the same Core-family number (the sample through ModestSynth), and the
sample also pins the published Opus package and defaults `AndroidAudioVersion` to
the Android package it consumes; set the latter to the published Android version
once one exists. Core, Desktop, ModestSynth and Opus are all published, so the
library and the host tests restore from nuget.org alone:

```bash
dotnet build src/CodeBrix.Audio.Android/CodeBrix.Audio.Android.csproj -c Release
bash tools/test-host.sh
```

## Build from sibling checkouts

This is the coordinated-change path, for when Core or Opus and this repository
must change together before any of them is published. Run commands in this repository. Install the released .NET 10 Android workload,
Android SDK 36.1, JDK 21, CMake and NDK 30 first. The library and sample csproj files
select this workstation's SDK/JDK only if those locations exist; elsewhere pass
`-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...` to dotnet. Native builds accept
`ANDROID_SDK_ROOT` and `CODEBRIX_ANDROID_NDK` environment overrides.

Choose one fresh date-stamped version for a coordinated build. Do not reuse a
version whose packages are already in NuGet's global cache after changing their
contents. Raise the Core pin in the Opus library csproj and in this repository's
library and host-test csproj files to that version first; the script checks and
refuses to build otherwise. It then builds Core/Desktop/ModestSynth, then Opus, then
both Android native ABIs and the Android package:

```bash
bash tools/build-local.sh <fresh-version>
```

The default local feed is `../CodeBrix.Audio/artifacts/android-port/feed`.
Override sibling paths with `CODEBRIX_AUDIO_REPO` / `CODEBRIX_OPUS_REPO`, or the
feed with `CODEBRIX_LOCAL_FEED`. The script publishes nothing, installs no tools
and does not touch CodeBrix.Android. The Core pins in both downstream repositories
then name the development Core package; set them back to a released Core version
before releasing dependents.

For just the native libraries, run `bash tools/build-native.sh`. It verifies the
vendored source manifest, builds both ABIs, strips the shared objects and checks
architecture, exported ABI, dependencies and 16 KB ELF alignment. These exact
files are embedded in the Android NuGet package's AAR under `jni/<abi>/`.
Do not distribute the Linux host test library as an Android asset.

## Verification

See [VERIFICATION.md](VERIFICATION.md) for the initial build's recorded results,
artifact locations and the distinction between host checks and pending device tests.

```bash
feed="$(realpath ../CodeBrix.Audio/artifacts/android-port/feed)"
dotnet test --solution ../CodeBrix.Audio/CodeBrix.Audio.slnx -c Release --no-build
dotnet test --solution ../CodeBrix.Audio.Opus/CodeBrix.Audio.Opus.slnx -c Release --no-build
bash tools/test-host.sh -p:RestoreSources="$feed%3Bhttps://api.nuget.org/v3/index.json"
```

Against published packages, `bash tools/test-host.sh` with no arguments uses the
pinned Core from nuget.org.

The host tests exercise the actual codec-only miniaudio/stb build: WAV, MP3, FLAC,
Ogg/Vorbis, rate conversion, seek reset, WAV encoding and MIDI byte parsing. They
do not test Android's device stack. The desktop suite includes factory registration
and failure cleanup. The Opus suite verifies the managed codec against Core with
the desktop package explicitly supplying its test platform.

`../CodeBrix.Audio/tools/verify-packages` contains SDK API compatibility validation,
package ownership/dependency checks and a consumer compiled against the pre-split
desktop package. Keep a pre-change baseline package for these checks. The initial
comparison preserved all desktop native/license assets byte-for-byte and ran that
unchanged consumer binary with the new managed assemblies.

## Diagnostics APK

The sample intentionally consumes the packed NuGet artifacts, not project
references, to verify transitive assembly and native packaging. Once the Android
package is published and the sample's `AndroidAudioVersion` default names it, the
sample builds from nuget.org alone:

```bash
dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj -c Debug
dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj -c Release --no-restore
```

Before that, pack the Android package locally (the library build writes it to
`src/CodeBrix.Audio.Android/bin/Release/`, or `tools/build-local.sh` copies it into
the feed) and point the sample at it. Core and Opus resolve from nuget.org:

```bash
feed="$(realpath ../CodeBrix.Audio/artifacts/android-port/feed)"
dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj -c Debug \
  -p:AndroidAudioVersion=<packed-version> \
  -p:RestoreSources="$feed%3Bhttps://api.nuget.org/v3/index.json"
dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj -c Release --no-restore \
  -p:AndroidAudioVersion=<packed-version>
```

Both configurations include ARM64 and x64. Release uses the workload's normal
linking/AOT pipeline. APKs appear under `samples/AudioDiagnostics/bin/<configuration>/net10.0-android36.1/`.
The development signed APK is for local testing, not store publication.

The sample covers native/Opus file playback, seek, pause/resume, ModestSynth voice
workloads, route changes, microphone record/replay and device/MIDI enumeration.
It pauses when backgrounded and supplies no foreground service or MediaProjection
consent UI. Use [DEVICE-VALIDATION.md](DEVICE-VALIDATION.md) to complete the physical
ARM64/x64 checks, including consuming-app workloads and platform lifecycle behavior.
