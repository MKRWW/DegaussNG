using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DegaussNG.Core;

namespace DegaussNG;

/// <summary>
/// The tray icon and its menu. Every change is saved at once and applies without a restart.
/// </summary>
internal sealed class TrayController : IDisposable
{
    private static readonly int[] VolumeSteps = { 10, 30, 50, 70, 100 };
    private static readonly string[] ModeNames = { ModemModes.Short, ModemModes.Full };

    private readonly ConfigStore _store;
    private readonly SoundPlayer _player = new();
    private readonly TrayIconFactory _iconFactory = new();
    private readonly RunGate _gate = new();
    private readonly DegaussRunner _runner;
    private readonly HotkeyService _hotkey;
    private readonly NetworkWatcher _watcher;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _icon;

    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _motionItem;
    private readonly ToolStripMenuItem _modemSoundItem;
    private readonly List<ToolStripMenuItem> _volumeItems = new();
    private readonly List<ToolStripMenuItem> _modeItems = new();

    private AppConfig _config;
    private bool _disposed;

    public TrayController(ConfigStore store)
    {
        _store = store;
        _config = store.Load();

        _runner = new DegaussRunner(_gate, _config, _player);
        _hotkey = new HotkeyService(gesture =>
        {
            // With the hotkey turned off, the trigger does nothing, but "Degauss now" still works.
            if (_config.DegaussEnabled)
            {
                _runner.Run();
            }
        });

        _watcher = new NetworkWatcher();
        _watcher.Connected += OnNetworkConnected;

        _menu = new ContextMenuStrip();
        _enabledItem = new ToolStripMenuItem("Degauss enabled")
        {
            CheckOnClick = true,
            Checked = _config.DegaussEnabled,
        };
        _motionItem = new ToolStripMenuItem("Reduced motion")
        {
            CheckOnClick = true,
            Checked = _config.ReducedMotion,
        };
        _modemSoundItem = new ToolStripMenuItem("Modem sound")
        {
            CheckOnClick = true,
            Checked = _config.ModemEnabled,
        };

        BuildMenu();

        _icon = new NotifyIcon
        {
            Text = "DegaussNG",
            Icon = _iconFactory.CreateIcon(),
            ContextMenuStrip = _menu,
        };

        // A click on the icon stops the modem sound.
        _icon.MouseDown += (_, _) => _player.StopModem();
        _icon.Click += (_, _) => _player.StopModem();
        _menu.Opening += (_, _) => _player.StopModem();
    }

    public void Start()
    {
        _icon.Visible = true;
        ApplyHotkey();
        _watcher.Start();
    }

    public void CloseOverlays()
    {
        _runner.CloseOverlays();
    }

    private void BuildMenu()
    {
        ToolStripMenuItem degaussItem = new ToolStripMenuItem("Degauss now");
        degaussItem.Click += (_, _) => _runner.Run();
        _menu.Items.Add(degaussItem);

        // The test plays the selected mode even when the modem sound is off.
        ToolStripMenuItem modemTestItem = new ToolStripMenuItem("Modem test");
        modemTestItem.Click += (_, _) => _player.PlayModem(_config.ModemMode, _config.Volume);
        _menu.Items.Add(modemTestItem);

        _menu.Items.Add(new ToolStripSeparator());

        _enabledItem.Click += (_, _) => Update(config => config with { DegaussEnabled = _enabledItem.Checked });
        _menu.Items.Add(_enabledItem);

        _motionItem.Click += (_, _) => Update(config => config with { ReducedMotion = _motionItem.Checked });
        _menu.Items.Add(_motionItem);

        _modemSoundItem.Click += (_, _) => Update(config => config with { ModemEnabled = _modemSoundItem.Checked });
        _menu.Items.Add(_modemSoundItem);

        ToolStripMenuItem modeMenu = new ToolStripMenuItem("Modem mode");
        foreach (string mode in ModeNames)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(mode)
            {
                CheckOnClick = true,
                Checked = _config.ModemMode == mode,
            };

            string captured = mode;
            item.Click += (_, _) => SelectMode(captured);

            _modeItems.Add(item);
            modeMenu.DropDownItems.Add(item);
        }

        _menu.Items.Add(modeMenu);

        ToolStripMenuItem volumeMenu = new ToolStripMenuItem("Volume");
        foreach (int percent in VolumeSteps)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(percent + " %")
            {
                CheckOnClick = true,
                Checked = Math.Abs(_config.Volume - percent / 100.0) < 0.0000001,
            };

            int captured = percent;
            item.Click += (_, _) => SelectVolume(captured);

            _volumeItems.Add(item);
            volumeMenu.DropDownItems.Add(item);
        }

        _menu.Items.Add(volumeMenu);

        ToolStripMenuItem hotkeyItem = new ToolStripMenuItem("Change hotkey...");
        hotkeyItem.Click += OnHotkeyClick;
        _menu.Items.Add(hotkeyItem);

        _menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            Dispose();
            System.Windows.Application.Current.Shutdown();
        };
        _menu.Items.Add(exitItem);
    }

    private void SelectVolume(int percent)
    {
        foreach (ToolStripMenuItem item in _volumeItems)
        {
            item.Checked = item == _volumeItems[Array.IndexOf(VolumeSteps, percent)];
        }

        Update(config => config with { Volume = percent / 100.0 });
    }

    private void SelectMode(string mode)
    {
        foreach (ToolStripMenuItem item in _modeItems)
        {
            item.Checked = item == _modeItems[Array.IndexOf(ModeNames, mode)];
        }

        Update(config => config with { ModemMode = mode });
    }

    private void OnNetworkConnected()
    {
        if (_config.ModemEnabled)
        {
            _player.PlayModem(_config.ModemMode, _config.Volume);
        }
    }

    private void OnHotkeyClick(object? sender, EventArgs e)
    {
        HotkeyGesture current = HotkeyGesture.TryParse(_config.Hotkey, out HotkeyGesture parsed)
            ? parsed
            : DefaultGesture();

        HotkeyDialog dialog = new HotkeyDialog(current);

        // The gesture is saved only when the dialog is closed with OK.
        if (dialog.ShowDialog() == true)
        {
            _config = _config with { Hotkey = dialog.Result.ToString() };
            _store.Save(_config);
            _runner.UpdateConfig(_config);
            ApplyHotkey();
        }
    }

    private static HotkeyGesture DefaultGesture()
    {
        return HotkeyGesture.TryParse(new AppConfig().Hotkey, out HotkeyGesture gesture) ? gesture : default;
    }

    private void Update(Func<AppConfig, AppConfig> transform)
    {
        _config = transform(_config).Normalized();
        _store.Save(_config);
        _runner.UpdateConfig(_config);
    }

    private void ApplyHotkey()
    {
        if (!_hotkey.Apply(_config))
        {
            _icon.ShowBalloonTip(2000, "DegaussNG", "Hotkey " + _config.Hotkey + " could not be registered.", ToolTipIcon.Warning);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _player.StopModem();
        _watcher.Dispose();

        _icon.Visible = false;
        _hotkey.Dispose();
        _iconFactory.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
