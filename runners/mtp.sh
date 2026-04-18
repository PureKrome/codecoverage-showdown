#!/usr/bin/env bash
# run-mtp.sh
# Usage: run-mtp.sh <humanizer-dir> [mtp-codecoverage-version] [output-xml-path]

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
echo "==> [MTP] Target framework : $TFM"

if [ -z "${2:-}" ]; then
  echo "==> [MTP] No version provided. Resolving latest Microsoft.Testing.Extensions.CodeCoverage from NuGet..."
  MTP_VER="$(resolve_latest "microsoft.testing.extensions.codecoverage")"
  if [ -z "$MTP_VER" ]; then
    echo "ERROR: Could not resolve latest Microsoft.Testing.Extensions.CodeCoverage version from NuGet" >&2
    exit 1
  fi
  echo "==> [MTP] No MTP NuGet version provided. Using most recent version which is \"${MTP_VER}\""
else
  MTP_VER="${2}"
fi

OUTPUT_XML="${3:-$SCRIPT_DIR/../results/mtp.cobertura.xml}"
OUTPUT_XML="$(realpath -m "$OUTPUT_XML")"

TEST_PROJECT="$(find "$HUMANIZER_DIR/tests" -name "Humanizer.Tests.csproj" | head -1)"
if [ -z "$TEST_PROJECT" ]; then
  TEST_PROJECT="$(find "$HUMANIZER_DIR" -name "Humanizer.Tests.csproj" | head -1)"
fi
if [ -z "$TEST_PROJECT" ]; then
  echo "ERROR: Could not find Humanizer.Tests.csproj" >&2
  exit 1
fi
echo "==> [MTP] Test project : $TEST_PROJECT"
echo "==> [MTP] Package ver  : $MTP_VER"
echo "==> [MTP] Output       : $OUTPUT_XML"

echo "==> [MTP] NuGet package : Addings Microsoft.Testing.Extensions.CodeCoverage $MTP_VER"
dotnet add "$TEST_PROJECT" package Microsoft.Testing.Extensions.CodeCoverage --version "$MTP_VER"

echo "==> [MTP] NuGet package : Removing coverlet.MTP if it exists to avoid conflicts"
dotnet remove "$TEST_PROJECT" package coverlet.MTP 2>/dev/null || true

echo "==> [MTP] Restoring packages..."
dotnet restore "$TEST_PROJECT"

COVERAGE_OUTPUT="mtp-coverage.cobertura.xml"

echo "==> [MTP] Running tests with Microsoft.Testing.Extensions.CodeCoverage..."
dotnet run \
  --project "$TEST_PROJECT" \
  --framework "$TFM" \
  --configuration Debug \
  -- \
  --coverage \
  --coverage-output-format cobertura \
  --coverage-output "$COVERAGE_OUTPUT"

echo "==> [MTP] Test run completed. Looking for generated coverage file..."
GENERATED="$(find "$HUMANIZER_DIR" -name "$COVERAGE_OUTPUT" 2>/dev/null | head -1)"
if [ -z "$GENERATED" ]; then
  echo "ERROR: MTP did not produce a cobertura XML file" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUTPUT_XML")"
cp "$GENERATED" "$OUTPUT_XML"
echo "==> [MTP] Output written: $OUTPUT_XML"