using System;
using System.Windows.Interop;
using DegaussNG.Core;

namespace DegaussNG;

/// <summary>
/// Registers the hotkey on a message window and raises the trigger when it arrives.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;

    private readonly Action<HotkeyGesture> _onHotkey;

    private HwndSource? _source;
    private HotkeyGesture _current;
    private bool _registered;

    public HotkeyService(Action<HotkeyGesture> onHotkey)
    {
        _onHotkey = onHotkey;
    }

    public bool Apply(AppConfig config)
    {
        if (!HotkeyGesture.TryParse(config.Hotkey, out HotkeyGesture gesture))
        {
            return false;
        }

        HwndSource source = EnsureSource();

        Unregister();

        uint modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;

        if (!NativeMethods.RegisterHotKey(source.Handle, HotkeyId, modifiers, gesture.VirtualKey))
        {
            return false;
        }

        _current = gesture;
        _registered = true;

        return true;
    }

    private HwndSource EnsureSource()
    {
        if (_source == null)
        {
            HwndSourceParameters parameters = new HwndSourceParameters("DegaussNGHotkeyWindow", 0, 0)
            {
                PositionX = -32000,
                PositionY = -32000,
                WindowStyle = 0,
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
        }

        return _source;
    }

    private void Unregister()
    {
        if (_registered && _source != null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _onHotkey(_current);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
