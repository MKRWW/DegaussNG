using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using DegaussNG.Core;

namespace DegaussNG;

internal sealed record BootResult(int Monitors, string ClosedBy, double Seconds);

/// <summary>
/// One fake BIOS run: one black window per monitor, the rows of the boot script and the boot
/// sound. Any key or click ends it, and a watchdog closes it after ten seconds. Every close
/// path releases the gate, so the boot screen and a degauss run never overlap.
/// </summary>
internal sealed class BootRunner
{
    private const double WatchdogSeconds = 10.0;
    private const int RefreshMilliseconds = 30;
    private const int BlinkHertz = 2;

    private const string BusyReason = "busy";
    private const string EndReason = "end";
    private const string KeyReason = "key";
    private const string MouseReason = "mouse";
    private const string CloseReason = "close";
    private const string WatchdogReason = "watchdog";

    private readonly RunGate _gate;
    private readonly SoundPlayer? _player;
    private readonly List<BootWindow> _windows = new();
    private readonly Stopwatch _stopwatch = new();

    private AppConfig _config;
    private BootInfo _info = new BootInfo(0, 0, 0);
    private TaskCompletionSource<BootResult> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DispatcherTimer? _refresh;
    private DispatcherTimer? _watchdog;
    private string _closedBy = BusyReason;

    public BootRunner(RunGate gate, AppConfig config, SoundPlayer? player)
    {
        _gate = gate;
        _config = config.Normalized();
        _player = player;
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config.Normalized();
    }

    public void Run()
    {
        _ = RunAsync();
    }

    public void Close()
    {
        Finish(CloseReason);
    }

    public async Task<BootResult> RunAsync()
    {
        // While the gate is held by another run, this one is ignored.
        if (!_gate.TryEnter())
        {
            return new BootResult(0, BusyReason, 0.0);
        }

        _closedBy = EndReason;
        _closed = new TaskCompletionSource<BootResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _info = SystemInfo.Read();

        try
        {
            BootWindow? primary = null;

            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                BootWindow window = new BootWindow(screen, screen.Primary);
                window.KeyDown += (_, _) => Finish(KeyReason);
                window.MouseDown += (_, _) => Finish(MouseReason);

                _windows.Add(window);
                window.Show();

                if (screen.Primary)
                {
                    primary = window;
                }
            }

            _refresh = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(RefreshMilliseconds),
            };
            _refresh.Tick += OnRefresh;
            _refresh.Start();

            _watchdog = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromSeconds(WatchdogSeconds),
            };
            _watchdog.Tick += (_, _) => Finish(WatchdogReason);
            _watchdog.Start();

            // The clock starts with the beep, so the rows and the sound stay in step.
            _stopwatch.Restart();
            _player?.PlayBoot(_config.Volume);

            // A window only gets keys when it is active.
            primary?.Activate();

            await _closed.Task;

            return Result();
        }
        catch (Exception exception)
        {
            Log.Error("boot screen failed: " + exception.Message);
            return Result();
        }
        finally
        {
            _player?.StopBoot();

            if (_refresh != null)
            {
                _refresh.Stop();
            }

            if (_watchdog != null)
            {
                _watchdog.Stop();
            }

            CloseWindows();
            _windows.Clear();

            _gate.Exit();
        }
    }

    private void OnRefresh(object? sender, EventArgs e)
    {
        double seconds = _stopwatch.Elapsed.TotalSeconds;

        if (seconds >= BootScript.TotalSeconds)
        {
            Finish(EndReason);
            return;
        }

        IReadOnlyList<string> lines = BootScript.LinesAt(_info, seconds, _config.ReducedMotion);

        // Two blinks a second, and the cursor stays on when nothing counts.
        bool cursorOn = _config.ReducedMotion || (int)(seconds * BlinkHertz * 2.0) % 2 == 0;

        foreach (BootWindow window in _windows)
        {
            window.Update(lines, cursorOn);
        }
    }

    private void Finish(string reason)
    {
        _closedBy = reason;
        _closed.TrySetResult(Result());
    }

    private BootResult Result()
    {
        return new BootResult(_windows.Count, _closedBy, _stopwatch.Elapsed.TotalSeconds);
    }

    private void CloseWindows()
    {
        foreach (BootWindow window in _windows)
        {
            try
            {
                window.Close();
            }
            catch (Exception exception)
            {
                Log.Error("boot window close failed: " + exception.Message);
            }
        }
    }
}
