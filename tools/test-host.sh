#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
cmake -S "$repo/native" -B "$repo/artifacts/native/host" -DCODEBRIX_CODECS_ONLY=ON -DCMAKE_BUILD_TYPE=Release
cmake --build "$repo/artifacts/native/host" --parallel 4
dotnet test --project "$repo/tests/CodeBrix.Audio.Android.HostTests/CodeBrix.Audio.Android.HostTests.csproj" -c Release "$@"
