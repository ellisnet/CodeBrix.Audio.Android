================================================================================
EXTRAS-README: CodeBrix.Audio.Android
Samples, tools and other content in this repository that is not part of a NuGet
package
================================================================================

This repository ships one package - CodeBrix.Audio.Android.ApacheLicenseForever,
from src/CodeBrix.Audio.Android - and everything else described here exists to
build, test, verify or demonstrate it. None of it is packed, and none of it is
needed to consume the package.

For compilable usage of the library, read the diagnostics sample and the test
project below; AGENT-README.txt's "WORKING EXAMPLES ON GITHUB" section maps each
feature area to the file that exercises it.


samples/AudioDiagnostics/ - the physical-device diagnostics application
=======================================================================
  Path:   samples/AudioDiagnostics/AudioDiagnostics.csproj
          samples/AudioDiagnostics/MainActivity.cs
          samples/AudioDiagnostics/Properties/AndroidManifest.xml

  WHAT IT IS
    A single-activity .NET Android application whose buttons exercise the
    package on a real phone or tablet: native-codec file playback (WAV, MP3,
    FLAC, Ogg Vorbis) plus Opus through the CodeBrix.Audio.Opus add-on, seek to
    start, pause/resume, ModestSynth workloads at 1, 32 and 128 simultaneous
    voices, extracting a packaged asset out of the APK with
    AndroidPackagedAssets and playing it by path (the route a packaged sample
    library takes), stepping through the output routes, microphone recording (up
    to ten seconds) and replay, audio-focus handling, and audio / MIDI device
    enumeration. A timer refreshes a metrics panel from
    IAndroidAudioDevice.GetDiagnostics() twice a second: callback count,
    underruns, burst and buffer sizes, the longest callback, bytes allocated
    inside callbacks, GC counts and the last native or backend error. It pauses
    when it leaves the foreground and provides no foreground service and no
    MediaProjection consent UI, on purpose - those belong to a real application.

  WHY IT CONSUMES THE PACKAGE, NOT THE PROJECT
    It references CodeBrix.Audio.Android.ApacheLicenseForever as a NuGet package
    (never a ProjectReference) so that what it verifies is the packed artifact:
    the AAR with both native ABIs inside it, the transitive Core dependency and
    the add-on packages. Its AndroidAudioVersion property names the package
    version to consume; it defaults to a value in the csproj and can be
    overridden on the command line.

  HOW TO BUILD IT
    Against the published package on nuget.org (name a published version; the
    csproj default is a locally packed build that nuget.org does not carry):

        dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj -c Debug \
          -p:AndroidAudioVersion=<published-version>
        dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj \
            -c Release --no-restore -p:AndroidAudioVersion=<published-version>

    Against a locally packed package - build the library first (its bin/Release
    holds the .nupkg) or run tools/build-local.sh, then point restore at that
    folder or feed:

        dotnet build samples/AudioDiagnostics/AudioDiagnostics.csproj -c Debug \
          -p:AndroidAudioVersion=<packed-version> \
          -p:RestoreSources="<local-feed>%3Bhttps://api.nuget.org/v3/index.json"

    Both configurations build ARM64 and x64. Release runs the workload's normal
    trimming / AOT pipeline. APKs land under samples/AudioDiagnostics/bin/
    <configuration>/net10.0-android36.1/; the development-signed APK is for
    local testing only, never store publication.

  PREREQUISITES (installed by YOU - nothing here installs anything)
    The .NET 10 Android workload, an Android SDK with API 36 platform and
    build tools, and a JDK the workload accepts. The csproj picks up the SDK
    under $HOME/Android/Sdk and the distribution's JDK 21 when they exist; set
    -p:AndroidSdkDirectory / -p:JavaSdkDirectory or ANDROID_HOME / JAVA_HOME
    otherwise. Deploy to a PHYSICAL device with `adb install`; see
    MAINTAINER-README.txt, "DEVICE VALIDATION", for what to exercise and record.

  WHAT IT DEMONSTRATES
    The whole startup contract in one place: CodeBrixAndroidAudio.Initialize
    (this) for the shared output, an explicit AndroidAudioEngine for the
    engine-level API, add-on registration on both, AndroidAudioFocus with a
    FocusChanged handler that stops playback on loss, the RECORD_AUDIO runtime
    grant before capture, SwitchDevice for route changes, and disposal order in
    OnDestroy. Every fixture it plays is decoded into memory BEFORE playback,
    so no file IO or decoding happens inside the audio callback.


