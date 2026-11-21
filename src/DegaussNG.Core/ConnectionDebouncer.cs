namespace DegaussNG.Core;

/// <summary>
/// Turns the flicker of a connecting network into one confirmed connection. The debouncer keeps a
/// confirmed state, the last reported state and the time of the last reported change.
/// </summary>
public sealed class ConnectionDebouncer
{
    public static readonly TimeSpan DefaultStableOnline = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan DefaultMinimumOffline = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _stableOnline;
    private readonly TimeSpan _minimumOffline;
    private readonly TimeSpan _cooldown;

    private bool _confirmed;
    private bool _lastReported;
    private TimeSpan _changeTime;
    private TimeSpan? _lastFiring;

    public ConnectionDebouncer(bool initiallyConnected)
        : this(initiallyConnected, DefaultStableOnline, DefaultMinimumOffline, DefaultCooldown)
    {
    }

    public ConnectionDebouncer(bool initiallyConnected, TimeSpan stableOnline, TimeSpan minimumOffline, TimeSpan cooldown)
    {
        _stableOnline = stableOnline;
        _minimumOffline = minimumOffline;
        _cooldown = cooldown;

        _confirmed = initiallyConnected;
        _lastReported = initiallyConnected;
        _changeTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Records a state. The change time moves only when the state differs. A long drop counts even
    /// when no Poll happened during it.
    /// </summary>
    public void Report(bool connected, TimeSpan now)
    {
        if (connected == _lastReported)
        {
            return;
        }

        if (connected && _confirmed && now - _changeTime >= _minimumOffline)
        {
            _confirmed = false;
        }

        _lastReported = connected;
        _changeTime = now;
    }

    /// <summary>
    /// Marks the connection as dropped by a sleep, so the next connection is a real one.
    /// </summary>
    public void Suspend(TimeSpan now)
    {
        _confirmed = false;
        _lastReported = false;
        _changeTime = now;
    }

    /// <summary>
    /// True exactly once per real connection.
    /// </summary>
    public bool Poll(TimeSpan now)
    {
        if (!_lastReported)
        {
            if (_confirmed && now - _changeTime >= _minimumOffline)
            {
                _confirmed = false;
            }

            return false;
        }

        if (_confirmed || now - _changeTime < _stableOnline)
        {
            return false;
        }

        _confirmed = true;

        if (_lastFiring is TimeSpan previous && now - previous < _cooldown)
        {
            return false;
        }

        _lastFiring = now;
        return true;
    }
}
