#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
log_dir="${1:-artifacts/validation}"
mkdir -p "$log_dir"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 MSBUILDDISABLENODEREUSE=1
run_check() {
    local name="$1"
    shift
    printf 'Command:'
    printf ' %q' "$@"
    printf '\n'
    "$@" 2>&1 | tee "$log_dir/$name.log"
}
run_check sdk dotnet --version
run_check restore dotnet restore --locked-mode
run_check build dotnet build -warnaserror --no-restore
run_check test dotnet test --no-build --filter 'Category!=Database'
run_check format dotnet format --verify-no-changes --no-restore
