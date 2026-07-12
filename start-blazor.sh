#!/usr/bin/env bash
set -e
export PATH="$PWD/.dotnet:$PATH"
export DOTNET_ROOT="$PWD/.dotnet"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://0.0.0.0:5000"
export PaycheckApi__BaseUrl="http://127.0.0.1:5201"
exec dotnet run --project PaycheckCalc.Blazor --no-launch-profile
