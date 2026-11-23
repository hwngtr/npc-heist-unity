#!/usr/bin/env bash
# Runs the EditMode suite in Unity batch mode and reports pass or fail.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY="${UNITY:-/home/hwngtr/Unity/Hub/Editor/6000.6.10f1/Editor/Unity}"
RESULTS="$ROOT/Tools/test-results.xml"
LOG="$ROOT/Tools/test-run.log"

mkdir -p "$ROOT/Tools"
rm -f "$RESULTS"

timeout 1800 "$UNITY" \
  -batchmode \
  -nographics \
  -projectPath "$ROOT" \
  -runTests \
  -testPlatform EditMode \
  -testResults "$RESULTS" \
  -logFile "$LOG"
unity_status=$?

if [[ ! -f "$RESULTS" ]]; then
  echo "BLOCKED: Unity produced no test results (exit $unity_status)."
  if grep -qiE "no valid unity editor license|license is not active|failed to activate license|failed to acquire license" "$LOG" 2>/dev/null; then
    echo "BLOCKED: license"
  fi
  echo "--- log tail ---"
  tail -n 40 "$LOG" 2>/dev/null
  exit 2
fi

python3 - "$RESULTS" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
try:
    root = ET.parse(path).getroot()
except ET.ParseError as exc:
    print(f"BLOCKED: unreadable test results at {path}: {exc}")
    sys.exit(2)

run = root if root.tag == "test-run" else root.find(".//test-run")
if run is None:
    print("BLOCKED: test results contain no test-run element")
    sys.exit(2)

def number(name):
    try:
        return int(run.get(name, "0") or 0)
    except ValueError:
        return 0

total = number("total")
passed = number("passed")
failed = number("failed")
skipped = number("skipped") + number("inconclusive")
print(f"SUMMARY total={total} passed={passed} failed={failed} skipped={skipped}")

failed_cases = []
for case in root.iter("test-case"):
    if case.get("result") == "Failed":
        failed_cases.append(case.get("fullname") or case.get("name") or "<unnamed>")
for name in failed_cases:
    print(f"FAILED {name}")

sys.exit(1 if failed > 0 else 0)
PY
summary_status=$?

if (( summary_status != 0 )); then
  exit "$summary_status"
fi
exit 0
