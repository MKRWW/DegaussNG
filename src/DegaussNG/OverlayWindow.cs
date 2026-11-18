using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DegaussNG.Core;
using DegaussNG.Shaders;

namespace DegaussNG;

/// <summary>
/// A borderless, click-through window on top of one monitor. It never activates,
/// never shows in the taskbar and never takes focus. The window is transparent until
/// its first frame is rendered, so an empty window can never flash on screen.
/// </summary>
internal sealed class OverlayWindow : Window
{
    private readonly MonitorCapture _capture;
    private readonly DegaussEffect _effect;

    public OverlayWindow(MonitorCapture capture, PixelShader pixelShader)
    {
        _capture = capture;
        _effect = new DegaussEffect(pixelShader);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;

        Content = new Image
        {
            Stretch = Stretch.Fill,
            Source = capture.Image,
            Effect = _effect,
        };

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
    }

    public void Apply(DegaussFrame frame)
    {
        _effect.Amplitude = frame.Amplitude;
        _effect.RgbShift = frame.RgbShift;
        _effect.Brightness = frame.Brightness;
        _effect.Phase = frame.Phase;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle
                | NativeMethods.WS_EX_TRANSPARENT
                | NativeMethods.WS_EX_NOACTIVATE
                | NativeMethods.WS_EX_TOOLWINDOW);

        PlaceOnMonitor(hwnd);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        // Moving onto a monitor with another DPI makes WPF rescale the window, so place it again.
        PlaceOnMonitor(new WindowInteropHelper(this).Handle);
    }

    private void PlaceOnMonitor(IntPtr hwnd)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            _capture.Left,
            _capture.Top,
            _capture.Width,
            _capture.Height,
            NativeMethods.SWP_NOACTIVATE);
    }
}
