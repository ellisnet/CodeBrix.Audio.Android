================================================================================
MAINTAINER-README: CodeBrix.Audio.Android
Notes for people and agents MAINTAINING this repository - not for package
consumers
================================================================================

If you are CONSUMING the NuGet package, stop reading and open AGENT-README.txt
instead. Everything below is about the repository itself: how it is laid out,
how it builds (managed and native), how it is tested, how it is packaged, what
still has to be verified on real hardware, and the conventions the source
follows.


PURPOSE AND SCOPE
=================
This repository produces exactly one NuGet package:

  CodeBrix.Audio.Android.ApacheLicenseForever
      Assembly:      CodeBrix.Audio.Android
      Project:       src/CodeBrix.Audio.Android/CodeBrix.Audio.Android.csproj
      Target:        net10.0-android36.1, SupportedOSPlatformVersion 33.0
      Native:        libcodebrix_miniaudio.so for arm64-v8a and x86_64, packed
                     in the package's AAR under jni/<abi>/
      License:       Apache-2.0
      Consumer doc:  AGENT-README.txt (repo root)

It is the ANDROID PLATFORM PACKAGE for CodeBrix.Audio: Oboe playback / capture
devices, a codec-only miniaudio + stb_vorbis build for native WAV / MP3 / FLAC /
Ogg Vorbis, audio focus, device-change notification, playback capture through
MediaProjection, and MIDI 1.0 ports - all for the shared managed engine that
ships in CodeBrix.Audio.Core.

WHY IT IS A SEPARATE REPOSITORY, AND APACHE-2.0. CodeBrix.Audio holds a licence
bar of MIT or more permissive, which its package id promises. Oboe is
Apache-2.0, whose patent and notice clauses MIT does not have, so the Android
backend lives here under Apache-2.0 - the same reasoning that put the Opus codec
in CodeBrix.Audio.Opus. Do not propose folding it into CodeBrix.Audio.


HOW THE AUDIO FAMILY FITS TOGETHER (the contract this repo depends on)
=====================================================================
  CodeBrix.Audio.Core.MitLicenseForever    the managed CodeBrix.Audio and
                                           CodeBrix.Audio.Engine assemblies.
                                           Platform-neutral. Built in the
                                           CodeBrix.Audio repository.
  CodeBrix.Audio.MitLicenseForever         the DESKTOP platform package:
                                           Windows / Linux / macOS native
                                           backend and assets. Depends on Core.
  CodeBrix.Audio.Android.ApacheLicenseForever   THIS package. Depends on Core.
  CodeBrix.Audio.ModestSynth.MitLicenseForever  add-on; depends on Core only.
  CodeBrix.Audio.Opus.BsdLicenseForever         add-on; depends on Core only.

Rules that keep this working:
  - Desktop consumers of CodeBrix.Audio.MitLicenseForever need ZERO changes
    when this package changes. Core owns the CodeBrix.Audio.dll and
    CodeBrix.Audio.Engine.dll assembly identities and their public APIs.
  - An application picks ONE platform package. The add-ons never reference a
    platform package; the application does.
  - This repository consumes Core as a PINNED NUGET PACKAGE, never as a
    project reference into the sibling checkout. The pin lives in the library
    csproj (and is mirrored in the test csproj); see PACKAGING AND PUBLISHING.
  - Published packages come from nuget.org. A local feed is used ONLY for a
    coordinated change where Core and this package must move together before
    either is published (see BUILDING, "Coordinated builds").
  - The engine seam this backend plugs into - SharedAudioOutput
    .UseEngineFactory, the MiniAudioCodecFactory over the sf_* native ABI,
    AudioEngine's device abstractions, IMidiBackend - belongs to Core. Change
    it there first, publish Core, then raise the pin here.


