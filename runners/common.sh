#!/usr/bin/env bash
# common.sh - sourced by run-coverlet.sh and run-mtp.sh

resolve_latest() {
  local package_id="${1}"
  curl -s "https://api.nuget.org/v3-flatcontainer/${package_id}/index.json" \
    | tr ',' '\n' \
    | grep -oP '[0-9]+\.[0-9]+\.[0-9]+' \
    | tail -1
}

# Derives the TFM to pass to --framework from the sdk version in global.json.
# e.g. sdk 11.0.100-preview.3 -> net11.0
#      sdk 10.0.202            -> net10.0
# Falls back to the highest installed SDK major if no global.json is found.
resolve_tfm() {
  local humanizer_dir="${1}"
  local global_json="${humanizer_dir}/global.json"
  local sdk_version=""

  if [ -f "$global_json" ]; then
    sdk_version="$(grep -oP '"version"\s*:\s*"\K[^"]+' "$global_json" | head -1)"
  fi

  if [ -n "$sdk_version" ]; then
    # Extract major version number from e.g. "11.0.100-preview.3" -> "11"
    local major
    major="$(echo "$sdk_version" | grep -oP '^\d+')"
    echo "net${major}.0"
  else
    # Fallback: pick the highest major from installed SDKs
    local major
    major="$(dotnet --list-sdks 2>/dev/null \
      | grep -oP '^\d+' \
      | sort -rn \
      | head -1)"
    echo "net${major}.0"
  fi
}

ensure_dotnet_sdk() {
  local humanizer_dir="${1}"
  local global_json="${humanizer_dir}/global.json"

  if [ ! -f "$global_json" ]; then
    echo "==> [SDK] No global.json found in ${humanizer_dir} - skipping SDK version check"
    return 0
  fi

  local required_version
  required_version="$(grep -oP '"version"\s*:\s*"\K[^"]+' "$global_json" | head -1)"

  if [ -z "$required_version" ]; then
    echo "==> [SDK] global.json found but no sdk.version field detected - skipping"
    return 0
  fi

  echo "==> [SDK] global.json requires SDK version: ${required_version}"

  if dotnet --list-sdks 2>/dev/null | grep -q "^${required_version}"; then
    echo "==> [SDK] SDK ${required_version} is already installed"
    return 0
  fi

  echo "==> [SDK] SDK ${required_version} not found - installing via dotnet-install.sh..."

  local installer="/tmp/dotnet-install.sh"
  if [ ! -f "$installer" ]; then
    curl -fsSL "https://dot.net/v1/dotnet-install.sh" -o "$installer"
    chmod +x "$installer"
  fi

  local install_dir="${DOTNET_ROOT:-$HOME/.dotnet}"

  # --version and --quality are mutually exclusive in dotnet-install.sh.
  # For preview versions, use --channel <major.minor> --quality preview instead.
  # global.json enforces the exact version at build time anyway.
  if echo "$required_version" | grep -qiE 'preview|rc|beta|alpha'; then
    local channel
    channel="$(echo "$required_version" | grep -oP '^\d+\.\d+')"
    echo "==> [SDK] Preview version detected - installing via --channel ${channel} --quality preview"
    "$installer" --channel "$channel" --quality preview --install-dir "$install_dir"
  else
    "$installer" --version "$required_version" --install-dir "$install_dir"
  fi

  echo "==> [SDK] SDK ${required_version} installed to ${install_dir}"
}

disable_nerdbank() {
  local humanizer_dir="${1}"

  # Nerdbank.GitVersioning requires full git history to calculate version height.
  # Since we clone with --depth=1, it always fails. We don't care about version
  # numbers - just delete version.json so Nerdbank never runs.
  find "$humanizer_dir" -name "version.json" | while read -r f; do
    echo "==> [Nerdbank] Removing $f"
    rm -f "$f"
  done
}