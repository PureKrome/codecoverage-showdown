#!/usr/bin/env bash
# coverlet.sh
# Usage: coverlet.sh <humanizer-dir> [coverlet-mtp-version] [output-xml-path]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "$SCRIPT_DIR/common.sh"

HUMANIZER_DIR="$(realpath "${1:?missing humanizer dir}")"

disable_nerdbank "$HUMANIZER_DIR"
ensure_dotnet_sdk "$HUMANIZER_DIR"

# cd into the Humanizer repo so its global.json takes precedence over ours
cd "$HUMANIZER_DIR"

TFM="$(resolve_tfm "$HUMANIZER_DIR")"
echo "==> [Coverlet] Target framework : $TFM"

if [ -z "${2:-}" ]; then
  echo "==> [Coverlet] No version provided. Resolving latest coverlet.MTP from NuGet..."
  COVERLET_VER="$(resolve_latest "coverlet.mtp")"
  if [ -z "$COVERLET_VER" ]; then
    echo "ERROR: Could not resolve latest coverlet.MTP version from NuGet" >&2
    exit 1
  fi
  echo "==> [Coverlet] No coverlet NuGet version provided. Using most recent version which is \"${COVERLET_VER}\""
else
  COVERLET_VER="${2}"
fi

OUTPUT_XML="${3:-$SCRIPT_DIR/../results/coverlet.cobertura.xml}"
OUTPUT_XML="$(realpath -m "$OUTPUT_XML")"

TEST_PROJECT="$(find "$HUMANIZER_DIR/tests" -name "Humanizer.Tests.csproj" | head -1)"
if [ -z "$TEST_PROJECT" ]; then
  TEST_PROJECT="$(find "$HUMANIZER_DIR" -name "Humanizer.Tests.csproj" | head -1)"
fi
if [ -z "$TEST_PROJECT" ]; then
  echo "ERROR: Could not find Humanizer.Tests.csproj" >&2
  exit 1
fi
TEST_DIR="$(dirname "$TEST_PROJECT")"

echo "==> [Coverlet] Test project : $TEST_PROJECT"
echo "==> [Coverlet] Package ver  : $COVERLET_VER"
echo "==> [Coverlet] Output       : $OUTPUT_XML"

dotnet add "$TEST_PROJECT" package coverlet.MTP --version "$COVERLET_VER"
dotnet remove "$TEST_PROJECT" package coverlet.collector 2>/dev/null || true
dotnet remove "$TEST_PROJECT" package Microsoft.Testing.Extensions.CodeCoverage 2>/dev/null || true
dotnet restore "$TEST_PROJECT"

# Coverlet command line options: https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/Coverlet.MTP.Integration.md#command-line-options
dotnet run \
  --project "$TEST_PROJECT" \
  --framework "$TFM" \
  --configuration Debug \
  -- \
  --coverlet \
  --coverlet-output-format cobertura \
  --coverlet-file-prefix coverlet \
  --coverlet-include "[Humanizer]*"

# Search the entire humanizer dir for any cobertura xml produced after this run
# The filename includes a timestamp so we match on the extension pattern
GENERATED="$(find "$HUMANIZER_DIR" -name "*.cobertura.*.xml" -newer "$TEST_PROJECT" 2>/dev/null | head -1)"
if [ -z "$GENERATED" ]; then
  # fallback: find any cobertura xml anywhere under humanizer
  GENERATED="$(find "$HUMANIZER_DIR" -name "*.cobertura*.xml" 2>/dev/null | sort | tail -1)"
fi
if [ -z "$GENERATED" ]; then
  echo "ERROR: Coverlet did not produce a cobertura XML file" >&2
  exit 1
fi

echo "==> [Coverlet] Found output: $GENERATED"
mkdir -p "$(dirname "$OUTPUT_XML")"
cp "$GENERATED" "$OUTPUT_XML"
echo "==> [Coverlet] Output written: $OUTPUT_XML"