#!/usr/bin/env python3
"""Recalculate unapproved cost scenarios from the saved official price excerpt; no AWS calls."""
import json
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = root / "docs/validation/p3-preparation/pricing-extract.json"
catalog = json.loads(source.read_text())
rates = {}
for entry in catalog["rates"]:
    if "sku" in entry:
        rates[entry["sku"]] = Decimal(entry["dimension"]["pricePerUnit"]["USD"])
    else:
        rates[entry["record"]["SKU"]] = Decimal(entry["record"]["PricePerUnit"])

ipv4 = Decimal(catalog["webRates"]["ipv4_hour_usd"])
secret = Decimal(catalog["webRates"]["secret_month_usd"])

def dollars(value):
    return str(value.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP))

scenarios = []
for name, db_sku, hours in [
    ("A_micro_730h", "ZA8WQZP3UAQPDPRR", 730),
    ("B_small_730h", "MRVAAV85VCTB8AXB", 730),
    ("C_small_60h_illustration_only", "MRVAAV85VCTB8AXB", 60),
]:
    components = {
        "rds_compute": rates[db_sku] * hours,
        "ec2_t3_micro": rates["NYVHMS44MY8N2F3M"] * hours,
        "one_public_ipv4": ipv4 * hours,
        "rds_gp3_20gib_full_month": rates["4NX6YFN5ACHCJXKU"] * 20,
        "ebs_gp3_8gib_full_month": rates["MTK7D9SGKGYR3JD6"] * 8,
        "one_managed_master_secret_full_month": secret,
    }
    scenarios.append({
        "name": name,
        "running_hours": hours,
        "approved": False,
        "components_usd": {k: dollars(v) for k, v in components.items()},
        "base_total_usd": dollars(sum(components.values())),
    })

print(json.dumps({
    "region": catalog["region"],
    "retrieved_date_kst": catalog["retrievedDateKst"],
    "warning": "Illustrations only, NOT a spending ceiling or approved configuration. No free tier, discounts, taxes or exchange rates assumed.",
    "excluded_variable_charges": ["RDS T3 surplus CPU credits", "billable backup/snapshot GB-month", "Secrets API calls and additional secrets", "KMS requests", "network transfer", "optional logs/ECR/other services", "existing account charges"],
    "scenarios": scenarios,
    "variable_charge_examples_usd": {
        "rds_100_surplus_vcpu_hours": dollars(rates["66QGSVBE6UFUSZUN"] * 100),
        "backup_20_billable_gb_month": dollars(rates["S6TKPGFCM7H35SHE"] * 20),
    },
}, indent=2))
