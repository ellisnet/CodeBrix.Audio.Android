#!/usr/bin/env bash
# Build unpublished packages together without project references across repositories.
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
version=${1:?Usage: bash tools/build-local.sh VERSION}
audio=${CODEBRIX_AUDIO_REPO:-$repo/../CodeBrix.Audio}
opus=${CODEBRIX_OPUS_REPO:-$repo/../CodeBrix.Audio.Opus}
feed=${CODEBRIX_LOCAL_FEED:-$audio/artifacts/android-port/feed}
mkdir -p "$feed"
common=(-c Release -m:1 -nr:false -p:UseSharedCompilation=false "-p:BuildVersion=$version")
sources="-p:RestoreSources=$feed%3Bhttps://api.nuget.org/v3/index.json"
# The Core pin is a literal PackageReference version in each downstream csproj, not a property,
# so a coordinated build needs those pins raised to $version first. Check before building anything.
for project in \
    "$opus/src/CodeBrix.Audio.Opus/CodeBrix.Audio.Opus.csproj" \
    "$repo/src/CodeBrix.Audio.Android/CodeBrix.Audio.Android.csproj" \
    "$repo/tests/CodeBrix.Audio.Android.Tests/CodeBrix.Audio.Android.Tests.csproj"; do
    grep -q "Include=\"CodeBrix.Audio.Core.MitLicenseForever\" Version=\"$version\"" "$project" \
        || { echo "Raise the CodeBrix.Audio.Core.MitLicenseForever pin to $version in $project first." >&2; exit 1; }
done
dotnet build "$audio/CodeBrix.Audio.slnx" "${common[@]}"
cp "$audio/src/CodeBrix.Audio/bin/Release/CodeBrix.Audio.Core.MitLicenseForever.$version.nupkg" "$feed/"
cp "$audio/src/CodeBrix.Audio.Desktop/bin/Release/CodeBrix.Audio.MitLicenseForever.$version.nupkg" "$feed/"
cp "$audio/src/CodeBrix.Audio.ModestSynth/bin/Release/CodeBrix.Audio.ModestSynth.MitLicenseForever.$version.nupkg" "$feed/"
dotnet build "$opus/CodeBrix.Audio.Opus.slnx" "${common[@]}" "$sources"
cp "$opus/src/CodeBrix.Audio.Opus/bin/Release/CodeBrix.Audio.Opus.BsdLicenseForever.$version.nupkg" "$feed/"
bash "$repo/tools/build-native.sh"
dotnet build "$repo/src/CodeBrix.Audio.Android/CodeBrix.Audio.Android.csproj" "${common[@]}" "$sources"
cp "$repo/src/CodeBrix.Audio.Android/bin/Release/CodeBrix.Audio.Android.ApacheLicenseForever.$version.nupkg" "$feed/"
echo "Packages ready in $feed (version $version). No packages were published."
