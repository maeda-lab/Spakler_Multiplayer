"""Opt-in single-client UDP test relay. Only forwards to a loopback target.

No packet payload is logged. Seeded decisions are reproducible for the same
per-direction packet order; OS scheduling and real LAN traffic are not replayed.
"""
import argparse
import hashlib
import heapq
import ipaddress
import json
import random
import selectors
import socket
import time
from pathlib import Path


class FaultPolicy:
    def __init__(self, seed, loss_percent=0, delay_ms=0, jitter_ms=0, blackouts=()):
        self.random = {"c2s": random.Random(seed), "s2c": random.Random(seed ^ 0x9E3779B9)}
        self.loss = loss_percent / 100
        self.delay = delay_ms / 1000
        self.jitter = jitter_ms / 1000
        self.blackouts = tuple(blackouts)

    def blocked(self, elapsed):
        return any(start <= elapsed < start + duration for start, duration in self.blackouts)

    def decide(self, direction, elapsed):
        if self.blocked(elapsed):
            return "blackout", 0
        rng = self.random[direction]
        if rng.random() < self.loss:
            return "random", 0
        return "forward", max(0, self.delay + rng.uniform(-self.jitter, self.jitter))


class Relay:
    def __init__(self, listen, target, allowed_client, policy, report_path, duration):
        if not ipaddress.ip_address(target[0]).is_loopback:
            raise ValueError("Target must be a loopback IPv4; this is not an open relay")
        self.policy, self.target, self.allowed = policy, target, allowed_client
        self.path, self.duration = Path(report_path), duration
        self.started, self.traffic_started = time.monotonic(), None
        self.client = None
        self.queue, self.queue_bytes, self.sequence = [], 0, 0
        self.digest = hashlib.sha256()
        self.front = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.back = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.front.bind(listen)
        self.back.bind(("127.0.0.1", 0))
        self.front.setblocking(False)
        self.back.setblocking(False)
        self.selector = selectors.DefaultSelector()
        self.selector.register(self.front, selectors.EVENT_READ, "c2s")
        self.selector.register(self.back, selectors.EVENT_READ, "s2c")
        self.report = dict(listen=self.front.getsockname(), target=target, allowedClient=allowed_client,
                           startedUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                           completedNormally=False, rejected=0, peakQueueDatagrams=0, peakQueueBytes=0,
                           directions={key: dict(received=0, forwarded=0, randomDropped=0,
                                                blackoutDropped=0, overflowDropped=0, shutdownDropped=0,
                                                sendErrors=0, forwardedBytes=0)
                                       for key in ("c2s", "s2c")})
        self.report["blackouts"] = [dict(startSeconds=start, durationSeconds=duration,
                                         observedStartSeconds=None, observedEndSeconds=None,
                                         c2sDropped=0, s2cDropped=0)
                                    for start, duration in policy.blackouts]
        self.next_report = self.started

    def elapsed(self, now):
        return 0 if self.traffic_started is None else now - self.traffic_started

    def accept(self, direction, data, now):
        stats = self.report["directions"][direction]
        stats["received"] += 1
        decision, delay = self.policy.decide(direction, self.elapsed(now))
        self.digest.update(f"{direction}:{stats['received']}:{len(data)}:{decision}:{delay:.6f}\n".encode())
        if decision != "forward":
            stats["blackoutDropped" if decision == "blackout" else "randomDropped"] += 1
            if decision == "blackout":
                self.record_blackout_drop(direction, now)
            return
        if len(self.queue) >= 4096 or self.queue_bytes + len(data) > 8 * 1024 * 1024:
            stats["overflowDropped"] += 1
            return
        self.sequence += 1
        heapq.heappush(self.queue, (now + delay, self.sequence, direction, data))
        self.queue_bytes += len(data)
        self.report["peakQueueDatagrams"] = max(self.report["peakQueueDatagrams"], len(self.queue))
        self.report["peakQueueBytes"] = max(self.report["peakQueueBytes"], self.queue_bytes)

    def release(self, now):
        while self.queue and self.queue[0][0] <= now:
            _, _, direction, data = heapq.heappop(self.queue)
            self.queue_bytes -= len(data)
            stats = self.report["directions"][direction]
            if self.policy.blocked(self.elapsed(now)):
                stats["blackoutDropped"] += 1
                self.record_blackout_drop(direction, now)
                continue
            channel, address = (self.back, self.target) if direction == "c2s" else (self.front, self.client)
            try:
                channel.sendto(data, address)
                stats["forwarded"] += 1
                stats["forwardedBytes"] += len(data)
            except OSError:
                stats["sendErrors"] += 1

    def record_blackout_drop(self, direction, now):
        for window in self.report["blackouts"]:
            if window["startSeconds"] <= self.elapsed(now) < window["startSeconds"] + window["durationSeconds"]:
                window[direction + "Dropped"] += 1
                break

    def step(self, timeout=0.02):
        now = time.monotonic()
        if self.queue:
            timeout = min(timeout, max(0, self.queue[0][0] - now))
        for key, _ in self.selector.select(timeout):
            # Bound work per socket so a busy sender cannot starve delivery/reporting.
            for _ in range(256):
                try:
                    data, address = key.fileobj.recvfrom(65535)
                except BlockingIOError:
                    break
                except ConnectionResetError:
                    # Windows UDP ICMP response after target stops; retain diagnostics.
                    self.report["rejected"] += 1
                    break
                if key.data == "c2s":
                    if address[0] != self.allowed or (self.client and address != self.client):
                        self.report["rejected"] += 1
                        continue
                    if self.client is None:
                        self.client, self.traffic_started = address, time.monotonic()
                        self.report["client"] = address
                        self.report["firstClientDatagramUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
                elif address != self.target or self.client is None:
                    self.report["rejected"] += 1
                    continue
                self.accept(key.data, data, time.monotonic())
        self.release(time.monotonic())
        if self.traffic_started is not None:
            elapsed = self.elapsed(time.monotonic())
            for window in self.report["blackouts"]:
                if elapsed >= window["startSeconds"] and window["observedStartSeconds"] is None:
                    window["observedStartSeconds"] = elapsed
                if elapsed >= window["startSeconds"] + window["durationSeconds"] and window["observedEndSeconds"] is None:
                    window["observedEndSeconds"] = elapsed
        if time.monotonic() >= self.next_report:
            self.next_report = time.monotonic() + 1
            self.write()

    def write(self):
        self.report["elapsedSeconds"] = time.monotonic() - self.started
        self.report["trafficSeconds"] = self.elapsed(time.monotonic())
        self.report["queuedDatagrams"] = len(self.queue)
        self.report["queuedBytes"] = self.queue_bytes
        self.report["decisionSha256"] = self.digest.hexdigest()
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.path.with_suffix(self.path.suffix + ".tmp")
        temporary.write_text(json.dumps(self.report, indent=2), encoding="utf-8")
        temporary.replace(self.path)

    def close(self, completed=False):
        self.report["completedNormally"] = completed
        for _, _, direction, _ in self.queue:
            self.report["directions"][direction]["shutdownDropped"] += 1
        self.queue.clear()
        self.queue_bytes = 0
        self.write()
        self.selector.close()
        self.front.close()
        self.back.close()


def endpoint(value):
    address, port = value.rsplit(":", 1)
    ipaddress.IPv4Address(address)
    port = int(port)
    if not 1 <= port <= 65535:
        raise ValueError("Port must be 1-65535")
    return address, port


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--listen", default="127.0.0.1:17778", type=endpoint)
    parser.add_argument("--target", default="127.0.0.1:17777", type=endpoint)
    parser.add_argument("--allow-client", default="127.0.0.1", type=lambda value: str(ipaddress.IPv4Address(value)))
    parser.add_argument("--seed", type=int, default=20261021)
    parser.add_argument("--loss-percent", type=float, default=0)
    parser.add_argument("--delay-ms", type=float, default=0)
    parser.add_argument("--jitter-ms", type=float, default=0)
    parser.add_argument("--blackout", action="append", default=[], help="start:duration seconds from first accepted client datagram; repeatable")
    parser.add_argument("--duration", type=float, default=120, help="wall-clock seconds, including waiting for a client")
    parser.add_argument("--report", required=True)
    args = parser.parse_args()
    if not (0 <= args.loss_percent <= 100 and 0 <= args.delay_ms <= 5000 and
            0 <= args.jitter_ms <= 5000 and 0 < args.duration <= 14400):
        parser.error("Loss must be 0-100, delay/jitter 0-5000ms, duration 0-14400s")
    try:
        blackouts = [tuple(map(float, item.split(":"))) for item in args.blackout]
        if any(len(pair) != 2 or not (0 <= pair[0] <= 14400 and 0 < pair[1] <= 14400) for pair in blackouts):
            raise ValueError("invalid blackout range")
        relay = Relay(args.listen, args.target, args.allow_client,
                      FaultPolicy(args.seed, args.loss_percent, args.delay_ms, args.jitter_ms, blackouts),
                      args.report, args.duration)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    relay.report["configuration"] = vars(args)
    print(f"UDP relay {args.listen} -> {args.target}; only {args.allow_client}; single client endpoint", flush=True)
    completed = False
    try:
        while time.monotonic() - relay.started < args.duration:
            relay.step()
        completed = True
    except KeyboardInterrupt:
        pass
    finally:
        relay.close(completed)


if __name__ == "__main__":
    main()
