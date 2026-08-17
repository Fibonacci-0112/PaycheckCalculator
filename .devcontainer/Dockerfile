# Development container for PaycheckCalculator.
#
# Covers everything that can be built on Linux: PaycheckCalculator.Core, .Shared, .Api,
# .Blazor, .Tests — and the MAUI app's net11.0-android target, which does build here.
#
# It cannot build net11.0-ios, net11.0-maccatalyst (Xcode is macOS-only) or
# net11.0-windows10.0.19041.0 (WindowsAppSDK is Windows-only). No container can; that is a
# licensing and toolchain constraint, not a packaging gap. CI covers those targets on
# macos-latest and windows-latest runners — see .github/workflows/maui-build.yml.

FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

ENV DEBIAN_FRONTEND=noninteractive

# - openjdk-21-jdk : required by the Android SDK tooling and the Android build
# - libicu-dev     : the .NET globalization stack aborts at startup without ICU, which is
#                    how this project's preview SDK fails on a minimal image
# - unzip/curl     : used by scripts/install-android-sdk.sh
# - libx11-6 ...   : shared libraries the Android emulator and headless Chromium need
RUN apt-get update && apt-get install -y --no-install-recommends \
        openjdk-21-jdk \
        libicu-dev \
        unzip \
        curl \
        zip \
        git \
        libx11-6 \
        libxcb1 \
        libxdamage1 \
        libnss3 \
        libatk1.0-0 \
        libatk-bridge2.0-0 \
        libgbm1 \
        libasound2t64 \
        libpulse0 \
    && rm -rf /var/lib/apt/lists/*

ENV JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64
ENV ANDROID_HOME=/opt/android-sdk
ENV ANDROID_SDK_ROOT=/opt/android-sdk

# Bake the Android SDK into the image so container creation doesn't re-download ~1 GB every
# time. The .NET SDK and workloads are installed by postCreateCommand instead, into the
# workspace, so they track global.json rather than the image build date.
COPY scripts/install-android-sdk.sh /tmp/install-android-sdk.sh
RUN chmod +x /tmp/install-android-sdk.sh \
    && INSTALL_EMULATOR=1 /tmp/install-android-sdk.sh \
    && rm /tmp/install-android-sdk.sh \
    && chmod -R a+rwX "$ANDROID_HOME"

ENV PATH="${PATH}:/opt/android-sdk/platform-tools:/opt/android-sdk/emulator:/opt/android-sdk/cmdline-tools/latest/bin"
