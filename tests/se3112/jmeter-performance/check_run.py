"""Shared readiness and sample checks for the EventPulse JMeter runners."""

import argparse
import csv
import json
import re
import sys
import time
from pathlib import Path
from urllib.error import URLError
from urllib.request import HTTPRedirectHandler, ProxyHandler, Request, build_opener


class RejectRedirects(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def reject_json_constant(value):
    raise ValueError(f"Invalid JSON constant: {value}")


def check_ready(args):
    if not re.fullmatch(r"[A-Za-z0-9.-]+", args.host):
        raise ValueError("Use a DNS hostname or IPv4 address for the target host.")
    if args.host.lower() not in {"localhost", "127.0.0.1"} and not args.allow_nonlocal:
        raise ValueError("Nonlocal target requires explicit opt-in after target-owner approval.")
    if not 1 <= args.port <= 65535:
        raise ValueError("Port must be between 1 and 65535.")
    if not re.fullmatch(r"/[A-Za-z0-9._~/%-]*", args.api_path) or args.api_path.startswith("//"):
        raise ValueError("API path must be a single-root relative URL path without query or fragment.")

    origin = f"{args.protocol}://{args.host}:{args.port}"
    opener = build_opener(ProxyHandler({}), RejectRedirects())
    deadline = time.monotonic() + args.wait_seconds
    while True:
        try:
            with opener.open(f"{origin}/health", timeout=3) as response:
                if response.status != 200:
                    raise ValueError(f"Gateway health returned HTTP {response.status}")
            request = Request(f"{origin}{args.api_path}", headers={"Accept": "application/json"})
            with opener.open(request, timeout=3) as response:
                if response.status != 200:
                    raise ValueError(f"Events API returned HTTP {response.status}")
                if response.headers.get_content_type().lower() != "application/json":
                    raise ValueError("Events API did not return Content-Type: application/json")
                events = json.load(response, parse_constant=reject_json_constant)
                if not isinstance(events, list):
                    raise ValueError("Events API did not return a JSON array")
            print(f"Gateway and Events API ready at {origin}{args.api_path} ({len(events)} events).")
            return
        except (OSError, URLError, ValueError) as error:
            if time.monotonic() >= deadline:
                raise ValueError(f"Readiness failed for {origin}{args.api_path}: {error}") from error
            time.sleep(1)


def check_results(args):
    total = failed = 0
    first_failure = None
    required = {"timeStamp", "elapsed", "label", "responseCode", "success"}
    with args.jtl.open(encoding="utf-8-sig", newline="") as result:
        rows = csv.DictReader(result, strict=True)
        fields = rows.fieldnames or []
        if not required.issubset(fields) or len(fields) != len(set(fields)) or "" in fields:
            raise ValueError("JTL has missing, empty or duplicate column names.")
        for sample in rows:
            if (None in sample or None in sample.values()
                    or sample["success"] not in {"true", "false"}
                    or not all(sample[key].isascii() and sample[key].isdigit()
                               for key in ("timeStamp", "elapsed"))
                    or not sample["label"].strip() or not sample["responseCode"].strip()):
                raise ValueError(f"Malformed JTL sample ending on line {rows.line_num}.")
            total += 1
            if sample["success"] != "true" or sample["responseCode"] != "200":
                failed += 1
                if first_failure is None:
                    message = sample.get("failureMessage") or sample.get("responseMessage", "")
                    first_failure = f'line {rows.line_num}, response {sample["responseCode"]}: {message[:240]}'
    if not total:
        raise ValueError("JTL contains no samples.")
    if failed:
        raise ValueError(f"{failed}/{total} samples failed. First failure: {first_failure}")
    if not args.dashboard.is_file() or args.dashboard.stat().st_size == 0:
        raise ValueError(f"HTML dashboard missing or empty: {args.dashboard}")
    print(f"{total} samples passed with zero unexpected failures.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    ready = commands.add_parser("ready")
    ready.add_argument("protocol", choices=("http", "https"))
    ready.add_argument("host")
    ready.add_argument("port", type=int)
    ready.add_argument("api_path")
    ready.add_argument("--allow-nonlocal", action="store_true")
    ready.add_argument("--wait-seconds", type=int, default=60)
    results = commands.add_parser("results")
    results.add_argument("jtl", type=Path)
    results.add_argument("dashboard", type=Path)
    args = parser.parse_args()
    try:
        if args.command == "ready":
            check_ready(args)
        else:
            check_results(args)
    except (OSError, ValueError, csv.Error) as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
