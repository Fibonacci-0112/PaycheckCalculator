#!/usr/bin/env bash
# Installs the app on the booted emulator, starts Appium, and runs the Android UI tests.
#
# This lives in a file rather than inline in the workflow because
# reactivecircus/android-emulator-runner executes its `script:` input one line at a time,
# each as a separate `sh -c` invocation. Multi-line shell constructs therefore cannot work
# there — a `for` loop dies with "Syntax error: end of file unexpected (expecting done)",
# and `set -eu` on its own line applies to nothing. The workflow calls this as a single
# line, so the whole sequence runs in one bash process with the semantics it expects.
#
# Env:
#   UITEST_APP_PATH      path to the signed APK to install (required)
#   APPIUM_LOG           where to write the Appium server log (default appium.log)
set -euo pipefail

APPIUM_LOG="${APPIUM_LOG:-appium.log}"

if [ -z "${UITEST_APP_PATH:-}" ]; then
  echo "::error::UITEST_APP_PATH is not set."
  exit 1
fi

echo "==> Waiting for the emulator"
adb wait-for-device

echo "==> Installing $UITEST_APP_PATH"
# -r reinstalls over an existing copy, -g pre-grants runtime permissions so no system
# dialog appears in front of the app and steals the first taps.
adb install -r -g "$UITEST_APP_PATH"

echo "==> Starting Appium"
appium --log-timestamp --log-level info > "$APPIUM_LOG" 2>&1 &
appium_pid=$!

appium_ready=0
for _ in $(seq 1 60); do
  if curl -fsS http://127.0.0.1:4723/status > /dev/null 2>&1; then
    appium_ready=1
    break
  fi
  sleep 2
done

if [ "$appium_ready" -ne 1 ]; then
  echo "::error::Appium did not start within 120s."
  cat "$APPIUM_LOG" || true
  exit 1
fi
echo "Appium is up (pid $appium_pid)."

echo "==> Running the Android UI tests"
dotnet test PaycheckCalculator.UITests.Android --no-build \
  --logger "trx;LogFileName=android-uitests.trx" \
  --logger "console;verbosity=normal" \
  --results-directory ./TestResults
