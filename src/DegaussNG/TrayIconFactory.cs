using System;
using System.Drawing;

namespace DegaussNG;

/// <summary>
/// Draws a small CRT in code. The native icon handle is destroyed on exit.
/// </summary>
internal sealed class TrayIconFactory : IDisposable
{
    private const int Size = 32;

    private IntPtr _handle;

    public Icon CreateIcon()
    {
        using Bitmap bitmap = new Bitmap(Size, Size);
        using Graphics graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using Brush caseBrush = new SolidBrush(Color.FromArgb(70, 74, 80));
        using Brush screenBrush = new SolidBrush(Color.FromArgb(168, 196, 214));
        using Pen outline = new Pen(Color.FromArgb(40, 44, 48), 1);
        using Pen beam = new Pen(Color.FromArgb(232, 244, 252), 2);

        // The monitor body and the screen.
        graphics.FillRectangle(caseBrush, 3, 4, 26, 19);
        graphics.FillRectangle(screenBrush, 5, 6, 22, 15);
        graphics.DrawRectangle(outline, 3, 4, 26, 19);

        // The neck of the tube and the base.
        graphics.FillRectangle(caseBrush, 13, 23, 6, 4);
        graphics.FillRectangle(caseBrush, 8, 27, 16, 3);

        // The decaying wobble across the screen.
        for (int x = 6; x < 27; x += 3)
        {
            int y = 13 + (int)(4.0 * Math.Sin((x - 6) * 0.7));
            graphics.DrawLine(beam, x, y, x + 2, y);
        }

        _handle = bitmap.GetHicon();

        return Icon.FromHandle(_handle);
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
