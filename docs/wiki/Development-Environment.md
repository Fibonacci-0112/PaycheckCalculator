# Development Environment & CI/CD

How to get a machine that can build **and run** every part of PaycheckCalculator, and what
CI does on your behalf.

---

## The short version: you need three operating systems

The .NET MAUI target frameworks are not portable across host operating systems. This is a
toolchain and licensing constraint, not a configuration problem — no container, VM image, or
cross-compiler changes it.

| Target framework | Buildable on | Why |
|---|---|---|
| `net11.0-android` | **Linux, macOS, Windows** | The `maui-android` workload installs on all three; needs a JDK and the Android SDK. |
| `net11.0-windows10.0.19041.0` | **Windows only** | WinUI 3 / Windows App SDK. |
| `net11.0-ios` | **macOS only** | Requires Xcode. |
| `net11.0-maccatalyst` | **macOS only** | Requires Xcode. |

`PaycheckCalculator.App.csproj` already encodes this — the Apple targets are appended only
under `IsOSPlatform('osx')` and the Windows target only under `IsOSPlatform('windows')`.

So: **Linux plus Windows is not enough.** That combination covers Android and WinUI but
leaves iOS and Mac Catalyst — half the MAUI targets — with no host that can compile them.
CI therefore uses `ubuntu-latest`, `windows-latest`, **and** `macos-latest`.

Everything else in the solution — `Core`, `Shared`, `Api`, `Blazor`, `Tests`,
`E2ETests`, and all four `UITests.*` projects — targets plain `net11.0` and builds anywhere.

---

## Setting up a machine

The setup scripts are the same ones CI runs, so a green pipeline means these work.

### Linux and macOS

```bash
scripts/setup-dev.sh              # SDK + workloads + Android SDK
scripts/setup-dev.sh --all        # …plus emulator, Appium, and Playwright browsers
```

### Windows

```powershell
pwsh scripts/setup-dev.ps1
pwsh scripts/setup-dev.ps1 -All
```

### What the scripts do

1. **`scripts/install-dotnet.sh`** reads the exact SDK version out of `global.json` and
   installs it into a repo-local `.dotnet/` (git-ignored). The project pins a *preview* SDK
   that no distro package manager or CI image carries, so every environment obtains it the
   same way.
2. **Workloads** — `maui-android` on Linux, the full `maui` workload on Windows and macOS.
   The scripts invoke the repo-local `dotnet` host explicitly: `dotnet workload` records its
   manifests relative to whichever host runs it, so a system-wide `dotnet` would install the
   workload where the pinned SDK cannot see it.
3. **`scripts/install-android-sdk.sh`** installs command-line tools, `platform-tools`,
   `platforms;android-37.0` and `build-tools;37.0.0`. Without this the Android build fails
   with `XA5300: The Android SDK directory could not be found`.

> **Note on the platform package id.** From API 36 onward Google publishes *minor-versioned*
> platform packages. The package is `platforms;android-37.0`, not `platforms;android-37`;
> asking for the unsuffixed id fails with "Failed to find package".

### Every new shell

```bash
source scripts/dotnet-env.sh
```

This puts `.dotnet/` first on `PATH` (so the pinned preview SDK shadows any system-wide
install) and exports `ANDROID_HOME` when it finds an Android SDK.

### Dev container

`.devcontainer/` gives a ready-made Linux environment: JDK 21, ICU, the Android SDK and
emulator baked into the image, with the .NET SDK and `maui-android` workload installed on
first create. It covers Blazor, the API, the tests, and the MAUI **Android** target.
It cannot cover iOS, Mac Catalyst, or WinUI — see the table above.

---

## Verifying the setup

```bash
source scripts/dotnet-env.sh

dotnet test  PaycheckCalculator.Tests                      # unit + integration
dotnet build PaycheckCalculator.Blazor                     # web app
dotnet build PaycheckCalculator.App -f net11.0-android     # MAUI Android
```

On macOS additionally:

