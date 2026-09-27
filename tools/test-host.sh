#!/usr/bin/env bash
# Rebuilds the committed host codec library for THIS machine's platform and runs the tests.
# The build lands in the git-ignored artifacts/ tree; the finished library is copied to
# tests/CodeBrix.Audio.Android.Tests/runtimes/<rid>/native/, which IS committed - after a change
# under native/, commit the refreshed .so together with it. Only linux-x64 is produced today.
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) rid=linux-x64 ;;
    *) echo "tools/test-host.sh builds the host codec library for linux-x64 only (this is $(uname -s)-$(uname -m))." >&2; exit 1 ;;
esac
cmake -S "$repo/native" -B "$repo/artifacts/native/host" -DCODEBRIX_CODECS_ONLY=ON -DCMAKE_BUILD_TYPE=Release
cmake --build "$repo/artifacts/native/host" --parallel 4
destination="$repo/tests/CodeBrix.Audio.Android.Tests/runtimes/$rid/native"
mkdir -p "$destination"
cp "$repo/artifacts/native/host/libcodebrix_miniaudio.so" "$destination/"
echo "Host codec library refreshed at tests/CodeBrix.Audio.Android.Tests/runtimes/$rid/native/ (commit it if native/ changed)."
dotnet test --project "$repo/tests/CodeBrix.Audio.Android.Tests/CodeBrix.Audio.Android.Tests.csproj" -c Release "$@"