tools/ - build, test and verification scripts
=============================================
  tools/build-native.sh
      Builds libcodebrix_miniaudio.so for arm64-v8a and x86_64 with the Android
      NDK's CMake toolchain, strips them into src/CodeBrix.Audio.Android/native/
      <abi>/, and runs the two verifiers below. Verifies the vendored-source
      manifest first, so a stray edit under native/vendor/ stops the build.
      Needs the NDK named in MAINTAINER-README.txt under $ANDROID_SDK_ROOT,
      $ANDROID_HOME or $HOME/Android/Sdk, or CODEBRIX_ANDROID_NDK pointing at
      it. Run it from anywhere; it locates the repository itself.

  tools/test-host.sh
      Rebuilds the codec-only HOST variant of the same native sources
      (CODEBRIX_CODECS_ONLY=ON, no Android toolchain, no device code) in
      artifacts/native/host/, copies the result to its COMMITTED home,
      tests/CodeBrix.Audio.Android.Tests/runtimes/linux-x64/native/, and then
      runs the test project in Release. Extra arguments are passed to
      `dotnet test` (for example a -p:RestoreSources= override). Linux x64
      only. Run it after any change under native/ and commit the refreshed
      .so; for merely RUNNING the tests, plain `dotnet test` is enough, since
      the library is committed. See MAINTAINER-README.txt, "TESTING".

  tools/build-local.sh <version>
      The coordinated-change path: builds the sibling CodeBrix.Audio and
      CodeBrix.Audio.Opus repositories and this one at ONE date-stamped version
      into a local feed, for the case where Core and this package must change
      together before either is published. It publishes nothing and installs
      nothing. It refuses to run until the Core pins in every downstream csproj
      have been raised to that version. Details and the sibling-path overrides
      are in MAINTAINER-README.txt, "BUILDING".

  tools/verify-vendor.py
      Hashes every file under native/vendor/ against native/VENDOR-SHA256.txt
      and fails on any mismatch or any added / missing file. Run by
      build-native.sh; run it directly after touching anything vendored.

  tools/verify-native.py <ndk-path>
      Checks the exact binaries that will be packed: architecture, the full
      list of required C ABI exports, that NO device-IO exports leaked in, the
      dynamic dependencies, 16 KB ELF page alignment, and records the results
      and hashes in native/BUILD-PROVENANCE.txt. Run by build-native.sh.

  tools/verify-package.py <feed> <version>
      Opens the packed Android and Opus .nupkg files and checks their
      dependency graphs and that the AAR's native payload matches the verified
      build byte-for-byte. Run after tools/build-local.sh.


tests/Assets/ - the audio fixtures
==================================
  tone.wav, tone.mp3, tone.flac, tone.ogg, tone.opus, with PROVENANCE.txt.
  Synthetic tones copied from the sibling CodeBrix.Audio and CodeBrix.Audio.Opus
  repositories, where their tools/make_test_fixtures scripts generate them; no
  third-party recording is included. The test csproj copies them next to the
  test assembly, and the sample packs them as Android assets.


tests/CodeBrix.Audio.Android.Tests/ - the test project
======================================================
  The only other non-package project in the solution, and the executable
  documentation for the codec path, the MIDI byte parser and the asset-stamp
  logic behind AndroidPackagedAssets. It runs on the
  development host, not on Android; how and why is in MAINTAINER-README.txt,
  "TESTING". Its runtimes/linux-x64/native/ folder holds the committed host
  build of the codec library the codec tests load; those tests skip on any
  platform other than Linux x64, the MIDI parser tests run everywhere. Run it
  with:

      dotnet test CodeBrix.Audio.Android.slnx


artifacts/ - build output (ignored by git)
==========================================
  Native CMake build trees (artifacts/native/<abi>/ and artifacts/native/host/)
  and any verification reports the tools write. Nothing the solution builds or
  tests with lives here - the finished binaries are copied out to their
  committed homes under src/ and tests/. Safe to delete at any time;
  tools/build-native.sh and tools/test-host.sh recreate what they need.
================================================================================
