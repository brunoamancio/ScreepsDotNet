#!/bin/bash
set -e

echo "========================================="
echo "Updating Screeps repository commit pins"
echo "========================================="

# Get script directory and navigate to parity-harness root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

VERSIONS_FILE="versions.json"

# Check if jq is available
if ! command -v jq &> /dev/null; then
    echo "ERROR: jq is required for updating pins"
    echo "Install with: sudo apt install jq (Ubuntu/Debian) or brew install jq (macOS)"
    exit 1
fi

echo ""
echo "Fetching latest commit hashes from GitHub..."
echo ""

# Fetch latest commit hashes
ENGINE_HASH=$(git ls-remote https://github.com/screeps/engine.git HEAD | awk '{print $1}')
DRIVER_HASH=$(git ls-remote https://github.com/screeps/driver.git HEAD | awk '{print $1}')
COMMON_HASH=$(git ls-remote https://github.com/screeps/common.git HEAD | awk '{print $1}')

# Get current hashes
CURRENT_ENGINE=$(jq -r '.engine.pins.engine' "$VERSIONS_FILE")
CURRENT_DRIVER=$(jq -r '.engine.pins.driver' "$VERSIONS_FILE")
CURRENT_COMMON=$(jq -r '.engine.pins.common' "$VERSIONS_FILE")

echo "Current pins:"
echo "  engine: $CURRENT_ENGINE"
echo "  driver: $CURRENT_DRIVER"
echo "  common: $CURRENT_COMMON"
echo ""

echo "Latest commits:"
echo "  engine: $ENGINE_HASH"
echo "  driver: $DRIVER_HASH"
echo "  common: $COMMON_HASH"
echo ""

# Check if there are changes
CHANGES=false
if [ "$ENGINE_HASH" != "$CURRENT_ENGINE" ]; then
    echo "✓ engine has updates"
    CHANGES=true
fi
if [ "$DRIVER_HASH" != "$CURRENT_DRIVER" ]; then
    echo "✓ driver has updates"
    CHANGES=true
fi
if [ "$COMMON_HASH" != "$CURRENT_COMMON" ]; then
    echo "✓ common has updates"
    CHANGES=true
fi

if [ "$CHANGES" = "false" ]; then
    echo "No updates available - already at latest commits"
    exit 0
fi

echo ""
read -p "Update versions.json with latest commits? [y/N] " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted"
    exit 0
fi

# Update versions.json
TEMP_FILE=$(mktemp)
TODAY=$(date +%Y-%m-%d)

jq --arg engine "$ENGINE_HASH" \
   --arg driver "$DRIVER_HASH" \
   --arg common "$COMMON_HASH" \
   --arg date "$TODAY" \
   '.engine.pins.engine = $engine |
    .engine.pins.driver = $driver |
    .engine.pins.common = $common |
    .engine.lastValidated = $date' \
   "$VERSIONS_FILE" > "$TEMP_FILE"

mv "$TEMP_FILE" "$VERSIONS_FILE"

echo ""
echo "========================================="
echo "✓ Updated versions.json"
echo "========================================="
echo ""
echo "Next steps:"
echo "1. Run parity tests: dotnet test --filter Category=Parity"
echo "2. If tests pass, commit versions.json"
echo "3. If divergences found, fix .NET Engine, then commit"
echo ""
echo "Commands:"
echo "  cd ../../.."
echo "  dotnet test --filter Category=Parity"
echo "  git add tools/parity-harness/versions.json"
echo "  git commit -m \"chore: update Screeps repo pins to latest\""
echo ""