REPOSITORY LAYOUT
=================
  src/CodeBrix.Audio.Android/
      CodeBrixAndroidAudio.cs     the Initialize() entry point
      AndroidAudioEngine.cs       AudioEngine over Oboe: devices, switching,
                                  enumeration, the 250 ms service loop, the
                                  device watcher, MIDI backend installation
      AndroidDeviceConfig.cs      public stream options (DeviceConfig)
      AndroidAudioFocus.cs        public focus helper
      AndroidAudioDiagnostics.cs  the diagnostics record + IAndroidAudioDevice
      AndroidMidiBackend.cs       IMidiBackend over Android's MIDI service
      AndroidPackagedAssets.cs    extracts packaged assets (sample libraries)
                                  out of the APK into private storage, once
                                  per installed build, and hands back paths
      Internal/
          AssetStamp.cs           the platform-neutral half of that: asset-path
                                  validation, on-disk layout, the per-install
                                  stamp (linked into the test project)
          Native.cs               LibraryImport declarations for the cb_oboe_*
                                  C ABI, and the StreamHandle SafeHandle
          OboeStream.cs           one native stream: open / start / stop /
                                  recover, the UnmanagedCallersOnly render
                                  callback, the control-thread guard, and the
                                  diagnostics counters
          AndroidPlaybackDevice.cs, AndroidCaptureDevice.cs
                                  the AudioPlaybackDevice / AudioCaptureDevice
                                  implementations over OboeStream, plus the
                                  IServiceDevice hook the service loop calls
          PlaybackCaptureDevice.cs   MediaProjection capture over AudioRecord
                                  on its own reader thread
          MidiByteParser.cs       the MIDI 1.0 byte-stream parser (linked into
                                  the test project)
      native/<abi>/libcodebrix_miniaudio.so
                                  the BUILT native libraries, committed, packed
                                  as AndroidNativeLibrary items. Rebuilt only by
                                  tools/build-native.sh. The csproj refuses to
                                  build without them.
  native/                         the native library's sources and provenance
      CMakeLists.txt              one target, codebrix_miniaudio; the
                                  CODEBRIX_CODECS_ONLY option builds the codec
                                  half alone for the host tests
      bridge.cpp                  the CodeBrix C ABI over Oboe (Apache-2.0,
                                  ours)
      codecs.c                    the sf_* codec export layer over miniaudio +
                                  stb_vorbis with device IO compiled out (ours)
      vendor/oboe/                Oboe, vendored (see PROVENANCE)
      vendor/miniaudio-<sha>/     miniaudio.h, vendored
      vendor/stb_vorbis-<sha>/    stb_vorbis.c, vendored
      vendor/LICENSE-MiniAudio.txt
      patches/                    the two local patches, already applied
      PROVENANCE.txt              what came from where and what was patched
      UPSTREAM-SHA256.txt         hashes of the ORIGINAL vendored files
      VENDOR-SHA256.txt           hashes of the CURRENT (patched) files - what
                                  tools/verify-vendor.py enforces
      BUILD-PROVENANCE.txt        toolchain + hashes of the shipped binaries,
                                  written by tools/verify-native.py
  tests/CodeBrix.Audio.Android.Tests/   the xUnit v3 HOST test project
      runtimes/linux-x64/native/libcodebrix_miniaudio.so
                                  the COMMITTED host build of the codec half
                                  of native/, for the codec tests (see
                                  TESTING); refreshed by tools/test-host.sh
  tests/Assets/                   audio fixtures + PROVENANCE.txt
  samples/AudioDiagnostics/       the physical-device diagnostics app
  tools/                          build / test / verify scripts (EXTRAS-README)
  artifacts/                      build output, git-ignored; nothing the
                                  solution needs lives here
  CodeBrix.Audio.Android.slnx     the solution. Its Solution Items folder
                                  carries .gitignore, AGENT-README.txt,
                                  EXTRAS-README.txt, global.json,
                                  icon-codebrix-128.png, LICENSE,
                                  MAINTAINER-README.txt, README-INDEX.txt,
                                  README.md and THIRD-PARTY-NOTICES.txt; the
                                  Tests folder carries the test project and
                                  the Samples folder the diagnostics app
  global.json                     selects the Microsoft.Testing.Platform test
                                  runner; pins NO SDK version (see TESTING)
  THIRD-PARTY-NOTICES.txt         the authoritative provenance record; packed

There is no Directory.Build.props: the version block lives in the library
csproj like every single-package family repo. There is no InternalsVisibleTo.cs
because the test project cannot reference the Android-targeted library at all
(see TESTING); the one internal type it tests is compiled in as a linked file.
No csproj in the repository sets Nullable or ImplicitUsings.


