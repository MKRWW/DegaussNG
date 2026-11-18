using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DegaussNG.Core;

namespace DegaussNG;

/// <summary>
/// Captures a key combination, shows it as text and keeps it only when the dialog is closed with OK.
/// </summary>
public partial class HotkeyDialog : Window
{
    private HotkeyGesture _gesture;
    private bool _winHeld;

    public HotkeyDialog(HotkeyGesture current)
    {
        InitializeComponent();

        _gesture = current;
        HotkeyBox.Text = current.ToString();

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
    }

    public HotkeyGesture Result => _gesture;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.LWin || key == Key.RWin)
        {
            _winHeld = true;
            return;
        }

        HotkeyModifiers modifiers = FromModifierKeys(Keyboard.Modifiers);
        if (_winHeld)
        {
            modifiers |= HotkeyModifiers.Win;
        }

        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (HotkeyGesture.TryParse(new HotkeyGesture(modifiers, virtualKey).ToString(), out HotkeyGesture candidate))
        {
            _gesture = candidate;
            HotkeyBox.Text = _gesture.ToString();
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.LWin || key == Key.RWin)
        {
            _winHeld = false;
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!HotkeyGesture.TryParse(_gesture.ToString(), out _))
        {
            return;
        }

        DialogResult = true;
    }

    private static HotkeyModifiers FromModifierKeys(ModifierKeys modifiers)
    {
        HotkeyModifiers result = HotkeyModifiers.None;

        if ((modifiers & ModifierKeys.Control) != 0)
        {
            result |= HotkeyModifiers.Ctrl;
        }

        if ((modifiers & ModifierKeys.Alt) != 0)
        {
            result |= HotkeyModifiers.Alt;
        }

        if ((modifiers & ModifierKeys.Shift) != 0)
        {
            result |= HotkeyModifiers.Shift;
        }

        return result;
    }
}
