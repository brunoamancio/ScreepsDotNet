#!/bin/bash
set -e

echo "=== Screeps Parity Harness: Repository Setup ==="

# Determine script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

# Load version configuration
VERSIONS_FILE="versions.json"
if [ ! -f "$VERSIONS_FILE" ]; then
  echo "Error: versions.json not found"
  exit 1
fi

# Check if jq is available (for JSON parsing)
if ! command -v jq &> /dev/null; then
  echo "Warning: jq not found. Using default versions (master branches)."
  PINNING_ENABLED="false"
else
  PINNING_ENABLED=$(jq -r '.pinningEnabled' "$VERSIONS_FILE")
fi

# Determine refs to clone
if [ "$PINNING_ENABLED" = "true" ] && command -v jq &> /dev/null; then
  ENGINE_REF=$(jq -r '.pins.engine' "$VERSIONS_FILE")
  DRIVER_REF=$(jq -r '.pins.driver' "$VERSIONS_FILE")
  COMMON_REF=$(jq -r '.pins.common' "$VERSIONS_FILE")
  echo "Using pinned versions: engine=$ENGINE_REF, driver=$DRIVER_REF, common=$COMMON_REF"
else
  ENGINE_REF="master"
  DRIVER_REF="master"
  COMMON_REF="master"
  echo "Using latest versions from master branches"
fi

MODULES_DIR="screeps-modules"
mkdir -p "$MODULES_DIR"

# Clone or update engine repository
echo ""
echo "--- Engine Repository ---"
if [ ! -d "$MODULES_DIR/engine" ]; then
  echo "Cloning screeps/engine..."
  git clone https://github.com/screeps/engine.git "$MODULES_DIR/engine"
  cd "$MODULES_DIR/engine"
  if [ "$ENGINE_REF" != "master" ]; then
    git checkout "$ENGINE_REF"
  fi
  cd ../..
else
  echo "Updating screeps/engine..."
  cd "$MODULES_DIR/engine"
  git fetch origin
  git checkout "$ENGINE_REF"
  if [ "$ENGINE_REF" = "master" ]; then
    git pull origin master
  fi
  cd ../..
fi

# Clone or update driver repository
echo ""
echo "--- Driver Repository ---"
if [ ! -d "$MODULES_DIR/driver" ]; then
  echo "Cloning screeps/driver..."
  git clone https://github.com/screeps/driver.git "$MODULES_DIR/driver"
  cd "$MODULES_DIR/driver"
  if [ "$DRIVER_REF" != "master" ]; then
    git checkout "$DRIVER_REF"
  fi
  cd ../..
else
  echo "Updating screeps/driver..."
  cd "$MODULES_DIR/driver"
  git fetch origin
  git checkout "$DRIVER_REF"
  if [ "$DRIVER_REF" = "master" ]; then
    git pull origin master
  fi
  cd ../..
fi

# Clone or update common repository
echo ""
echo "--- Common Repository ---"
if [ ! -d "$MODULES_DIR/common" ]; then
  echo "Cloning screeps/common..."
  git clone https://github.com/screeps/common.git "$MODULES_DIR/common"
  cd "$MODULES_DIR/common"
  if [ "$COMMON_REF" != "master" ]; then
    git checkout "$COMMON_REF"
  fi
  cd ../..
else
  echo "Updating screeps/common..."
  cd "$MODULES_DIR/common"
  git fetch origin
  git checkout "$COMMON_REF"
  if [ "$COMMON_REF" = "master" ]; then
    git pull origin master
  fi
  cd ../..
fi

# Install dependencies
echo ""
echo "--- Installing Dependencies ---"
echo "Installing engine dependencies..."
cd "$MODULES_DIR/engine" && npm install && cd ../..

echo "Installing driver dependencies..."
cd "$MODULES_DIR/driver" && npm install && cd ../..

echo "Installing common dependencies..."
cd "$MODULES_DIR/common" && npm install && cd ../..

echo ""
echo "=== Setup Complete ==="
echo "Official Screeps modules ready for parity testing."
echo "Modules location: $(pwd)/$MODULES_DIR"
