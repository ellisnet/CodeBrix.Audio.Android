# Repository instructions

- Never create a Git commit or run git push. Leave changes for Jeremy to review.
- Read README.md and DEVELOPMENT.md before changing the backend or packaging.
- CodeBrix.Android is read-only; do not edit or build that sibling repository.
- Do not run an Android emulator/AVD. Runtime validation uses physical devices.
- Preserve desktop consumer compatibility and existing shared assembly identities.
- Keep the platform dependency on a pinned Core NuGet package, not a sibling project reference.
- Document public APIs. Keep real-time callbacks bounded and free of blocking operations.
- Record native upstream revisions, licenses and local patches; retain license headers.
- Do not claim physical-device validation from cross-compilation or host tests.
