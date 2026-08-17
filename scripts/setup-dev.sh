#!/usr/bin/env bash
# One-shot development environment setup for Linux and macOS.
#
# Installs the pinned .NET SDK, the MAUI workloads this host is *capable* of building,
# and the Android SDK. What you get depends on the host, because Apple's and Microsoft's
# toolchains are not redistributable:
#
#   Linux  -> maui-android            (Android, Blazor, API, unit tests)
#   macOS  -> maui (android+ios+maccatalyst)
#   Windows-> use scripts/setup-dev.ps1 instead
#
# Usage:  scripts/setup-dev.sh [--emulator] [--appium] [--playwright] [--all]
#   --emulator    also install the Android emulator + a system image (needed to *run* the app)
#   --appium      also install Appium and the drivers this host supports (UI tests)
#   --playwright  also install Playwright browsers (Blazor end-to-end tests)
#   --all         all of the above
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

WITH_EMULATOR=0
WITH_APPIUM=0
WITH_PLAYWRIGHT=0
for arg in "$@"; do
  case "$arg" in
    --emulator)   WITH_EMULATOR=1 ;;
    --appium)     WITH_APPIUM=1 ;;
    --playwright) WITH_PLAYWRIGHT=1 ;;
    --all)        WITH_EMULATOR=1; WITH_APPIUM=1; WITH_PLAYWRIGHT=1 ;;
    -h|--help)    sed -n '2,20p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "error: unknown option '$arg'" >&2; exit 1 ;;
  esac
done

HOST="$(uname -s)"
case "$HOST" in
  Linux)  WORKLOADS="maui-android" ;;
  Darwin) WORKLOADS="maui" ;;
  *) echo "error: unsupported host '$HOST'. On Windows run scripts/setup-dev.ps1." >&2; exit 1 ;;
esac

echo "==> [1/4] .NET SDK"
"$REPO_ROOT/scripts/install-dotnet.sh"
# shellcheck source=scripts/dotnet-env.sh
source "$REPO_ROOT/scripts/dotnet-env.sh"

echo
echo "==> [2/4] MAUI workloads ($WORKLOADS)"
# Always invoke the repo-local host: `dotnet workload` records its manifests relative to
# the dotnet root of whichever host runs it, so a system-wide dotnet would install the
# workload somewhere the pinned SDK can't see.
"$DOTNET_ROOT/dotnet" workload install $WORKLOADS --skip-sign-check
"$DOTNET_ROOT/dotnet" workload list

echo
echo "==> [3/4] Android SDK"
INSTALL_EMULATOR="$WITH_EMULATOR" "$REPO_ROOT/scripts/install-android-sdk.sh"

echo
echo "==> [4/4] Optional test tooling"
if [ "$WITH_APPIUM" = "1" ]; then
  "$REPO_ROOT/scripts/install-appium.sh"
else
  echo "skipped Appium (pass --appium to install the MAUI UI-test driver stack)"
fi

if [ "$WITH_PLAYWRIGHT" = "1" ]; then
  echo "Installing Playwright browsers for the Blazor end-to-end tests ..."
  "$DOTNET_ROOT/dotnet" build PaycheckCalculator.E2ETests -c Debug
  PW_SCRIPT="$(find PaycheckCalculator.E2ETests/bin -name playwright.ps1 -print -quit 2>/dev/null || true)"
  if [ -n "$PW_SCRIPT" ] && command -v pwsh >/dev/null 2>&1; then
    pwsh "$PW_SCRIPT" install chromium --with-deps
  else
    echo "warning: could not run Playwright's installer (needs pwsh)." >&2
    echo "         Install PowerShell, then: pwsh PaycheckCalculator.E2ETests/bin/Debug/net11.0/playwright.ps1 install chromium" >&2
  fi
else
  echo "skipped Playwright (pass --playwright to install browsers for Blazor E2E tests)"
fi

cat <<EOF

================================================================================
Setup complete on $HOST.

Every new shell needs the repo-local SDK on PATH:

    source scripts/dotnet-env.sh

Then verify:

    dotnet test  PaycheckCalculator.Tests                     # unit + integration tests
    dotnet build PaycheckCalculator.Blazor                    # web app
    dotnet build PaycheckCalculator.App -f net11.0-android    # MAUI Android
EOF
if [ "$HOST" = "Darwin" ]; then
  cat <<'EOF'
    dotnet build PaycheckCalculator.App -f net11.0-maccatalyst
    dotnet build PaycheckCalculator.App -f net11.0-ios -p:RuntimeIdentifier=iossimulator-arm64
EOF
else
  cat <<'EOF'

Not buildable on Linux (toolchain is OS-locked, no workaround exists):
    net11.0-ios / net11.0-maccatalyst  -> requires macOS + Xcode
    net11.0-windows10.0.19041.0        -> requires Windows
CI covers those on macos-latest and windows-latest runners.
EOF
fi
echo "================================================================================"
