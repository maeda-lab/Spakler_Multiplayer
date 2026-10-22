"""Summarize one schema-2 probe JSON and its 1Hz CSV. No cross-device clock subtraction."""
import argparse
import csv
import json
import statistics
from pathlib import Path


def percentile(values, fraction):
    if not values:
        return None
    ordered = sorted(values)
    position = (len(ordered) - 1) * fraction
    lower = int(position)
    upper = min(lower + 1, len(ordered) - 1)
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (position - lower)


def analyze(path, minimum_connected=0, expect_timeout=False, require_connected=False):
    report = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    with Path(str(path) + ".metrics.csv").open(encoding="utf-8-sig", newline="") as source:
        rows = list(csv.DictReader(source))
    errors = []
    if not report.get("connected") or not report.get("sawTwoPlayers") or report.get("observedReceivedSnapshots", 0) <= 0:
        errors.append("No verified two-player traffic")
    if report.get("schemaVersion") != 2 or not report.get("completedNormally"):
        errors.append("Incomplete or old-schema report")
    if report.get("elapsedSeconds", 0) < report.get("requestedDurationSeconds", 0) - .1:
        errors.append("Run shorter than requested")
    if report.get("metricsError") or not rows:
        errors.append("Missing or failed metrics")
    if report.get("finalWorkingSetBytes", 0) <= 0 or report.get("finalPrivateBytes", 0) <= 0:
        errors.append("Process memory measurements are unavailable")
    if report.get("connectedSeconds", 0) < minimum_connected:
        errors.append("Insufficient connected time")
    if require_connected and report.get("finalStatus") not in ("Host", "Connected"):
        errors.append("Final session is disconnected")
    if report.get("timeoutVisibleViolations", 0) or report.get("timeoutInvalidPoseViolations", 0):
        errors.append("Pose or visual remained valid during receive timeout")
    if expect_timeout and not (report.get("receiveTimeoutCount", 0) > 0 and
                               report.get("timeoutRecoveries", 0) > 0 and
                               report.get("timeoutVisualChecks", 0) > 0 and
                               report.get("timeoutVisualChecks") == report.get("timeoutHiddenSamples")):
        errors.append("Timeout/hide/recovery assertions missing")
    rates, rtts = [], []
    for before, after in zip(rows, rows[1:]):
        elapsed = float(after["elapsed_seconds"]) - float(before["elapsed_seconds"])
        received = int(after["received_snapshots"]) - int(before["received_snapshots"])
        if elapsed <= 0 or received < 0:
            errors.append("Non-monotonic CSV time or cumulative receive counter")
        if elapsed > 0 and int(before["players"]) >= 2 and int(after["players"]) >= 2:
            rates.append(received / elapsed)
    for row in rows:
        if (int(row["players"]) >= 2 and row["rtt_timed_out"].lower() == "false" and
                float(row["smoothed_rtt_ms"]) > 0):
            rtts.append(float(row["smoothed_rtt_ms"]))
    summary = dict(report=str(path), machine=report.get("machineName"), passed=not errors, errors=errors,
                   actualSeconds=report.get("elapsedSeconds"), connectedSeconds=report.get("connectedSeconds"),
                   csvSamples=len(rows), receiveHzMedian=percentile(rates, .5), receiveHzP05=percentile(rates, .05),
                   sampledSmoothedRttMedianMs=percentile(rtts, .5), sampledSmoothedRttP95Ms=percentile(rtts, .95),
                   receiveTimeouts=report.get("receiveTimeoutCount"), recoveries=report.get("timeoutRecoveries"),
                   longestTimeoutSeconds=report.get("longestReceiveTimeoutSeconds"), finalStatus=report.get("finalStatus"))
    # Resource deltas are observations, never an automatic leak/no-leak verdict.
    stable = [row for row in rows if float(row["elapsed_seconds"]) >= 10]
    if len(stable) >= 10:
        window = min(10, len(stable) // 2)
        for field in ("working_set_bytes", "private_bytes", "managed_bytes", "unity_allocated_bytes"):
            summary[field + "MedianDelta"] = (statistics.median(int(row[field]) for row in stable[-window:]) -
                                               statistics.median(int(row[field]) for row in stable[:window]))
        elapsed = float(stable[-1]["elapsed_seconds"]) - float(stable[0]["elapsed_seconds"])
        if elapsed > 0:
            summary["averageCpuCoreEquivalent"] = (float(stable[-1]["cpu_seconds"]) - float(stable[0]["cpu_seconds"])) / elapsed
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report")
    parser.add_argument("--minimum-connected", type=float, default=0)
    parser.add_argument("--expect-timeout", action="store_true")
    parser.add_argument("--require-connected", action="store_true")
    parser.add_argument("--output")
    args = parser.parse_args()
    summary = analyze(args.report, args.minimum_connected, args.expect_timeout, args.require_connected)
    content = json.dumps(summary, indent=2)
    if args.output:
        Path(args.output).write_text(content, encoding="utf-8")
    print(content)
    raise SystemExit(0 if summary["passed"] else 1)


if __name__ == "__main__":
    main()