BUILDING
========
Managed build (needs the .NET 10 Android workload, an Android SDK with the
API 36 platform, and a JDK the workload accepts):

    dotnet build src/CodeBrix.Audio.Android/CodeBrix.Audio.Android.csproj \
        -c Release

Both csproj files that target Android default AndroidSdkDirectory to
$HOME/Android/Sdk and JavaSdkDirectory to the Debian JDK 21 path WHEN THOSE
EXIST and nothing else set them - the system default Java on a Debian box may
be newer than the workload accepts, which is why the JDK is selected
explicitly. Elsewhere pass -p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...
or set ANDROID_HOME / JAVA_HOME. GeneratePackageOnBuild is ON, so a Release
build also writes the .nupkg to src/CodeBrix.Audio.Android/bin/Release/.

The solution builds the library, the tests and the sample together:

    dotnet build CodeBrix.Audio.Android.slnx -c Release

but note the sample consumes the PACKAGE (see SAMPLES) and needs a version it
can restore, and the tests need the host native library (see TESTING). The
per-project commands above and below are what day-to-day work uses.

Native build (needs the Android NDK - the release build-native.sh defaults to
is recorded in native/BUILD-PROVENANCE.txt - plus CMake and Python 3):

    bash tools/build-native.sh

It verifies the vendored sources against native/VENDOR-SHA256.txt, builds
arm64-v8a and x86_64 in Release with the static C++ runtime and 16 KB page
alignment, strips the two .so files into src/CodeBrix.Audio.Android/native/,
and runs tools/verify-native.py, which checks architecture, all required ABI
exports, that NO device-IO export leaked into the codec half, the dynamic
dependencies and the ELF alignment, and rewrites native/BUILD-PROVENANCE.txt.
Finds the NDK under $ANDROID_SDK_ROOT, $ANDROID_HOME or $HOME/Android/Sdk, or
at $CODEBRIX_ANDROID_NDK. Nothing is downloaded and nothing is installed: put
the NDK there yourself.

The committed binaries are the ones the package ships. Rebuild them only when
native/ changes, then commit the new .so files together with the updated
BUILD-PROVENANCE.txt.

Coordinated builds (Core and this package changing together, before publish):

    bash tools/build-local.sh <fresh-date-stamped-version>

Builds the sibling CodeBrix.Audio checkout (Core, Desktop, ModestSynth), then
CodeBrix.Audio.Opus, then the native libraries and this package, ALL at the one
version you name, into a local feed (default
../CodeBrix.Audio/artifacts/android-port/feed; override the sibling paths with
CODEBRIX_AUDIO_REPO / CODEBRIX_OPUS_REPO and the feed with CODEBRIX_LOCAL_FEED).
It refuses to start until the Core pin in the Opus library csproj and in this
repository's library and test csproj files already reads that version - raise
them first. Choose a version no package in NuGet's global cache already has,
because a cached package with the same version but different contents will be
used silently. Afterwards run tools/verify-package.py <feed> <version>. Set the
pins back to a PUBLISHED Core version before releasing anything.


TESTING
=======
The tests are HOST tests: they run on the development machine, not on Android.

    dotnet test -c Release --project \
        tests/CodeBrix.Audio.Android.Tests/CodeBrix.Audio.Android.Tests.csproj

That works from a fresh clone on any platform, because everything the tests
need is committed. The codec tests exercise a HOST build of the codec half of
native/ (CODEBRIX_CODECS_ONLY=ON: codecs.c alone, no Oboe, no NDK), and that
build is committed under the test project at

    tests/CodeBrix.Audio.Android.Tests/runtimes/linux-x64/native/libcodebrix_miniaudio.so

which the csproj copies into the output under runtimes/<rid>/native/, the
layout Core's DllImportResolver probes. It is LINUX x64 ONLY, by decision: a
codebrix_miniaudio.dll or .dylib would exist only to run these tests on Windows
or macOS, and that is not worth a second and third native build. So the codec
tests call Assert.SkipUnless for Linux x64 and SKIP everywhere else, while the
MIDI parser tests run on every platform. The committed .so was compiled on the
development workstation and carries its glibc floor, which is fine for a test
helper (it is never shipped) but means a much older Linux would fail to load it.

When anything under native/ changes, refresh that committed library and commit
the new file alongside the change - the same rule the family applies to every
committed native binary:

    bash tools/test-host.sh

