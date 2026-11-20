using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using DegaussNG.Core;

namespace DegaussNG;

/// <summary>
/// One black screen of the fake BIOS. Only the window on the primary monitor shows text, the
/// others are plain black. The window is placed with SetWindowPos on the physical bounds of its
/// monitor, again after the first frame is rendered, like the overlay. It is not click-through
/// and it may activate, because a key or a click on it ends the run.
/// </summary>
internal sealed class BootWindow : Window
{
    private const int FontSizeDivisor = 30;
    private const double MarginPercent = 0.02;

    private readonly System.Windows.Forms.Screen _screen;
    private readonly TextBlock? _text;
    private readonly TextBlock? _footer;

    public BootWindow(System.Windows.Forms.Screen screen, bool showsText)
    {
        _screen = screen;

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Background = Brushes.Black;

        if (showsText)
        {
            _text = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            };

            _footer = new TextBlock
            {
                FontFamily = _text.FontFamily,
                Foreground = _text.Foreground,
                Text = BootScript.Footer,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
            };

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(_text, 0);
            Grid.SetRow(_footer, 1);

            grid.Children.Add(_text);
            grid.Children.Add(_footer);

            Content = grid;

            // The size follows the window in device independent units, so scaled displays get
            // the same 30 rows as unscaled ones.
            SizeChanged += OnSizeChanged;
        }

        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
    }

    /// <summary>
    /// Writes the rows and the cursor after the last row. A window without text does nothing.
    /// </summary>
    public void Update(IReadOnlyList<string> lines, bool cursorOn)
    {
        if (_text == null)
        {
            return;
        }

        _text.Inlines.Clear();

        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                _text.Inlines.Add(new LineBreak());
            }

            _text.Inlines.Add(new Run(lines[i]));
        }

        _text.Inlines.Add(new Run(cursorOn ? "_" : string.Empty));
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_text == null || _footer == null || ActualHeight <= 0)
        {
            return;
        }

        double fontSize = ActualHeight / FontSizeDivisor;
        Thickness margin = new Thickness(ActualHeight * MarginPercent);

        _text.FontSize = fontSize;
        _text.Margin = margin;
        _footer.FontSize = fontSize;
        _footer.Margin = margin;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        PlaceOnMonitor(new WindowInteropHelper(this).Handle);
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
            _screen.Bounds.Left,
            _screen.Bounds.Top,
            _screen.Bounds.Width,
            _screen.Bounds.Height,
            0);
    }
}
