#!/usr/bin/env bash
# Installs Appium plus the drivers this host can actually drive, for the MAUI UI tests.
#
# Driver availability is dictated by the host OS, not by preference:
#   uiautomator2 (Android)      Linux, macOS, Windows
#   xcuitest     (iOS)          macOS only
#   mac2         (Mac Catalyst) macOS only
#   windows      (WinUI)        Windows only  -> see scripts/setup-dev.ps1
set -euo pipefail

if ! command -v npm >/dev/null 2>&1; then
  echo "error: Node.js (with npm) is required. Install the LTS release from https://nodejs.org/" >&2
  exit 1
fi

APPIUM_VERSION="${APPIUM_VERSION:-3}"

if ! command -v appium >/dev/null 2>&1; then
  echo "Installing Appium $APPIUM_VERSION ..."
  npm install -g "appium@$APPIUM_VERSION"
fi

install_driver() {
  if appium driver list --installed 2>&1 | grep -q "$1"; then
    echo "appium driver '$1' already installed"
  else
    echo "Installing appium driver '$1' ..."
    appium driver install "$1"
  fi
}

install_driver uiautomator2

if [ "$(uname -s)" = "Darwin" ]; then
  install_driver xcuitest
  install_driver mac2
else
  echo "skipping xcuitest/mac2 drivers: iOS and Mac Catalyst automation requires macOS + Xcode"
fi

echo
appium driver list --installed
echo
echo "Start the server with:  appium"
echo "It listens on http://127.0.0.1:4723 — the UI tests start it themselves if it isn't running."
