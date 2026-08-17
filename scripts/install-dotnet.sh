#!/usr/bin/env bash
# Installs the exact .NET SDK pinned in global.json into a repo-local .dotnet/ directory.
#
# The repo pins a *preview* SDK, which is not carried by most distro package managers,
# Nix modules or CI images, so every environment installs it the same way: straight from
# Microsoft's dotnet-install script into .dotnet/ (git-ignored). Callers then put that
# directory first on PATH so it shadows any system-wide dotnet.
#
# Usage:  scripts/install-dotnet.sh [install-dir]
# Env:    DOTNET_INSTALL_DIR   override the install directory (default <repo>/.dotnet)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INSTALL_DIR="${1:-${DOTNET_INSTALL_DIR:-$REPO_ROOT/.dotnet}}"

# Pull the version straight out of global.json so this script can never drift from the pin.
SDK_VERSION="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$REPO_ROOT/global.json" | head -1)"
if [ -z "$SDK_VERSION" ]; then
  echo "error: could not read sdk.version from $REPO_ROOT/global.json" >&2
  exit 1
fi

if [ -x "$INSTALL_DIR/dotnet" ] && "$INSTALL_DIR/dotnet" --list-sdks 2>/dev/null | grep -q "^$SDK_VERSION "; then
  echo ".NET SDK $SDK_VERSION already installed in $INSTALL_DIR"
  exit 0
fi

echo "Installing .NET SDK $SDK_VERSION into $INSTALL_DIR ..."
SCRIPT="$(mktemp)"
trap 'rm -f "$SCRIPT"' EXIT
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$SCRIPT"
chmod +x "$SCRIPT"
"$SCRIPT" --version "$SDK_VERSION" --install-dir "$INSTALL_DIR"

echo
echo ".NET SDK installed. Add it to your shell with:"
echo "    source scripts/dotnet-env.sh"
