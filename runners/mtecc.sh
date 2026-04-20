#!/usr/bin/env bash
# run-mtecc.sh
# Usage: run-mtecc.sh <humanizer-dir> [mtecc-codecoverage-version] [output-xml-path]

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
echo "==> [MTECC] Target framework : $TFM"

if [ -z "${2:-}" ]; then
  echo "==> [MTECC] No version provided. Resolving latest Microsoft.Testing.Extensions.CodeCoverage from NuGet..."
  MTECC_VER="$(resolve_latest "microsoft.testing.extensions.codecoverage")"
  if [ -z "$MTECC_VER" ]; then
    echo "ERROR: Could not resolve latest Microsoft.Testing.Extensions.CodeCoverage version from NuGet" >&2
    exit 1
  fi
  echo "==> [MTECC] No MTECC NuGet version provided. Using most recent version which is \"${MTECC_VER}\""
else
  MTECC_VER="${2}"
fi

OUTPUT_XML="${3:-$SCRIPT_DIR/../results/mtecc.cobertura.xml}"
OUTPUT_XML="$(realpath -m "$OUTPUT_XML")"

TEST_PROJECT="$(find "$HUMANIZER_DIR/tests" -name "Humanizer.Tests.csproj" | head -1)"
if [ -z "$TEST_PROJECT" ]; then
  TEST_PROJECT="$(find "$HUMANIZER_DIR" -name "Humanizer.Tests.csproj" | head -1)"
fi
if [ -z "$TEST_PROJECT" ]; then
  echo "ERROR: Could not find Humanizer.Tests.csproj" >&2
  exit 1
fi
echo "==> [MTECC] Test project : $TEST_PROJECT"
echo "==> [MTECC] Package ver  : $MTECC_VER"
echo "==> [MTECC] Output       : $OUTPUT_XML"

echo "==> [MTECC] NuGet package : Adding Microsoft.Testing.Extensions.CodeCoverage $MTECC_VER"
dotnet add "$TEST_PROJECT" package Microsoft.Testing.Extensions.CodeCoverage --version "$MTECC_VER"

echo "==> [MTECC] NuGet package : Removing coverlet.MTP if it exists to avoid conflicts"
dotnet remove "$TEST_PROJECT" package coverlet.MTP 2>/dev/null || true

echo "==> [MTECC] Restoring packages..."
dotnet restore "$TEST_PROJECT"

COVERAGE_OUTPUT="mtecc-coverage.cobertura.xml"

echo "==> [MTECC] Running tests with Microsoft.Testing.Extensions.CodeCoverage..."
dotnet run \
  --project "$TEST_PROJECT" \
  --framework "$TFM" \
  --configuration Debug \
  -- \
  --coverage \
  --coverage-output-format cobertura \
  --coverage-output "$COVERAGE_OUTPUT"

echo "==> [MTECC] Test run completed. Looking for generated coverage file..."
GENERATED="$(find "$HUMANIZER_DIR" -name "$COVERAGE_OUTPUT" 2>/dev/null | head -1)"
if [ -z "$GENERATED" ]; then
  echo "ERROR: MTECC did not produce a cobertura XML file" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUTPUT_XML")"
cp "$GENERATED" "$OUTPUT_XML"
echo "==> [MTECC] Output written: $OUTPUT_XML"