It builds into the git-ignored artifacts/native/host/, copies the result to the
committed location, and then runs the tests. Extra arguments are passed to
dotnet test (for example a -p:RestoreSources= pointing at a local feed during a
coordinated build). It refuses to run anywhere but Linux x64.

THE TEST RUNNER IS Microsoft.Testing.Platform (MTP), selected by global.json at
the repo root: { "test": { "runner": "Microsoft.Testing.Platform" } }. That is
what xunit.v3 4.x expects; without it `dotnet test` falls back to the VSTest
bridge, which fails on the .NET 10 SDK. The file pins no SDK version. Keep it.

Why the test project looks the way it does:
  - It targets net10.0. The library targets net10.0-android36.1, and a desktop
    project cannot ProjectReference an Android-targeted one, so there is no
    project reference and no InternalsVisibleTo. The one platform-independent
    library source it tests, Internal/MidiByteParser.cs, is compiled in as a
    linked file.
  - It references the pinned Core package directly, because the codec tests go
    through Core's MiniAudioCodecFactory against the host native library.
    Core ships no native code of its own, so nothing collides with the
    committed host build under runtimes/.
  - Test dependencies are Microsoft.NET.Test.Sdk, xunit.v3,
    xunit.runner.visualstudio and SilverAssertions.ApacheLicenseForever. No
    coverage collector is referenced.

What the tests cover: WAV / MP3 / FLAC / Ogg Vorbis decoding, rate conversion,
seek to start and mid-file (the seek-reset patch), that the codec-only binary
exports sf_has_vorbis and no device-IO symbols, WAV encoding, and the MIDI byte
parser. What they do NOT cover: anything that needs an Android device - Oboe
streams, routing, focus, capture, MIDI hardware. That is DEVICE VALIDATION.

Fixtures under tests/Assets/ are synthetic tones copied from the sibling
CodeBrix.Audio and CodeBrix.Audio.Opus repositories, where their
tools/make_test_fixtures scripts generate them (tests/Assets/PROVENANCE.txt).
Never add a third-party recording.

The sibling repositories' own suites (CodeBrix.Audio's Core / Engine /
ModestSynth tests and CodeBrix.Audio.Opus's) are the regression net for the
shared seam. After a coordinated Core change, run them against the local feed
before publishing Core.


SAMPLES
=======
samples/AudioDiagnostics/ is the physical-device diagnostics application;
EXTRAS-README.txt describes what it exercises and how to build it. Two
maintenance rules:

  - It consumes CodeBrix.Audio.Android.ApacheLicenseForever as a NUGET PACKAGE,
    never as a project reference, so that what it verifies is the packed
    artifact: the AAR with both ABIs, the transitive Core dependency and the
    add-on packages. Its AndroidAudioVersion property names the version;
    until a version is on nuget.org, pass -p:AndroidAudioVersion=<packed> and
    a -p:RestoreSources= that includes the folder or feed holding it.
  - Once the package IS published, set the csproj's AndroidAudioVersion
    default to a published version so the sample restores from nuget.org
    alone. Keep it pointing at a published version from then on.


PACKAGING AND PUBLISHING
========================
  PackageId              CodeBrix.Audio.Android.ApacheLicenseForever
  License expression     Apache-2.0, with PackageRequireLicenseAcceptance set
  GeneratePackageOnBuild true - every Release build writes a fresh .nupkg
  Dependency             CodeBrix.Audio.Core.MitLicenseForever, pinned by a
                         PackageReference version in the library csproj. THAT
                         PIN IS THE ONE PLACE the Core version belongs; the
                         test csproj mirrors it (raise both together) and it
                         never appears in AGENT-README.txt or README.md.
  Packed alongside the assembly and its XML docs:
      icon-codebrix-128.png     the package icon
      README.md                 the nuget.org / GitHub landing page
      AGENT-README.txt          the consumer guide - keep it consumer-only
      THIRD-PARTY-NOTICES.txt   required by the vendored native sources
      the AAR                   the two native libraries, produced by the
                                Android SDK from the AndroidNativeLibrary items
  MAINTAINER-README.txt, EXTRAS-README.txt, README-INDEX.txt, native/ and
  LICENSE are NOT packed (the licence is expressed by PackageLicenseExpression).

