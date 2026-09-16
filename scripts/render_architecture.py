#!/usr/bin/env python3
"""Validate/render architecture pages without confusing design counts with evidence."""

import argparse
import json
from pathlib import Path
import subprocess
import sys


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--skill-dir",
        type=Path,
        default=Path.home() / ".claude/skills/system-diagrams",
        help="Installed system-diagrams skill directory (requires Python 3.12+).",
    )
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = root / "docs/architecture"
    model_path = output / "model.json"
    model = json.loads(model_path.read_text(encoding="utf-8"))

    for script, arguments in (
        ("validate_model.py", [str(model_path)]),
        ("render.py", [str(model_path), "-o", str(output)]),
    ):
        subprocess.run(
            [sys.executable, str(args.skill_dir / "scripts" / script), *arguments],
            cwd=root,
            check=True,
        )

    # The upstream template counts definitions, not implemented components or
    # passing verifications. Keep this project-specific correction reproducible.
    trace_path = output / "traceability.html"
    trace = trace_path.read_text(encoding="utf-8")
    components = sum(node.get("level", 0) >= 2 for node in model["nodes"])
    verifications = model["verifications"]
    passing = sum(item.get("status") == "passing" for item in verifications)
    replacements = {
        f"{components} component(s) built</text>":
            f"{components} component(s) specified</text>",
        "covered by a verification</span>": "mapped to a verification</span>",
        "No gaps: every requirement is allocated and verified, and every component traces back to a requirement.":
            "No traceability gaps: every requirement has an owner and a verification definition, and every component traces back to a requirement.",
        "<th>Verified by</th>": "<th>Verification</th>",
        "<h2>Coverage</h2>":
            f"<h2>Coverage</h2><p>Verification evidence: {passing}/{len(verifications)} passing. Definition coverage below is not a pass rate.</p>",
    }
    # The no-gaps note is legitimately absent when the model contains gaps.
    optional_note = "No gaps: every requirement is allocated and verified, and every component traces back to a requirement."
    for original, replacement in replacements.items():
        count = trace.count(original)
        if count != 1 and not (original == optional_note and count == 0):
            raise RuntimeError(f"Diagram template changed; review evidence label: {original}")
        trace = trace.replace(original, replacement)
    trace_path.write_text(trace, encoding="utf-8")
    print(f"Evidence labels corrected: {passing}/{len(verifications)} verifications passing.")


if __name__ == "__main__":
    main()
