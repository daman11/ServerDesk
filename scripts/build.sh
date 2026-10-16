#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build ServerDesk.sln -c Release -m:1
dotnet test tests/ServerDesk.Tests -c Release --no-build --logger 'trx;LogFileName=unit.trx' --results-directory artifacts/test-results