VERSIONING. The csproj computes the version from the UTC clock at build time:
1.<years since 2026>.<day of year>.<minute of day> - the family's canonical
date-stamped block, pasted verbatim. It is strictly increasing over time and is
NOT SemVer: major is pinned to 1 and minor encodes the year, so major and minor
say nothing about API compatibility. Two builds in the same UTC minute produce
the SAME version, so never publish two packages from within one minute. The
one local wrinkle: BuildVersion is only computed when it was not supplied, so a
-p:BuildVersion=... on the command line wins - that is how tools/build-local.sh
stamps every package of a coordinated build with one number, and it is the same
condition CodeBrix.Audio.Opus's csproj carries.

PUBLISH CHECKLIST (Jeremy publishes; nothing here pushes to nuget.org):
  1. The Core pin in the library csproj names a version that is ON NUGET.ORG.
  2. native/ unchanged since the committed binaries were built - or rebuilt
     with tools/build-native.sh and BUILD-PROVENANCE.txt updated and committed.
  3. `bash tools/test-host.sh` passes, and the host library it refreshed is
     committed (it only changes when native/ changed); the sibling suites pass
     against the Core version pinned.
  4. `dotnet build src/CodeBrix.Audio.Android/CodeBrix.Audio.Android.csproj
     -c Release` with 0 warnings / 0 errors. Take the .nupkg from bin/Release.
  5. Push the package; then set samples/AudioDiagnostics's AndroidAudioVersion
     default to that version and confirm the sample builds from nuget.org
     alone, Debug and Release.
  6. Tag the repository at the published version, so the latest git tag and
     the latest nuget.org version agree (the family rule).
  7. Until DEVICE VALIDATION below is complete, say so in the release note:
     host-verified and cross-compiled is not device-verified.


DEVICE VALIDATION
=================
Development needs no attached device. SHIPPING validation needs BOTH an ARM64
and an x64 physical device; never an emulator / AVD on this workstation (its
audio path is not the one real hardware uses, and it proves nothing about
latency, routing, capture or Bluetooth). Listening alone establishes basic
function; lifecycle, capture and sustained synthesis need their own passes.

Record, for every run: device model, ABI, Android / API version, audio route,
package and build version, Debug / Release, duration, the diagnostics counters,
and what was observed. Test API 33 and the newest supported OS the device
matrix offers. Start with the Release diagnostics APK, then compare Debug.

  1. Play through speakers, wired / USB headphones and every Bluetooth route
     the device offers. Check channel order, pitch, clipping, underruns and
     silence on stop.
  2. Exercise WAV, MP3, FLAC, Ogg Vorbis and Opus: mono and stereo, differing
     source rates, long files, seek-to-zero, mid-file seek, pause / resume, end
     of file.
  3. Exercise the SharedAudioOutput surface: overlapping players, shutdown and
     restart, modifiers, mixing, effects, the high-level recorders and file
     writers.
  4. Run ModestSynth at 1, 32 and 128 voices for at least ten minutes after
     warm-up. Record xruns, callback allocations and duration, GC counts,
     latency and thermal behaviour. Repeat with the consuming application's
     real instrument / mixer graph.
  5. Deny, grant and revoke the microphone permission. Record and replay; test
     simultaneous playback and capture and long-running clock drift; confirm
     clean failure on revocation.
  6. Plug and unplug routes during playback and capture; switch devices; repeat
     rapid stop / restart / dispose. Verify automatic recovery, and the
     RecoverDisconnectedStreams = false path.
  7. Background / foreground, rotate and recreate the activity, lose and regain
     focus, interrupt with another audio app and with a call. The sample is
     foreground-only; test the consuming app's foreground service and
     notification separately.
  8. Enumerate USB and Bluetooth MIDI hardware; send and receive notes and
     fragmented SysEx; unplug while open; refresh and reconnect. No stuck notes,
     no leaks.
  9. In an application that provides MediaProjection consent and the
     foreground service, capture eligible playback and stop the projection
     mid-recording. Confirm prohibited sources stay excluded. The sample does
     not supply this UI.
 10. Exercise SF2, SFZ, Decent Sampler, streamed sample libraries, packet /
     network audio and the file writers the consuming applications use.

The sample is a starting point for all of this, not evidence that any of it
has passed.

  Target            Listening   Capture / lifecycle / routing   Sustained load
  Physical ARM64    pending     pending                         pending
  Physical x64      pending     pending                         pending

