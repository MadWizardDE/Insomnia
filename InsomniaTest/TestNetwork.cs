using Microsoft.WindowsAPICodePack.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;

namespace InsomniaTest
{
    class TestNetwork
    {
        public void Test()
        {
            static NetworkInterface FindNIC(string adapterId)
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string nicId = nic.Id.Replace("{", "").Replace("}", "");
                    if (nicId.Equals(adapterId, StringComparison.InvariantCultureIgnoreCase))
                        return nic;
                }

                return null;
            }
            static bool IsNetworkWireless(Microsoft.WindowsAPICodePack.Net.Network net)
            {
                foreach (var con in net.Connections)
                {
                    var nic = FindNIC(con.AdapterId.ToString());

                    if (nic != null && nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        return true;
                }

                return false;
            }

            foreach (var net in NetworkListManager.GetNetworks(NetworkConnectivityLevels.Connected))
            {
                Console.WriteLine($"Network[{net.Name}] -> {(IsNetworkWireless(net) ? "wireless" : "wired")}");
            }

            Console.ReadKey();
        }
    }
}
