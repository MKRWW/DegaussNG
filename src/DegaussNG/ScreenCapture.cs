using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DegaussNG;

/// <summary>
/// One monitor, captured in physical pixels before any overlay exists.
/// </summary>
internal sealed record MonitorCapture(int Left, int Top, int Width, int Height, BitmapSource Image);

/// <summary>
/// Photographs every monitor. Because no overlay is on screen during the capture,
/// the overlay can never photograph itself.
/// </summary>
internal static class ScreenCapture
{
    private const int BytesPerPixel = 4;
    private const double ImageDpi = 96.0;

    public static List<MonitorCapture> Capture()
    {
        List<MonitorCapture> captures = new();

        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            return captures;
        }

        IntPtr memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        if (memoryDc == IntPtr.Zero)
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            return captures;
        }

        try
        {
            NativeMethods.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                NativeMethods.MONITORINFO info = new()
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>(),
                };

                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    BitmapSource? image = CaptureRectangle(screenDc, memoryDc, info.rcMonitor);
                    if (image != null)
                    {
                        captures.Add(new MonitorCapture(
                            info.rcMonitor.left,
                            info.rcMonitor.top,
                            image.PixelWidth,
                            image.PixelHeight,
                            image));
                    }
                }

                return true;
            };

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        }
        finally
        {
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }

        return captures;
    }

    private static BitmapSource? CaptureRectangle(IntPtr screenDc, IntPtr memoryDc, NativeMethods.RECT rectangle)
    {
        int width = rectangle.right - rectangle.left;
        int height = rectangle.bottom - rectangle.top;

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        IntPtr bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, width, height);
        if (bitmap == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            IntPtr previous = NativeMethods.SelectObject(memoryDc, bitmap);

            bool copied = NativeMethods.BitBlt(
                memoryDc,
                0,
                0,
                width,
                height,
                screenDc,
                rectangle.left,
                rectangle.top,
                NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);

            // GetDIBits requires the bitmap to be deselected first.
            NativeMethods.SelectObject(memoryDc, previous);

            return copied ? ReadOpaquePixels(memoryDc, bitmap, width, height) : null;
        }
        finally
        {
            NativeMethods.DeleteObject(bitmap);
        }
    }

    /// <summary>
    /// The alpha byte of a screen bitmap is undefined, so the pixels are read as Bgr32, which ignores it.
    /// </summary>
    private static BitmapSource? ReadOpaquePixels(IntPtr memoryDc, IntPtr bitmap, int width, int height)
    {
        int stride = width * BytesPerPixel;
        byte[] pixels = new byte[stride * height];

        NativeMethods.BITMAPINFOHEADER header = new()
        {
            biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // negative: top-down rows
            biPlanes = 1,
            biBitCount = 32,
            biCompression = NativeMethods.BI_RGB,
        };

        int lines = NativeMethods.GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref header, NativeMethods.DIB_RGB_COLORS);
        if (lines != height)
        {
            return null;
        }

        BitmapSource image = BitmapSource.Create(width, height, ImageDpi, ImageDpi, PixelFormats.Bgr32, null, pixels, stride);
        image.Freeze();

        return image;
    }
}
