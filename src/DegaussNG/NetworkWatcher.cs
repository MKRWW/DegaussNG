using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using DegaussNG.Core;

namespace DegaussNG;

/// <summary>
/// Watches the network and reports one real connection. The state changes arrive on other threads,
/// so they are marshalled to the dispatcher.
/// </summary>
internal sealed class NetworkWatcher : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private const int ReportEveryTicks = 5;

    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private ConnectionDebouncer? _debouncer;
    private DispatcherTimer? _timer;
    private int _ticks;
    private bool _disposed;

    public event Action? Connected;

    public NetworkWatcher()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        _debouncer = new ConnectionDebouncer(IsConnected());

        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Normal, OnTick, _dispatcher);
        _timer.Start();
    }

    /// <summary>
    /// True when a real adapter is up and has a default gateway. An exception counts as not connected.
    /// </summary>
    public static bool IsConnected()
    {
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (!AdapterRules.IsRealAdapter(adapter.NetworkInterfaceType, adapter.Name, adapter.Description))
                {
                    continue;
                }

                if (HasDefaultGateway(adapter))
                {
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error("network state could not be read: " + exception.Message);
        }

        return false;
    }

    public static bool HasDefaultGateway(NetworkInterface adapter)
    {
        try
        {
            foreach (GatewayIPAddressInformation gateway in adapter.GetIPProperties().GatewayAddresses)
            {
                string address = gateway.Address.ToString();
                if (address != "0.0.0.0" && address != "::")
                {
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error("gateway of adapter " + adapter.Name + " could not be read: " + exception.Message);
        }

        return false;
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        ReportOnDispatcher();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        ReportOnDispatcher();
    }

    private void ReportOnDispatcher()
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.BeginInvoke(new Action(() => _debouncer?.Report(IsConnected(), _clock.Elapsed)));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        ConnectionDebouncer? debouncer = _debouncer;
        if (debouncer == null)
        {
            return;
        }

        _ticks++;

        if (_ticks % ReportEveryTicks == 0)
        {
            debouncer.Report(IsConnected(), _clock.Elapsed);
        }

        if (debouncer.Poll(_clock.Elapsed))
        {
            Connected?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

        if (_timer != null)
        {
            _timer.Tick -= OnTick;
            _timer.Stop();
        }
    }
}
