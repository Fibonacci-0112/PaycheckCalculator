# Sourceable environment for the repo-local .NET SDK and Android SDK.
#
#     source scripts/dotnet-env.sh
#
# Puts <repo>/.dotnet first on PATH so the pinned preview SDK shadows any system-wide
# dotnet, and exports ANDROID_HOME/JAVA_HOME when a repo-local or well-known Android SDK
# is present. Safe to source repeatedly.

# shellcheck shell=bash
__pc_repo_root="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"

export DOTNET_ROOT="${DOTNET_ROOT:-$__pc_repo_root/.dotnet}"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
# Workload resolution reads the manifests next to the SDK that the *host* dotnet came
# from, so the repo-local host has to win PATH lookup.
case ":$PATH:" in
  *":$DOTNET_ROOT:"*) ;;
  *) export PATH="$DOTNET_ROOT:$PATH" ;;
esac

if [ -z "${ANDROID_HOME:-}" ]; then
  for __pc_candidate in "$__pc_repo_root/.android-sdk" "$HOME/android-sdk" "$HOME/Android/Sdk" "$HOME/Library/Android/sdk" "/usr/local/lib/android/sdk"; do
    if [ -d "$__pc_candidate" ]; then
      export ANDROID_HOME="$__pc_candidate"
      break
    fi
  done
fi

if [ -n "${ANDROID_HOME:-}" ]; then
  export ANDROID_SDK_ROOT="$ANDROID_HOME"
  for __pc_bin in "$ANDROID_HOME/platform-tools" "$ANDROID_HOME/emulator" "$ANDROID_HOME/cmdline-tools/latest/bin"; do
    case ":$PATH:" in
      *":$__pc_bin:"*) ;;
      *) [ -d "$__pc_bin" ] && export PATH="$PATH:$__pc_bin" ;;
    esac
  done
fi

unset __pc_repo_root __pc_candidate __pc_bin
