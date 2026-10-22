import importlib.util
import socket
import tempfile
import time
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location("proxy", Path(__file__).parents[1] / "lan_fault_proxy.py")
proxy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(proxy)


class FaultTests(unittest.TestCase):
    def test_seed_reproduces_decisions_and_directions_are_independent(self):
        a, b = proxy.FaultPolicy(42, 20, 40, 20), proxy.FaultPolicy(42, 20, 40, 20)
        decisions = [a.decide("c2s", i) for i in range(200)]
        for i in range(200):
            b.decide("s2c", i)
            self.assertEqual(decisions[i], b.decide("c2s", i))
        self.assertTrue(any(item[0] == "random" for item in decisions))
        self.assertTrue(all(.02 <= item[1] <= .06 for item in decisions if item[0] == "forward"))

    def test_blackout_boundaries_and_total_loss(self):
        policy = proxy.FaultPolicy(42, blackouts=[(20, .2), (30, 1.5)])
        self.assertEqual(policy.decide("c2s", 20)[0], "blackout")
        self.assertEqual(policy.decide("s2c", 20.1999)[0], "blackout")
        self.assertEqual(policy.decide("c2s", 20.2)[0], "forward")
        self.assertEqual(proxy.FaultPolicy(1, 100).decide("c2s", 0)[0], "random")

    def test_loopback_roundtrip_rejects_second_endpoint_and_accounts_queue(self):
        with tempfile.TemporaryDirectory() as directory:
            server = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            stranger = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            server.bind(("127.0.0.1", 0))
            server.settimeout(1)
            client.settimeout(1)
            relay = proxy.Relay(("127.0.0.1", 0), server.getsockname(), "127.0.0.1",
                                proxy.FaultPolicy(1), Path(directory) / "report.json", 2)
            try:
                client.sendto(b"request", relay.front.getsockname())
                relay.step(.1)
                data, address = server.recvfrom(100)
                server.sendto(data + b"-reply", address)
                relay.step(.1)
                self.assertEqual(client.recvfrom(100)[0], b"request-reply")
                stranger.sendto(b"not this flow", relay.front.getsockname())
                relay.step(.1)
                self.assertEqual(relay.report["rejected"], 1)
                relay.policy.delay = 20
                for _ in range(4100):
                    relay.accept("c2s", b"x", time.monotonic())
                self.assertEqual(len(relay.queue), 4096)
                self.assertEqual(relay.report["directions"]["c2s"]["overflowDropped"], 4)
            finally:
                relay.close(True)
                server.close()
                client.close()
                stranger.close()
            for stats in relay.report["directions"].values():
                self.assertEqual(stats["received"], sum(stats[key] for key in
                    ("forwarded", "randomDropped", "blackoutDropped", "overflowDropped", "shutdownDropped", "sendErrors")))

    def test_cannot_relay_to_other_machine(self):
        with self.assertRaises(ValueError):
            proxy.Relay(("127.0.0.1", 0), ("172.20.10.6", 17777), "127.0.0.1", proxy.FaultPolicy(1), "unused", 1)

    def test_queued_packet_is_dropped_when_delivery_falls_in_blackout(self):
        with tempfile.TemporaryDirectory() as directory:
            relay = proxy.Relay(("127.0.0.1", 0), ("127.0.0.1", 17777), "127.0.0.1",
                                proxy.FaultPolicy(1, delay_ms=150, blackouts=[(.1, 1)]),
                                Path(directory) / "report.json", 1)
            try:
                relay.traffic_started = 100
                relay.accept("c2s", b"queued", 100)
                relay.release(100.2)
                self.assertEqual(relay.queue_bytes, 0)
                self.assertEqual(relay.report["directions"]["c2s"]["blackoutDropped"], 1)
                self.assertEqual(relay.report["blackouts"][0]["c2sDropped"], 1)
            finally:
                relay.close(True)


if __name__ == "__main__":
    unittest.main()