Update this table as passes are completed, with the date and device. What HAS
been verified without a device - the host tests, the desktop suites' binary-
compatibility runs against the split Core, the native ABI exports, page
alignment and AAR contents - is what the tools/ scripts check; the record of
the initial run is in this repository's history, not kept here where it would
go stale.


CODING CONVENTIONS
==================
The family conventions apply in full:

  - Nullable reference types are OFF. Reference types are always nullable, so
    the source carries no `?` on a reference type and never uses the
    null-forgiveness `!` operator; value-type nullables (int?, bool?) are
    fine - and DeviceInfo is a STRUCT in the Engine, so the `DeviceInfo?`
    parameters on the device-opening and switching methods are Nullable<T>,
    not annotations, and stay. The Engine abstractions this backend
    implements and the Android bindings it calls are nullable-annotated on
    their side; overriding them without annotations is legal and is the
    family's way. When a binding
    member can return null (Looper.MainLooper, the Android builders' Set...
    methods, GetSystemService), the code either guards with a throw or
    accepts the null at run time - it never asserts it away.
  - No ImplicitUsings and no global usings. Every file lists its usings at the
    top: System.* first, then other namespaces, alphabetical within each
    group, aliases last.
  - AllowUnsafeBlocks is ON: the render callback is an unmanaged function
    pointer and the sample buffers are spans over native memory.
  - File-scoped namespaces only; <GenerateDocumentationFile> is ON and every
    public / protected member carries an XML doc comment - never suppress
    CS1591, fix it at source; no <NoWarn> anywhere; no fabricated top-of-file
    banners on new-in-family files (the only header in the repo is on
    native/bridge.cpp, where a licence line is conventional for C++).
  - Real-time rules, enforced by the code and to be preserved by any change:
    nothing inside a render or capture callback blocks, allocates, throws
    across the native boundary or calls a control operation. OboeStream's
    control-thread guard exists so violations fail loudly.
  - Tests use xUnit v3 + SilverAssertions, are named <Class>Tests.cs, use
    snake_case method names (with the member name first where a specific
    member is exercised), and mark //Arrange //Act //Assert in the body.
  - Public APIs stay documented and stable: desktop consumers and the add-ons
    depend on Core, and Android applications depend on this surface.


PROVENANCE AND VENDORED SOURCES
===============================
THIRD-PARTY-NOTICES.txt is the authoritative record, with the licence texts;
native/PROVENANCE.txt is the working summary. In short:

  - native/vendor/oboe/       Oboe (Google / AOSP, Apache-2.0), vendored at a
                              pinned release commit. Compiled STATICALLY into
                              libcodebrix_miniaudio.so; no Oboe symbol is
                              exported. ONE local patch,
                              patches/oboe-ndk30.patch, adapts AAudioLoader.h
                              / .cpp to the released NDK's own headers; each
                              changed passage is marked "// CodeBrix:" in the
                              file, which is what Apache-2.0 4(b) asks for.
  - native/vendor/miniaudio-<sha>/miniaudio.h   miniaudio (public domain /
                              MIT-0), copied from CodeBrix.Audio's vendored
                              copy. Built with device IO, engine, resource
                              manager, node graph and generation compiled OUT
                              - on Android only its decoders and WAV encoder
                              are used. ONE local patch,
                              patches/miniaudio-seek-reset.patch: after a
                              seek the decoder discards read-ahead and resets
                              conversion state, and the biquad clears history
                              for every channel. The host tests cover both.
                              These fixes are Android-only; CodeBrix.Audio's
                              desktop binaries are untouched by them.
  - native/vendor/stb_vorbis-<sha>/stb_vorbis.c   stb_vorbis (public domain /
                              MIT), copied from CodeBrix.Audio's vendored
                              copy. UNMODIFIED.
  - native/bridge.cpp, native/codecs.c, native/CMakeLists.txt   ours.

The manifests make this enforceable: UPSTREAM-SHA256.txt hashes the originals,
VENDOR-SHA256.txt hashes the current (patched) files, and
tools/verify-vendor.py fails the native build on any mismatch or any added /
missing file under native/vendor/. To change a vendored file: edit it, update
the patch under native/patches/ (paths relative to native/), regenerate the
VENDOR-SHA256.txt line, describe the change in PROVENANCE.txt and in
THIRD-PARTY-NOTICES.txt, mark the changed passage in-file, rebuild with
tools/build-native.sh, and commit the binaries with BUILD-PROVENANCE.txt.

