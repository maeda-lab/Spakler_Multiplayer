using System.Net;
using NUnit.Framework;
using QuestLAN.Integration;

namespace QuestLAN.EditorTests
{
    public sealed class LocalIPv4Tests
    {
        [Test]
        public void SelectsRoutableLanOverVirtualAndRejectsLoopbackOrLinkLocal()
        {
            Assert.That(LocalIPv4Detector.Score(IPAddress.Parse("172.20.10.4"), true, true),
                Is.GreaterThan(LocalIPv4Detector.Score(IPAddress.Parse("192.168.56.1"), false, false)));
            Assert.That(LocalIPv4Detector.Score(IPAddress.Loopback, true, true), Is.EqualTo(-1));
            Assert.That(LocalIPv4Detector.Score(IPAddress.Parse("169.254.1.2"), true, true), Is.EqualTo(-1));
            Assert.That(LocalIPv4Detector.Score(IPAddress.IPv6Loopback, true, true), Is.EqualTo(-1));
            Assert.That(IPAddress.TryParse(LocalIPv4Detector.Detect(), out var address), Is.True);
        }
    }
}
