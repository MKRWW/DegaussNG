using System.Net.NetworkInformation;
using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class AdapterRulesTests
{
    [Fact]
    public void AWirelessAdapterCountsAsAConnection()
    {
        Assert.True(AdapterRules.IsRealAdapter(NetworkInterfaceType.Wireless80211, "Wi-Fi", "Intel(R) Wi-Fi 6 AX201"));
    }

    [Fact]
    public void APlainEthernetAdapterCountsAsAConnection()
    {
        Assert.True(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "Ethernet", "Realtek Gaming Family Controller"));
    }

    [Fact]
    public void EveryRealTypeIsAccepted()
    {
        foreach (NetworkInterfaceType type in new[]
        {
            NetworkInterfaceType.Ethernet,
            NetworkInterfaceType.GigabitEthernet,
            NetworkInterfaceType.FastEthernetT,
            NetworkInterfaceType.FastEthernetFx,
            NetworkInterfaceType.Wireless80211,
            NetworkInterfaceType.Wwanpp,
            NetworkInterfaceType.Wwanpp2,
        })
        {
            Assert.True(AdapterRules.IsRealAdapter(type, "Network Adapter", "Network Adapter"));
        }
    }

    [Fact]
    public void AnythingElseThanARealTypeIsRejected()
    {
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Loopback, "Local Area Connection", "Network Adapter"));
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ppp, "Dial-up", "Modem"));
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.TokenRing, "Token Ring", "Network Adapter"));
    }

    [Fact]
    public void AHyperVVirtualAdapterIsRejected()
    {
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter"));
    }

    [Fact]
    public void ATailscaleAdapterIsRejected()
    {
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "Tailscale", "Tailscale"));
    }

    [Fact]
    public void ABluetoothNetworkConnectionIsRejected()
    {
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "Bluetooth Network Connection", "Bluetooth Network Connection"));
    }

    [Fact]
    public void TheExcludedPartsAreFoundIgnoringCaseInEitherText()
    {
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "WIREGUARD", "Network Adapter"));
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "Network Adapter", "Docker Desktop Network"));
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.Wireless80211, "Wi-Fi", "VMware Virtual Adapter"));
        Assert.False(AdapterRules.IsRealAdapter(NetworkInterfaceType.GigabitEthernet, "npcap tap adapter", "Network Adapter"));
    }

    [Fact]
    public void ARealAdapterWithAnEmptyDescriptionIsAccepted()
    {
        Assert.True(AdapterRules.IsRealAdapter(NetworkInterfaceType.Ethernet, "Ethernet", string.Empty));
    }
}
