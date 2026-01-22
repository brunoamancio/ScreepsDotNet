#!/bin/bash
set -e

echo "========================================="
echo "Cloning official Screeps repositories..."
echo "  (for Engine parity harness)"
echo "========================================="

# Get script directory and navigate to parity-harness root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

# Load version configuration
VERSIONS_FILE="versions.json"
if [ ! -f "$VERSIONS_FILE" ]; then
    echo "ERROR: versions.json not found!"
    exit 1
fi

# Check if jq is available for JSON parsing
if command -v jq &> /dev/null; then
    PINNING_ENABLED=$(jq -r '.engine.pinningEnabled' "$VERSIONS_FILE")
    ENGINE_REF=$(jq -r '.engine.pins.engine' "$VERSIONS_FILE")
    DRIVER_REF=$(jq -r '.engine.pins.driver' "$VERSIONS_FILE")
    COMMON_REF=$(jq -r '.engine.pins.common' "$VERSIONS_FILE")
else
    echo "WARNING: jq not installed, using default refs (master)"
    PINNING_ENABLED="false"
    ENGINE_REF="master"
    DRIVER_REF="master"
    COMMON_REF="master"
fi

if [ "$PINNING_ENABLED" = "true" ]; then
    echo "Using pinned versions:"
    echo "  engine: $ENGINE_REF"
    echo "  driver: $DRIVER_REF"
    echo "  common: $COMMON_REF"
else
    ENGINE_REF="master"
    DRIVER_REF="master"
    COMMON_REF="master"
    echo "Using latest versions from master branches"
fi

MODULES_DIR="screeps-modules"
mkdir -p "$MODULES_DIR"

# Function to clone or update a repository
clone_or_update() {
    local name=$1
    local url=$2
    local ref=$3

    echo ""
    echo "Processing $name..."

    if [ -d "$MODULES_DIR/$name/.git" ]; then
        echo "  Repository exists, updating..."
        cd "$MODULES_DIR/$name"
        git fetch origin
        git checkout "$ref"
        if [ "$ref" = "master" ]; then
            git pull origin master
        fi
        cd ../..
    else
        echo "  Cloning repository..."
        git clone "$url" "$MODULES_DIR/$name"
        cd "$MODULES_DIR/$name"
        git checkout "$ref"
        cd ../..
    fi

    echo "  Installing npm dependencies..."
    cd "$MODULES_DIR/$name"
    npm install --legacy-peer-deps
    cd ../..

    echo "  ✓ $name ready"
}

# Clone/update all repositories
clone_or_update "engine" "https://github.com/screeps/engine.git" "$ENGINE_REF"
clone_or_update "driver" "https://github.com/screeps/driver.git" "$DRIVER_REF"
clone_or_update "common" "https://github.com/screeps/common.git" "$COMMON_REF"

echo ""
echo "========================================="
echo "✓ All Screeps modules ready for parity testing"
echo "========================================="
