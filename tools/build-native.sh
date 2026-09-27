#!/usr/bin/env bash
set -euo pipefail
repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
sdk=${ANDROID_SDK_ROOT:-${ANDROID_HOME:-$HOME/Android/Sdk}}
ndk=${CODEBRIX_ANDROID_NDK:-$sdk/ndk/30.0.16248370}
test -f "$ndk/build/cmake/android.toolchain.cmake" || { echo "NDK missing: $ndk" >&2; exit 1; }
python3 "$repo/tools/verify-vendor.py"
for abi in arm64-v8a x86_64; do
    build="$repo/artifacts/native/$abi"
    cmake -S "$repo/native" -B "$build" -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_TOOLCHAIN_FILE="$ndk/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI="$abi" -DANDROID_PLATFORM=android-33 -DANDROID_STL=c++_static
    cmake --build "$build" --parallel 4
    destination="$repo/src/CodeBrix.Audio.Android/native/$abi"
    mkdir -p "$destination"
    cp "$build/libcodebrix_miniaudio.so" "$destination/"
    "$ndk/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip" --strip-unneeded "$destination/libcodebrix_miniaudio.so"
done
python3 "$repo/tools/verify-native.py" "$ndk"
