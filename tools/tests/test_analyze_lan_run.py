import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("analysis", Path(__file__).parents[1] / "analyze_lan_run.py")
analysis = importlib.util.module_from_spec(spec)
spec.loader.exec_module(analysis)


class AnalysisTests(unittest.TestCase):
    def make_report(self, directory, **changes):
        report = dict(schemaVersion=2, completedNormally=True, connected=True, sawTwoPlayers=True,
                      observedReceivedSnapshots=20, elapsedSeconds=30, requestedDurationSeconds=30,
                      connectedSeconds=29, finalStatus="Connected", receiveTimeoutCount=1,
                      timeoutRecoveries=1, timeoutVisualChecks=10, timeoutHiddenSamples=10,
                      finalWorkingSetBytes=100000, finalPrivateBytes=100000)
        report.update(changes)
        path = Path(directory) / "run.json"
        path.write_text(json.dumps(report), encoding="utf-8")
        Path(str(path) + ".metrics.csv").write_text(
            "elapsed_seconds,players,received_snapshots,rtt_timed_out,smoothed_rtt_ms\n"
            "1,2,0,False,80\n2,2,20,False,100\n3,2,20,True,100\n", encoding="utf-8")
        return path

    def test_completed_run_summary_ignores_rtt_timeout_sample(self):
        with tempfile.TemporaryDirectory() as directory:
            summary = analysis.analyze(self.make_report(directory), 25, True, True)
            self.assertTrue(summary["passed"])
            self.assertEqual(summary["receiveHzMedian"], 10)
            self.assertEqual(summary["sampledSmoothedRttP95Ms"], 99)

    def test_historical_connected_flag_cannot_pass_soak_after_disconnect(self):
        with tempfile.TemporaryDirectory() as directory:
            path = self.make_report(directory, finalStatus="Disconnected", connectedSeconds=10)
            summary = analysis.analyze(path, 25, True, True)
            self.assertFalse(summary["passed"])
            self.assertIn("Final session is disconnected", summary["errors"])
            self.assertIn("Insufficient connected time", summary["errors"])

    def test_incomplete_report_or_visible_pose_during_timeout_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            summary = analysis.analyze(self.make_report(directory, completedNormally=False, timeoutVisibleViolations=1))
            self.assertFalse(summary["passed"])
            self.assertEqual(len(summary["errors"]), 2)

    def test_zero_memory_measurements_are_not_accepted_as_stable_memory(self):
        with tempfile.TemporaryDirectory() as directory:
            summary = analysis.analyze(self.make_report(directory, finalWorkingSetBytes=0))
            self.assertFalse(summary["passed"])
            self.assertIn("Process memory measurements are unavailable", summary["errors"])


if __name__ == "__main__":
    unittest.main()
