import json
from datetime import datetime
from pathlib import Path
import sys
import re

# Use raw strings for Windows paths to avoid escape-sequence warnings
DECISIONS_FILE = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CampaignAILab\Logs\decisions.jsonl"
OUTCOMES_FILE = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CampaignAILab\Logs\outcomes.jsonl"


def parse_iso(ts: str):
    """
    Try to parse an ISO-8601 timestamp. If that fails, accept the game's human-friendly
    timestamp format like 'Summer 2, 1084' and convert it deterministically to a
    datetime for validation purposes.
    """
    if not isinstance(ts, str):
        raise ValueError(f"Invalid ISO timestamp: {ts}")

    try:
        # Primary: strict ISO parsing
        return datetime.fromisoformat(ts)
    except Exception:
        # Fallback: accept "Season Day, Year" e.g. "Summer 2, 1084"
        m = re.match(r"^(Spring|Summer|Autumn|Fall|Winter)\s+(\d+),\s*(\d+)$", ts, re.IGNORECASE)
        if m:
            season = m.group(1).lower()
            day = int(m.group(2))
            year = int(m.group(3))

            # Map seasons to canonical months (deterministic)
            # Spring -> March (3), Summer -> June (6), Autumn/Fall -> September (9), Winter -> December (12)
            month_map = {
                'spring': 3,
                'summer': 6,
                'autumn': 9,
                'fall': 9,
                'winter': 12
            }
            month = month_map.get(season, 1)

            # Clamp day to valid range (1..28) to avoid invalid dates
            if day < 1:
                day = 1
            elif day > 28:
                day = 28

            try:
                return datetime(year, month, day)
            except Exception:
                raise ValueError(f"Invalid game timestamp: {ts}")

    # If all parsing attempts fail, raise a clear error
    raise ValueError(f"Invalid ISO timestamp: {ts}")


def load_jsonl(path: Path):
    rows = []
    with path.open("r", encoding="utf-8") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError as e:
                raise ValueError(f"{path}:{line_no} invalid JSON: {e}")
    return rows


def validate_decisions(decisions):
    seen_ids = set()

    for i, d in enumerate(decisions):
        required = ["decisionId", "timestamp", "partyId", "decisionType"]
        for k in required:
            if k not in d:
                raise ValueError(f"Decision[{i}] missing field '{k}'")

        if d["decisionId"] in seen_ids:
            raise ValueError(f"Duplicate decisionId: {d['decisionId']}")
        seen_ids.add(d["decisionId"])

        parse_iso(d["timestamp"])

        # Context checks (Phase-2)
        if "context" in d and d["context"] is not None:
            ctx = d["context"]
            if not isinstance(ctx, dict):
                raise ValueError(f"Decision[{i}] context is not an object")

            # Mandatory metadata (Phase-2 contract)
            # Backwards-compatible: only enforce if any Phase-2 metadata key is present
            phase2_meta = [
                "contextSchemaVersion",
                "campaignAILabAssemblyVersion",
                "gameVersionString",
            ]

            # If any Phase-2 key is present, require all of them
            if any(k in ctx for k in phase2_meta):
                for meta in phase2_meta:
                    if meta not in ctx:
                        raise ValueError(
                            f"Decision[{i}] context missing '{meta}'"
                        )

    return seen_ids


def validate_outcomes(outcomes):
    for i, o in enumerate(outcomes):
        required = ["decisionId", "outcomeType", "resolutionTime"]
        for k in required:
            if k not in o:
                raise ValueError(f"Outcome[{i}] missing field '{k}'")

        parse_iso(o["resolutionTime"])

        if "durationHours" in o:
            if o["durationHours"] < 0:
                raise ValueError(
                    f"Outcome[{i}] has negative durationHours"
                )


def main():
    root = Path(".")
    decisions_path = root / DECISIONS_FILE
    outcomes_path = root / OUTCOMES_FILE

    if not decisions_path.exists():
        print("decisions.jsonl not found")
        sys.exit(1)

    decisions = load_jsonl(decisions_path)
    decision_ids = validate_decisions(decisions)

    if outcomes_path.exists():
        outcomes = load_jsonl(outcomes_path)
        validate_outcomes(outcomes)
    else:
        print("outcomes.jsonl not found (allowed)")

    print("? Log validation passed")
    print(f"  Decisions read: {len(decisions)}")
    if outcomes_path.exists():
        print(f"  Outcomes read: {len(outcomes)}")


if __name__ == "__main__":
    main()
