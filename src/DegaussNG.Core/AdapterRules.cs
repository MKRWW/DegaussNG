using System.Net.NetworkInformation;

namespace DegaussNG.Core;

/// <summary>
/// Real adapters only. Virtual, tunnel and loopback adapters never count as a connection.
/// </summary>
public static class AdapterRules
{
    private static readonly HashSet<NetworkInterfaceType> RealTypes = new()
    {
        NetworkInterfaceType.Ethernet,
        NetworkInterfaceType.GigabitEthernet,
        NetworkInterfaceType.FastEthernetT,
        NetworkInterfaceType.FastEthernetFx,
        NetworkInterfaceType.Wireless80211,
        NetworkInterfaceType.Wwanpp,
        NetworkInterfaceType.Wwanpp2,
    };

    private static readonly string[] ExcludedParts =
    {
        "virtual",
        "vethernet",
        "hyper-v",
        "vmware",
        "virtualbox",
        "wsl",
        "docker",
        "tap-",
        "tap adapter",
        "tunnel",
        "vpn",
        "wireguard",
        "tailscale",
        "zerotier",
        "loopback",
        "bluetooth",
        "npcap",
        "pseudo",
    };

    public static bool IsRealAdapter(NetworkInterfaceType type, string name, string description)
    {
        if (!RealTypes.Contains(type))
        {
            return false;
        }

        return !ContainsExcludedPart(name) && !ContainsExcludedPart(description);
    }

    private static bool ContainsExcludedPart(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (string part in ExcludedParts)
        {
            if (text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
