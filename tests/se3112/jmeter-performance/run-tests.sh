#!/usr/bin/env bash
# =============================================================================
# run-tests.sh  —  EventPulse JMeter Load Test Runner
# SE3112 Load & Performance Testing — Member 3
#
# Usage:
#   ./run-tests.sh [profile]
#
# Profiles:
#   baseline   (default)  5 VU / 10 s ramp / 30 s duration
#   normal                20 VU / 20 s ramp / 30 s duration
#   higher                50 VU / 30 s ramp / 30 s duration
#
# Environment variables (optional overrides):
#   JMETER_HOME  — path to JMeter installation (default: looks for 'jmeter' on PATH)
#   TARGET_HOST  — API Gateway hostname (default: localhost)
#   TARGET_PORT  — API Gateway port    (default: 7000)
#   TARGET_PROTO — http or https       (default: http)
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLAN_FILE="${SCRIPT_DIR}/eventpulse-events-load-test.jmx"
RESULTS_DIR="${SCRIPT_DIR}/results"
REPORTS_DIR="${SCRIPT_DIR}/reports"

PROFILE="${1:-baseline}"

# ── Workload profiles ─────────────────────────────────────────────────────────
case "${PROFILE}" in
  baseline) USERS=5;  RAMPUP=10; DURATION=30; THINK=500  ;;
  normal)   USERS=20; RAMPUP=20; DURATION=30; THINK=500  ;;
  higher)   USERS=50; RAMPUP=30; DURATION=30; THINK=300  ;;
  *)
    echo "ERROR: Unknown profile '${PROFILE}'. Use: baseline | normal | higher"
    exit 1
    ;;
esac

# ── Target server ─────────────────────────────────────────────────────────────
HOST="${TARGET_HOST:-localhost}"
PORT="${TARGET_PORT:-7000}"
PROTO="${TARGET_PROTO:-http}"

# ── Locate JMeter ─────────────────────────────────────────────────────────────
if [[ -n "${JMETER_HOME:-}" ]]; then
  JMETER="${JMETER_HOME}/bin/jmeter"
else
  JMETER="$(command -v jmeter 2>/dev/null || true)"
fi

if [[ -z "${JMETER}" || ! -x "${JMETER}" ]]; then
  echo "ERROR: 'jmeter' not found. Set JMETER_HOME or add JMeter to PATH."
  exit 2
fi

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
JTL_FILE="${RESULTS_DIR}/${PROFILE}-${TIMESTAMP}.jtl"
REPORT_DIR="${REPORTS_DIR}/${PROFILE}-${TIMESTAMP}"

mkdir -p "${RESULTS_DIR}" "${REPORTS_DIR}"

echo "============================================================"
echo "  EventPulse JMeter Load Test"
echo "  Profile  : ${PROFILE}"
echo "  Target   : ${PROTO}://${HOST}:${PORT}"
echo "  VUsers   : ${USERS}"
echo "  Ramp-up  : ${RAMPUP}s"
echo "  Duration : ${DURATION}s"
echo "  Think    : ${THINK}ms"
echo "  JTL      : ${JTL_FILE}"
echo "  Report   : ${REPORT_DIR}"
echo "============================================================"

# ── Health-check gateway before running test ──────────────────────────────────
echo "Health-checking ${PROTO}://${HOST}:${PORT}/api/events ..."
MAX_RETRIES=10
for i in $(seq 1 $MAX_RETRIES); do
  if curl -sf --max-time 5 "${PROTO}://${HOST}:${PORT}/api/events" -o /dev/null; then
    echo "  [OK] Gateway is responding."
    break
  fi
  if [[ $i -eq $MAX_RETRIES ]]; then
    echo "  [WARN] Gateway did not respond after ${MAX_RETRIES} attempts — proceeding anyway."
  fi
  echo "  Attempt $i/$MAX_RETRIES — waiting 5s..."
  sleep 5
done

# ── Execute JMeter ────────────────────────────────────────────────────────────
"${JMETER}" \
  -n \
  -t "${PLAN_FILE}" \
  -Jprotocol="${PROTO}" \
  -Jhost="${HOST}" \
  -Jport="${PORT}" \
  -Jusers="${USERS}" \
  -Jrampup="${RAMPUP}" \
  -Jduration="${DURATION}" \
  -Jthink_time_ms="${THINK}" \
  -l "${JTL_FILE}" \
  -e \
  -o "${REPORT_DIR}" \
  -j "${RESULTS_DIR}/jmeter-${PROFILE}-${TIMESTAMP}.log"

echo ""
echo "============================================================"
echo "  Test complete!"
echo "  JTL results : ${JTL_FILE}"
echo "  HTML report : ${REPORT_DIR}/index.html"
echo "============================================================"