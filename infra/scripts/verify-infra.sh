#!/usr/bin/env bash
# Local init/schema/format/mocked plan tests only. Never runs real plan/apply/destroy.
set -euo pipefail
cd "$(dirname "$0")/../.."
log_dir="${1:-artifacts/infra-validation}"
mkdir -p "$log_dir"
export CHECKPOINT_DISABLE=1 TF_IN_AUTOMATION=1 AWS_EC2_METADATA_DISABLED=true
run_check() {
    local name="$1"
    shift
    printf 'Command:'
    printf ' %q' "$@"
    printf '\n'
    "$@" 2>&1 | tee "$log_dir/$name.log"
}
run_check version terraform version
run_check init terraform -chdir=infra/terraform init -backend=false -input=false -no-color
run_check fmt terraform -chdir=infra/terraform fmt -check -recursive
run_check validate terraform -chdir=infra/terraform validate -no-color
run_check test terraform -chdir=infra/terraform test -no-color
run_check cost python3 infra/scripts/estimate-aws-cost.py
