using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace QuestLAN.Integration
{
    public static class LocalIPv4Detector
    {
        public static int Score(IPAddress address, bool gateway, bool physical)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) return -1;
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 0 || bytes[0] >= 224 || (bytes[0] == 169 && bytes[1] == 254)) return -1;
            bool local = bytes[0] == 10 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
            return (gateway ? 100 : 0) + (physical ? 20 : 0) + (local ? 10 : 0);
        }
        public static string Detect()
        {
            string best = "127.0.0.1"; int bestScore = -1;
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    try
                    {
                        var properties = adapter.GetIPProperties(); bool gateway = false;
                        foreach (var item in properties.GatewayAddresses)
                            gateway |= item.Address.AddressFamily == AddressFamily.InterNetwork && !item.Address.Equals(IPAddress.Any);
                        string name = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
                        bool physical = (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                            !name.Contains("virtual") && !name.Contains("vpn") && !name.Contains("tap");
                        foreach (var item in properties.UnicastAddresses)
                        {
                            int score = Score(item.Address, gateway, physical);
                            if (score > bestScore) { bestScore = score; best = item.Address.ToString(); }
                        }
                    }
                    catch (NetworkInformationException) { }
                }
            }
            catch (NetworkInformationException) { }
            return best;
        }
    }
}
