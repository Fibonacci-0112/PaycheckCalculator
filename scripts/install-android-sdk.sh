#!/usr/bin/env bash
# Installs a headless Android SDK (command-line tools + the packages .NET Android needs)
# so `dotnet build PaycheckCalculator.App -f net11.0-android` works on a bare machine.
#
# Without this, the Android build fails with XA5300 ("The Android SDK directory could not
# be found"), which is the single most common reason a fresh Linux box or container can't
# build the MAUI app.
#
# Usage:  scripts/install-android-sdk.sh
# Env:
#   ANDROID_HOME          install location (default $HOME/android-sdk)
#   ANDROID_API_LEVEL     platform to install (default 37.0, matching targetSdkVersion 37).
#                         Note the ".0": from API 36 onward Google publishes minor-versioned
#                         platform packages, so the package id is `platforms;android-37.0`,
#                         not `platforms;android-37`. Asking for the unsuffixed id fails with
#                         "Failed to find package".
#   ANDROID_BUILD_TOOLS   build-tools version (default 37.0.0)
#   INSTALL_EMULATOR      set to 1 to also install the emulator + a system image, so the
#                         app can actually be *run*, not just built
set -euo pipefail

ANDROID_HOME="${ANDROID_HOME:-$HOME/android-sdk}"
ANDROID_API_LEVEL="${ANDROID_API_LEVEL:-37.0}"
ANDROID_BUILD_TOOLS="${ANDROID_BUILD_TOOLS:-37.0.0}"
CMDLINE_TOOLS_VERSION="${CMDLINE_TOOLS_VERSION:-13114758}"
INSTALL_EMULATOR="${INSTALL_EMULATOR:-0}"
EMULATOR_SYSTEM_IMAGE="${EMULATOR_SYSTEM_IMAGE:-system-images;android-35;google_apis;x86_64}"

case "$(uname -s)" in
  Linux)  CMDLINE_TOOLS_OS=linux ;;
  Darwin) CMDLINE_TOOLS_OS=mac ;;
  *) echo "error: unsupported host '$(uname -s)'; use scripts/setup-dev.ps1 on Windows" >&2; exit 1 ;;
esac

if ! command -v java >/dev/null 2>&1; then
  echo "error: a JDK is required (sdkmanager and the Android build both need one)." >&2
  echo "  Debian/Ubuntu: sudo apt-get install -y openjdk-21-jdk" >&2
  echo "  macOS:         brew install --cask temurin@21" >&2
  exit 1
fi

export ANDROID_HOME
export ANDROID_SDK_ROOT="$ANDROID_HOME"
SDKMANAGER="$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager"

if [ ! -x "$SDKMANAGER" ]; then
  echo "Installing Android command-line tools ($CMDLINE_TOOLS_VERSION) into $ANDROID_HOME ..."
  mkdir -p "$ANDROID_HOME/cmdline-tools"
  TMP_ZIP="$(mktemp -d)/cmdline-tools.zip"
  curl -fsSL -o "$TMP_ZIP" \
    "https://dl.google.com/android/repository/commandlinetools-${CMDLINE_TOOLS_OS}-${CMDLINE_TOOLS_VERSION}_latest.zip"
  # The archive expands to a bare `cmdline-tools/` directory; sdkmanager insists on being
  # located at cmdline-tools/latest/ so it can resolve the rest of the SDK relative to itself.
  rm -rf "$ANDROID_HOME/cmdline-tools/latest" "$ANDROID_HOME/cmdline-tools/cmdline-tools"
  unzip -q "$TMP_ZIP" -d "$ANDROID_HOME/cmdline-tools"
  mv "$ANDROID_HOME/cmdline-tools/cmdline-tools" "$ANDROID_HOME/cmdline-tools/latest"
  rm -rf "$(dirname "$TMP_ZIP")"
fi

PACKAGES=(
  "platform-tools"
  "platforms;android-${ANDROID_API_LEVEL}"
  "build-tools;${ANDROID_BUILD_TOOLS}"
)
if [ "$INSTALL_EMULATOR" = "1" ]; then
  PACKAGES+=("emulator" "$EMULATOR_SYSTEM_IMAGE")
fi

echo "Accepting Android SDK licenses ..."
yes | "$SDKMANAGER" --licenses >/dev/null 2>&1 || true

echo "Installing: ${PACKAGES[*]}"
"$SDKMANAGER" --install "${PACKAGES[@]}"

echo
echo "Android SDK ready at $ANDROID_HOME"
echo "Export it for the build with:"
echo "    export ANDROID_HOME=\"$ANDROID_HOME\""
echo "(or 'source scripts/dotnet-env.sh', which detects it automatically)"
