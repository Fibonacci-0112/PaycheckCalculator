#!/bin/bash
# SessionStart hook for Claude Code on the web.
#
# The remote container ships without a .NET SDK, so this installs the SDK pinned
# in global.json and warms the NuGet/build caches for the projects that build on
# Linux (Core, Shared, API, Blazor, Tests). The MAUI app is deliberately skipped:
# PaycheckCalculator.App needs the `maui` workload plus a platform SDK and is not
# built by CI either.
set -euo pipefail

# Local machines already have their own toolchain; only set up the web container.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$PROJECT_DIR"

DOTNET_INSTALL_DIR="$HOME/.dotnet"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_ROOT="$DOTNET_INSTALL_DIR"
export PATH="$DOTNET_INSTALL_DIR:$DOTNET_INSTALL_DIR/tools:$PATH"

# Read the pinned SDK version out of global.json without depending on jq.
SDK_VERSION="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' global.json | head -n1)"
if [ -z "$SDK_VERSION" ]; then
  echo "session-start: could not read the SDK version from global.json" >&2
  exit 1
fi

# Idempotent: reinstalling is skipped when the pinned SDK is already present.
if ! dotnet --list-sdks 2>/dev/null | grep -q "^${SDK_VERSION} "; then
  echo "session-start: installing .NET SDK ${SDK_VERSION} into ${DOTNET_INSTALL_DIR}"
  INSTALL_SCRIPT="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
  bash "$INSTALL_SCRIPT" --version "$SDK_VERSION" --install-dir "$DOTNET_INSTALL_DIR" --no-path
  rm -f "$INSTALL_SCRIPT"
else
  echo "session-start: .NET SDK ${SDK_VERSION} already installed"
fi

# dotnet-ef, pinned in .config/dotnet-tools.json (EF Core migrations for the API).
echo "session-start: restoring local dotnet tools"
dotnet tool restore

# Restoring and building the test project transitively covers Core, Shared, API
# and Blazor -- the same set CI builds -- so tests and analyzers are ready to run.
echo "session-start: building PaycheckCalculator.Tests (and its project references)"
dotnet build PaycheckCalculator.Tests

# Persist the toolchain for the rest of the session.
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"${DOTNET_INSTALL_DIR}\""
    echo "export PATH=\"${DOTNET_INSTALL_DIR}:${DOTNET_INSTALL_DIR}/tools:\$PATH\""
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi

echo "session-start: ready"
