#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_ROOT="${SCRIPT_DIR}/build"

usage() {
    cat <<'EOF'
Usage: ./build.sh <rid> [config]

Examples:
  ./build.sh linux-x64
  ./build.sh osx-arm64 Release

The script configures CMake for the provided runtime identifier (RID),
builds the native library, and copies the output to
ScreepsDotNet.Driver/runtimes/<rid>/native/.
EOF
}

if [[ $# -lt 1 ]]; then
    usage
    exit 1
fi

RID="$1"
CONFIG="${2:-Release}"

BUILD_DIR="${BUILD_ROOT}/${RID}-${CONFIG}"

GENERATOR_ARGS=()
CMAKE_ARGS=(
    "-DRUNTIME_IDENTIFIER=${RID}"
    "-DCMAKE_BUILD_TYPE=${CONFIG}"
)

case "${RID}" in
    linux-arm64)
        CMAKE_ARGS+=(
            "-DCMAKE_SYSTEM_NAME=Linux"
            "-DCMAKE_SYSTEM_PROCESSOR=aarch64"
            "-DCMAKE_C_COMPILER=aarch64-linux-gnu-gcc"
            "-DCMAKE_CXX_COMPILER=aarch64-linux-gnu-g++"
        )
        ;;
    win-x64)
        if [[ "$OS" == "Windows_NT" ]]; then
            GENERATOR_ARGS+=("-G" "Visual Studio 17 2022" "-A" "x64")
        fi
        ;;
    win-arm64)
        if [[ "$OS" != "Windows_NT" ]]; then
            echo "RID '${RID}' must be built on Windows." >&2
            exit 1
        fi
        GENERATOR_ARGS+=("-G" "Visual Studio 17 2022" "-A" "ARM64")
        ;;
    osx-x64)
        CMAKE_ARGS+=("-DCMAKE_OSX_ARCHITECTURES=x86_64")
        ;;
    osx-arm64)
        CMAKE_ARGS+=("-DCMAKE_OSX_ARCHITECTURES=arm64")
        ;;
esac

cmake -S "${SCRIPT_DIR}" -B "${BUILD_DIR}" \
    "${GENERATOR_ARGS[@]}" \
    "${CMAKE_ARGS[@]}"

cmake --build "${BUILD_DIR}" --config "${CONFIG}"