```bash
dotnet build PaycheckCalculator.App -f net11.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
dotnet build PaycheckCalculator.App -f net11.0-ios         -p:RuntimeIdentifier=iossimulator-arm64
```

On Windows additionally:

```powershell
dotnet build PaycheckCalculator.App -f net11.0-windows10.0.19041.0
```

---

## Running the apps under test

Building proves the code compiles. It proves nothing about whether the app survives launch.
Two suites cover that gap.

### Blazor — `PaycheckCalculator.E2ETests`

xUnit + Playwright. Boots the Blazor server and drives Chromium through a real calculation.

```bash
dotnet test PaycheckCalculator.E2ETests
```

With no configuration the fixture starts `dotnet run` on a free port itself. Set
`E2E_BASE_URL` to test an already-running server instead — which is what CI does, pointing
at the *published* output so the artifact under test is the one that would ship.

| Variable | Effect |
|---|---|
| `E2E_BASE_URL` | Test a running server instead of starting one. |
| `E2E_HEADED=1` | Run the browser headed, for debugging a selector. |
| `E2E_BROWSER_EXECUTABLE` | Use a specific Chromium binary. Playwright pins an exact build per release and refuses others. |

Browsers install with:

```bash
pwsh PaycheckCalculator.E2ETests/bin/Debug/net11.0/playwright.ps1 install chromium
```

### MAUI — `PaycheckCalculator.UITests.*`

