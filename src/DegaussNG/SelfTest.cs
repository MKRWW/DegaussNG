using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DegaussNG.Core;

namespace DegaussNG;

/// <summary>
/// Runs complete degauss cycles without tray, hotkey, sound or config access and reports
/// what is left behind after the last cycle.
/// </summary>
internal static class SelfTest
{
    private const int BaselineCycle = 3;
    private const int MaxGdiDelta = 5;
    private const int MaxUserDelta = 5;
    private const double MaxPrivateDeltaMb = 40.0;

    public static async Task<int> Run(int cycles, string logPath)
    {
        DegaussRunner runner = new DegaussRunner(new RunGate(), new AppConfig(), null);

        int failed = 0;
        int gdiBase = 0;
        int userBase = 0;
        long privateBase = 0;
        bool baselineTaken = false;

        for (int cycle = 1; cycle <= cycles; cycle++)
        {
            if (!await runner.RunAsync())
            {
                failed++;
            }

            if (cycle == BaselineCycle)
            {
                Collect();

                gdiBase = Count(NativeMethods.GR_GDIOBJECTS);
                userBase = Count(NativeMethods.GR_USEROBJECTS);
                privateBase = Process.GetCurrentProcess().PrivateMemorySize64;
                baselineTaken = true;
            }
        }

        Collect();

        if (!baselineTaken)
        {
            gdiBase = Count(NativeMethods.GR_GDIOBJECTS);
            userBase = Count(NativeMethods.GR_USEROBJECTS);
            privateBase = Process.GetCurrentProcess().PrivateMemorySize64;
        }

        int gdiDelta = Count(NativeMethods.GR_GDIOBJECTS) - gdiBase;
        int userDelta = Count(NativeMethods.GR_USEROBJECTS) - userBase;
        double privateDeltaMb = Math.Round(
            (Process.GetCurrentProcess().PrivateMemorySize64 - privateBase) / (1024.0 * 1024.0),
            1);

        string line = string.Format(
            CultureInfo.InvariantCulture,
            "cycles={0} failed={1} gdiDelta={2} userDelta={3} privateDeltaMB={4:F1}",
            cycles,
            failed,
            gdiDelta,
            userDelta,
            privateDeltaMb);

        try
        {
            string? directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(logPath, line + Environment.NewLine);
        }
        catch (Exception exception)
        {
            Log.Error("self-test log could not be written: " + exception.Message);
        }

        return failed == 0 && gdiDelta <= MaxGdiDelta && userDelta <= MaxUserDelta && privateDeltaMb <= MaxPrivateDeltaMb
            ? 0
            : 1;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static int Count(uint kind)
    {
        return (int)NativeMethods.GetGuiResources(NativeMethods.GetCurrentProcess(), kind);
    }
}
