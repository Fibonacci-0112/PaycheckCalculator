#!/usr/bin/env bash
set -e
export PATH="$PWD/.dotnet:$PATH"
export DOTNET_ROOT="$PWD/.dotnet"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="http://0.0.0.0:5201"
export ConnectionStrings__Sync="Host=${PGHOST};Port=${PGPORT};Database=${PGDATABASE};Username=${PGUSER};Password=${PGPASSWORD};SSL Mode=Disable"
exec dotnet run --project PaycheckCalc.Api --no-launch-profile