NUnit + Appium, following the [official .NET MAUI UI-testing
pattern](https://learn.microsoft.com/dotnet/maui/deployment/ui-testing): shared test code
plus one runner project per platform.

```
PaycheckCalculator.UITests.Shared/       tests + helpers, linked into each runner
PaycheckCalculator.UITests.Android/      UiAutomator2  — Linux, macOS, Windows hosts
PaycheckCalculator.UITests.iOS/          XCUITest      — macOS host only
PaycheckCalculator.UITests.MacCatalyst/  Mac2          — macOS host only
PaycheckCalculator.UITests.Windows/      windows       — Windows host only
```

All four assemblies share the namespace `PaycheckCalculator.UITests`. That is load-bearing:
NUnit matches a `[SetUpFixture]` to the fixtures it initializes **by namespace**, so a
diverging namespace leaves the Appium driver null and every test failing for an unrelated-looking
reason.

Run them with the app already deployed to a device, emulator, or simulator:

```bash
appium                                                # in another terminal
dotnet test PaycheckCalculator.UITests.Android
```

| Variable | Effect |
|---|---|
| `UITEST_APP_PATH` | Path to the `.apk` / `.app` / `.exe` to install. Required on Windows (the WinUI build is unpackaged, so there is no AUMID to launch by). |
| `UITEST_DEVICE_NAME` | Target device or simulator name. |
| `UITEST_PLATFORM_VERSION` | Target OS version. Leave unset so the driver picks an installed runtime. |
| `UITEST_ARTIFACT_DIR` | Where failure screenshots are written. |
| `APPIUM_SERVER_URI` | Appium endpoint (default `http://127.0.0.1:4723/`). |
| `APPIUM_EXTERNAL_SERVER=1` | Don't start a server; one is already running. |

#### What these tests are for

Not the tax math — the unit suite covers that far more precisely. These cover the failures
that compile cleanly and only appear on a device:

- a service missing from `MauiProgram`'s container,
- a `StaticResource` key that only resolves at run time,
- **a tax JSON renamed without updating the `MauiAsset` entries in the csproj** — the exact
  hazard `CLAUDE.md` warns about, which builds green and dies on the first calculation.

#### Adding a new element to a test

Set `AutomationId` on the control in XAML. It maps to the platform accessibility id that
Appium locates elements by. `BaseTest.FindElement` handles the one platform difference:
WinUI exposes it as an accessibility id, everything else as a plain element id.

Shell tab bar items are the exception — the native tab is built by the platform renderer and
doesn't carry the XAML `AutomationId`, so `GoToTab` locates tabs by their visible text.

---

## CI/CD

| Workflow | Runners | What it proves |
|---|---|---|
| `ci.yml` | ubuntu | Unit + integration tests; Blazor publishes, **starts**, and passes browser E2E; the sync API publishes. |
| `maui-build.yml` | ubuntu + windows + macos | The MAUI app compiles for all four target platforms. |
| `maui-uitests.yml` | ubuntu + windows + macos | The MAUI app **launches and can be driven** on an Android emulator, an iOS Simulator, a macOS desktop, and a Windows desktop. |
| `codeql.yml` | ubuntu | CodeQL over C#, JavaScript/TypeScript, and the workflow definitions themselves. |
| `release.yml` | ubuntu + windows + macos | On a `v*` tag: builds every shippable artifact and drafts a GitHub Release. |

`.github/actions/setup-dotnet-maui` is a composite action shared by all of them. It reads the
SDK version from `global.json`, picks the right workload set for the runner OS, caches NuGet,
and reuses `scripts/install-android-sdk.sh` — the same script you run locally, so the two
paths cannot drift.

### Notes on specific jobs

- **Android emulator.** `ubuntu-latest` supports nested virtualisation, but `/dev/kvm` is
  only reachable by the runner user after a udev rule is installed. Without it the emulator
  falls back to software rendering and is too slow to finish. The job also reclaims disk
  space first — an Android SDK, an emulator image, and a MAUI build together come close to
  filling a standard runner.
- **iOS.** Built for the **Simulator** (`iossimulator-arm64`), which is ad-hoc signed and
  needs no Apple Developer certificate. The job discovers whichever iPhone simulator the
  runner image carries rather than pinning a model, because the available set changes with
  every Xcode bump.
- **Windows.** The app sets `WindowsPackageType=None`, so the build is unpackaged: no MSIX,
  no certificate, and Appium launches it by executable path. The job installs WinAppDriver
  **1.2.1 specifically** — other releases are known not to pair correctly with the Appium
  windows driver — and enables Developer Mode, without which UI Automation won't attach.
- **CodeQL** uses `build-mode: none` for C#. `autobuild` on a Linux runner can only compile
  the projects that build without the MAUI workload, which silently left the entire MAUI app
  out of every scan. Buildless analysis covers all of it.
- **Release signing is optional.** Artifacts that need no secrets are always produced.
  Android is signed only when `ANDROID_KEYSTORE_BASE64` and friends exist; a distributable
  `.ipa` is produced only when `APPLE_CERTIFICATE_BASE64` and a provisioning profile exist.
  Otherwise those steps degrade to unsigned or Simulator-only rather than failing the release.

### Secrets used by `release.yml`

All optional.

| Secret | Used for |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Base64 of the release keystore. |
| `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` | Android signing. |
| `APPLE_CERTIFICATE_BASE64`, `APPLE_CERTIFICATE_PASSWORD` | iOS signing identity. |
| `APPLE_PROVISIONING_PROFILE_BASE64`, `APPLE_PROVISIONING_PROFILE_NAME` | iOS provisioning. |
| `APPLE_CODESIGN_KEY` | Codesign identity name. |

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `XA5300: The Android SDK directory could not be found` | Run `scripts/install-android-sdk.sh` and `source scripts/dotnet-env.sh`. |
| `Failed to find package 'platforms;android-37'` | The id is `platforms;android-37.0`. See the note above. |
| Globalization error on startup with the preview SDK | ICU is missing. `sudo apt-get install -y libicu-dev`. |
| `dotnet workload list` doesn't show the workload you just installed | It was installed against a different `dotnet` host. Use `./.dotnet/dotnet workload install …`. |
| Playwright: `Executable doesn't exist at …` | Browser build doesn't match the Playwright version. Run `playwright.ps1 install chromium`, or set `E2E_BROWSER_EXECUTABLE`. |
| Every UI test fails with a null driver | The platform project's `AppiumSetup` is in a different namespace from the shared fixtures. They must all be `PaycheckCalculator.UITests`. |
| Mac Catalyst tests automate Finder | The `bundleId` capability is missing — Mac2 requires it. |
