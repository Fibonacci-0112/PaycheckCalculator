#!/bin/bash
# SessionStart hook for Claude Code on the web.
#
# Builds out the toolchain the repo needs so a remote session can build, test and run the
# projects instead of discovering halfway through that `dotnet` isn't installed:
#
#   1. the exact .NET SDK pinned in global.json (a preview build no image carries)
#   2. the maui-android workload, so PaycheckCalculator.App's Android target compiles
#   3. the Android SDK packages that target needs
#
# iOS, Mac Catalyst and WinUI cannot be built on Linux at all — CI covers those on
# macos-latest and windows-latest runners. See docs/wiki/Development-Environment.md.
#
# Everything here is idempotent: each step checks for an existing install first, so a
# resumed or cleared session re-runs it in seconds.
set -euo pipefail

# Local runs already have a configured machine (or scripts/setup-dev.sh to make one).
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"

DOTNET_DIR="$PWD/.dotnet"
export DOTNET_ROOT="$DOTNET_DIR"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_DIR:$PATH"

echo "==> Installing the .NET SDK pinned in global.json"
bash scripts/install-dotnet.sh

# Persist for the rest of the session, so every later `dotnet` resolves to the pinned
# preview SDK rather than any system-wide install that would fail on net11.0.
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\""
    echo "export PATH=\"$DOTNET_DIR:\$PATH\""
    echo "export DOTNET_NOLOGO=1"
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
  } >> "$CLAUDE_ENV_FILE"
fi

echo "==> Restoring NuGet packages"
"$DOTNET_DIR/dotnet" restore PaycheckCalculator.Tests

# --- MAUI Android -----------------------------------------------------------------------
# Optional: skip with PAYCHECK_SKIP_MAUI=1 when a session only touches Core/Blazor/Api and
# the extra few minutes of first-run install aren't worth it.
if [ "${PAYCHECK_SKIP_MAUI:-0}" = "1" ]; then
  echo "==> Skipping MAUI setup (PAYCHECK_SKIP_MAUI=1)"
  exit 0
fi

if ! command -v java >/dev/null 2>&1; then
  echo "==> Installing a JDK (required by the Android build)"
  if command -v apt-get >/dev/null 2>&1; then
    sudo apt-get update -qq && sudo apt-get install -y -qq openjdk-21-jdk-headless \
      || echo "warning: JDK install failed; the Android target will not build." >&2
  fi
fi

echo "==> Installing the maui-android workload"
# `dotnet workload` records manifests relative to the dotnet root of the host that runs it,
# so this must be the repo-local host, not any system-wide dotnet.
if "$DOTNET_DIR/dotnet" workload list 2>/dev/null | grep -q '^maui-android'; then
  echo "maui-android already installed"
else
  "$DOTNET_DIR/dotnet" workload install maui-android --skip-sign-check \
    || echo "warning: maui-android install failed; only the net11.0 projects will build." >&2
fi

echo "==> Installing the Android SDK"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/android-sdk}"
if bash scripts/install-android-sdk.sh; then
  if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
    {
      echo "export ANDROID_HOME=\"$ANDROID_HOME\""
      echo "export ANDROID_SDK_ROOT=\"$ANDROID_HOME\""
      echo "export PATH=\"\$PATH:$ANDROID_HOME/platform-tools\""
    } >> "$CLAUDE_ENV_FILE"
  fi
else
  echo "warning: Android SDK install failed; the net11.0-android target will not build." >&2
fi

echo "==> Environment ready"
"$DOTNET_DIR/dotnet" --version