This is ONE-AND-DONE vendoring in the family's usual sense: nothing tracks
upstream automatically. Re-vendoring a newer Oboe or miniaudio is a deliberate
task: refresh the folder, re-apply or retire the patches, regenerate BOTH
manifests, and re-verify on devices.

The desktop native binaries live in the CodeBrix.Audio repository and are never
built, changed or shipped from here.


NOTES
=====
  - NEVER COMMIT OR PUSH from an agent session. Leave changes in the working
    tree for Jeremy to review and commit.
  - CodeBrix.Android (the sibling repository) is READ-ONLY from here: do not
    edit or build it.
  - NO EMULATOR / AVD on this workstation. Run-time validation is physical
    devices only; nothing done without a device may be described as
    device-verified.
  - Preserve desktop consumer compatibility and the shared assembly identities
    (CodeBrix.Audio.dll, CodeBrix.Audio.Engine.dll belong to Core).
  - Keep the Core dependency a PINNED NUGET PACKAGE, never a project reference
    into the sibling checkout.
  - Every native upstream revision, licence and local patch is recorded (see
    PROVENANCE); every vendored file keeps its licence header.
  - THE CONTROL-THREAD GUARD IS LOAD-BEARING. OboeStream tracks "inside a
    callback" in a thread-static and every control operation asserts on it;
    playback capture sets the same flag around its subscriber call. Removing
    it turns a clear InvalidOperationException into a deadlock or a torn
    stream.
  - THE WEAK GC HANDLE in OboeStream.Open is deliberate: the native stream
    holds only a weak reference to its managed owner, so a forgotten device
    can still be collected, and StreamHandle's release closes the native
    stream BEFORE freeing the handle. Do not "strengthen" it.
  - RECOVERY KEEPS THE OLD HANDLE until the replacement has started, so a
    failed reopen can be retried on the next service tick with its
    diagnostics intact. Do not dispose the old handle first.
  - Oboe's error callback returns TRUE on purpose: the control thread owns
    stop / close / reopen, and returning true stops Oboe's automatic close
    from racing it.
  - The MIDI backend's OpenDevice completion has a five-second timeout and
    closes a device that arrives late; MidiByteParser caps SysEx at 1 MiB.
    Both limits are documented in AGENT-README.txt; change them there too.
  - AndroidPackagedAssets STAMPS an extracted copy with the application's
    version code AND LastUpdateTime, so a developer redeploy with the same
    version code still re-extracts. It writes under a ".codebrix-partial" name
    and renames into place, so a crash mid-copy can never leave a copy that
    passes as complete.
  - PENDING WIRING INTO CORE. CodeBrix.Audio (the Core repository) gains a
    locator seam, CodeBrix.Audio.Instruments.PackagedAssets, through which
    instrument packages find the files they ship. When a Core carrying it is
    PUBLISHED and this repository's Core pin is raised to it, add ONE line to
    CodeBrixAndroidAudio.Initialize - register an IPackagedAssetLocator whose
    Locate calls AndroidPackagedAssets.Materialize and whose Exists calls
    IsMaterialized - so instrument packages find their extracted asset with
    no application code. Until then this package compiles against the
    published Core and must not reference that seam; applications hand the
    returned path to the library (FluidR3GmInstrumentLibrary.UseSoundFontAt
    and the like), which keeps working afterwards as the explicit override.
  - SupportedDataFormats is EMPTY for every DeviceInfo on purpose: Android's
    advertised route formats do not describe what an Oboe stream can
    negotiate, and reporting them would mislead callers into probing.
  - Toolchain the committed binaries were built with: recorded in
    native/BUILD-PROVENANCE.txt (NDK release, API level, configuration). The
    default NDK path in tools/build-native.sh names that release; when the
    NDK is upgraded, change the default, rebuild, re-verify the Oboe patch
    against the new headers, and update BUILD-PROVENANCE.txt.
  - Future platform targets (a newer .NET / Android API pairing, or new
    device classes) are separate, deliberate upgrades - nothing here claims
    them.
================================================================================
