using System;
using DegaussNG.Core;
using Microsoft.Win32;

namespace DegaussNG;

/// <summary>
/// What the fake BIOS reports about this machine. Every failure gives 0 and is logged, so a
/// missing key never stops the boot screen.
/// </summary>
internal static class SystemInfo
{
    private const string ProcessorKeyPath = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
    private const string MegahertzValueName = "~MHz";

    internal static BootInfo Read()
    {
        long memoryKilobytes = 0;

        try
        {
            memoryKilobytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024;
        }
        catch (Exception exception)
        {
            Log.Error("system info: memory read failed: " + exception.Message);
        }

        int cores = 0;

        try
        {
            cores = Environment.ProcessorCount;
        }
        catch (Exception exception)
        {
            Log.Error("system info: processor count failed: " + exception.Message);
        }

        return new BootInfo(memoryKilobytes, cores, ReadMegahertz());
    }

    /// <summary>
    /// The DWORD ~MHz of the first processor. Reading the registry is the only way, so no
    /// native call is used here.
    /// </summary>
    private static int ReadMegahertz()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(ProcessorKeyPath);
            if (key == null)
            {
                Log.Error("system info: " + ProcessorKeyPath + " is not present");
                return 0;
            }

            if (key.GetValue(MegahertzValueName) is not int megahertz)
            {
                Log.Error("system info: " + MegahertzValueName + " is not a DWORD");
                return 0;
            }

            return megahertz;
        }
        catch (Exception exception)
        {
            Log.Error("system info: registry read failed: " + exception.Message);
            return 0;
        }
    }
}
