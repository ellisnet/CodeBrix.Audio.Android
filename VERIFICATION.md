# Initial development verification

Verified on the Linux x64 development workstation with local, unpublished package
version **1.0.269.1101**. No physical Android device or emulator was used.

| Check | Result |
| --- | --- |
| Audio/Engine/ModestSynth Release tests | 5,157 passed, 104 skipped, 0 failed |
| Opus Release tests | 137 passed, 7 skipped, 0 failed |
| Android codec/MIDI host tests | 9 passed, 0 skipped, 0 failed |
| Managed API comparison with pre-split assemblies | Passed, including parameter names |
| Pre-split compiled desktop consumer using new Core DLLs | Passed without recompiling the consumer |
| Same consumer source with only its existing Desktop package reference | Built and ran successfully |
| Package dependency graph and managed assembly ownership | Passed for all five packages |
| Desktop native binaries and adjacent licenses | All 14 files byte-for-byte identical to baseline |
| Android native build | ARM64 and x64, API 33, all 26 required C ABI exports |
| Native dependency and device-I/O isolation check | Passed; no miniaudio device initialization exports |
| Native ELF and APK page alignment | 16 KB checks passed on both ABIs/configurations |
| Packed Android AAR and final APK native payloads | Match the verified native build byte-for-byte |
| Diagnostics application Debug | Built for both ABIs, zero warnings/errors |
| Diagnostics application Release | Built, trimmed and AOT compiled for both ABIs, zero warnings/errors |
| APK manifest | Minimum API 33, target API 36, ARM64 and x64 |
| Vendored source integrity | All 146 files match recorded patched-source hashes |

Total: **5,303 tests passed, 111 skipped, zero failed**. Skips retain the existing
suite's opt-in/fixture/platform gates; they are not passing device tests. The
Core/ModestSynth package-version tests ran, using normal Release package outputs.

The workload was verified as .NET SDK 10.0.401 with Android workload 36.1.69.
Native builds use NDK 30.0.16248370 and CMake 3.31.6; managed Android builds select
JDK 21 and SDK 36.1. An intermediate sandboxed restore could not reach nuget.org's
vulnerability endpoint; the affected restores were repeated successfully with
network access. Existing upstream native compiler warnings do not indicate
managed build failures.

## Artifacts

- Local feed: `../CodeBrix.Audio/artifacts/android-port/feed/` (five package IDs at
  the version above; earlier development versions are also retained there).
- Release diagnostics APK:
  `samples/AudioDiagnostics/bin/Release/net10.0-android36.1/com.codebrix.audio.diagnostics-Signed.apk`
- Debug diagnostics APK: the corresponding `bin/Debug/net10.0-android36.1/` path.
- Native checks and hashes: `native/BUILD-PROVENANCE.txt`.
- APK verification report: `artifacts/final-apk-verification.txt`.
- Pre-split desktop baseline: sibling Audio repository's
  `artifacts/android-port/baseline-feed/CodeBrix.Audio.MitLicenseForever.1.0.269.1.nupkg`.

The original compiled consumer DLL's SHA-256 is
`7aecaaa8f211b897a4ccb54fac2b3df5ee3ba8ff6214dcee53e2b5a2d34f1ed3`.
Its executable and dependency manifest remain unchanged during the binary swap
check; only CodeBrix.Audio.dll and CodeBrix.Audio.Engine.dll are replaced.

## Remaining release validation

Run [DEVICE-VALIDATION.md](DEVICE-VALIDATION.md) on physical ARM64 and x64 devices.
Listening, Android callback behavior, microphone/MediaProjection capture, routing,
focus/lifecycle, MIDI hardware and sustained synthesis performance are **pending**.
The current build checks cannot establish these runtime results. Windows/macOS
runtime smoke tests also remain part of normal desktop release validation; the
regression and binary-compatibility runs here used Linux.

The native codec seek tests uncovered and now cover two Android-only miniaudio
patches: decoder read-ahead/converter reset and per-channel biquad history reset.
Oboe has two released-NDK-30 header compatibility changes. See
`native/PROVENANCE.txt` and `native/patches/`; desktop native binaries were untouched.

All repository changes are uncommitted. No packages were published. The sibling
CodeBrix.Android repository was treated as read-only.
