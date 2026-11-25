#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
profile="${1:-baseline}"
if (( $# > 1 )); then
  echo "Usage: bash run-tests.sh [smoke|baseline|normal|higher]" >&2
  exit 2
fi
case "$profile" in
  smoke)    default_users=1;  default_ramp=1;  default_duration=10 ;;
  baseline) default_users=5;  default_ramp=10; default_duration=30 ;;
  normal)   default_users=20; default_ramp=20; default_duration=30 ;;
  higher)   default_users=50; default_ramp=30; default_duration=30 ;;
  *) echo "Unknown JMeter profile: $profile" >&2; exit 2 ;;
esac

protocol="${TARGET_PROTO:-http}"
host="${TARGET_HOST:-localhost}"
port="${TARGET_PORT:-7000}"
api_path="${API_PATH:-/api/events}"
users="${USERS:-$default_users}"
rampup="${RAMPUP:-$default_ramp}"
duration="${DURATION:-$default_duration}"
think_time="${THINK_TIME_MS:-500}"
python="${PYTHON_BIN:-python3}"
output_root="${OUTPUT_ROOT:-$script_dir/results}"

for number in "$users" "$rampup" "$duration" "$think_time"; do
  if [[ ! "$number" =~ ^(0|[1-9][0-9]{0,9})$ ]] || (( number > 2147483647 )); then
    echo "Workload overrides must be whole numbers between 0 and 2147483647." >&2
    exit 2
  fi
done
if (( users == 0 || duration == 0 || think_time == 0 )); then
  echo "USERS, DURATION and THINK_TIME_MS must be positive." >&2
  exit 2
fi

if [[ -n "${JMETER_HOME:-}" ]]; then
  jmeter="$JMETER_HOME/bin/jmeter"
else
  jmeter="$(command -v jmeter || true)"
fi
if [[ ! -x "$jmeter" ]]; then
  echo "JMeter not found. Set JMETER_HOME or add JMeter/bin to PATH." >&2
  exit 2
fi

ready_options=()
if [[ "${ALLOW_NONLOCAL:-0}" == 1 ]]; then
  ready_options+=(--allow-nonlocal)
fi
"$python" "$script_dir/check_run.py" ready "$protocol" "$host" "$port" "$api_path" "${ready_options[@]}"

mkdir -p -- "$output_root"
output_root="$(cd -- "$output_root" && pwd)"
output="$(mktemp -d "$output_root/$profile-$(date +%Y%m%d_%H%M%S)-XXXXXX")"
echo "$profile on $host port $port with $users users, ${rampup}s ramp, ${duration}s scheduled, ${think_time}ms pacing."
echo "Artifacts: $output"

jmeter_exit=0
"$jmeter" -n -t "$script_dir/eventpulse-events-load-test.jmx" \
  "-Jprotocol=$protocol" "-Jhost=$host" "-Jport=$port" "-Japi_path=$api_path" \
  "-Jusers=$users" "-Jrampup=$rampup" "-Jduration=$duration" "-Jthink_time_ms=$think_time" \
  -Jjmeter.save.saveservice.output_format=csv -Jjmeter.save.saveservice.print_field_names=true \
  -Jjmeter.save.saveservice.default_delimiter=, -Jjmeter.save.saveservice.timestamp_format=ms \
  -l "$output/results.jtl" -e -o "$output/html" -j "$output/jmeter.log" || jmeter_exit=$?
if (( jmeter_exit != 0 )); then
  echo "JMeter exited with code $jmeter_exit. Available artifacts: $output" >&2
  exit "$jmeter_exit"
fi

"$python" "$script_dir/check_run.py" results "$output/results.jtl" "$output/html/index.html"
