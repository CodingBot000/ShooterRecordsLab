#!/usr/bin/env python3
"""Publish only an explicit allowlist of validation facts; never copy raw cloud/test logs."""
import argparse
import datetime
import json
import re
from pathlib import Path

CHECKS = {
    "local-build", "local-tests", "linux-build", "linux-tests", "container-health",
    "sql-integration", "sql-tls", "http-liveness", "http-readiness", "http-player",
    "http-create", "http-replay", "http-conflict", "http-match", "http-stats",
    "http-rankings", "http-pagination", "http-concurrent-idempotency", "http-validation",
    "auth-missing", "auth-scope", "auth-signature", "auth-expired", "auth-issuer",
    "auth-audience", "deployment-recovery", "database-disconnect-readiness",
    "query-plan-matches", "query-plan-rankings", "query-plan-statistics",
}


def build_report(source):
    revision = source["sourceCommit"]
    digest = source["imageDigest"]
    when = source["verifiedAtUtc"]
    if not re.fullmatch(r"[a-f0-9]{40}", revision):
        raise ValueError("Invalid source revision")
    if not re.fullmatch(r"sha256:[a-f0-9]{64}", digest):
        raise ValueError("Invalid content digest")
    parsed = datetime.datetime.fromisoformat(when.replace("Z", "+00:00"))
    if parsed.utcoffset() != datetime.timedelta(0):
        raise ValueError("UTC verification time required")
    checks = []
    for item in source["checks"]:
        if item["name"] not in CHECKS or item["result"] not in {"PASS", "FAIL", "NOT_RUN"}:
            raise ValueError("Unknown verification case")
        result = {"name": item["name"], "result": item["result"]}
        for key in ["passed", "failed", "skipped", "sampleRows", "returnedRows", "logicalReads", "planOperators", "durationMs"]:
            if key in item:
                value = item[key]
                if type(value) not in {int, float} or not 0 <= value < 10**12:
                    raise ValueError("Invalid numeric verification result")
                result[key] = value
        checks.append(result)
    return {
        "schemaVersion": 1,
        "sourceCommit": revision,
        "imageDigest": digest,
        "verifiedAtUtc": when,
        "platform": "AWS EC2 Linux x86_64 / private RDS SQL Server 2022 / SSM",
        "checks": checks,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("private_input", type=Path)
    parser.add_argument("public_output", type=Path)
    args = parser.parse_args()
    report = build_report(json.loads(args.private_input.read_text()))
    args.public_output.parent.mkdir(parents=True, exist_ok=True)
    args.public_output.write_text(json.dumps(report, indent=2) + "\n")


if __name__ == "__main__":
    main()
