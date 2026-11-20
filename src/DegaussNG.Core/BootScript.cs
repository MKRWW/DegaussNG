using System.Globalization;

namespace DegaussNG.Core;

/// <summary>
/// What the fake BIOS reports about the machine it runs on.
/// </summary>
public sealed record BootInfo(long MemoryKilobytes, int Cores, int Megahertz);

/// <summary>
/// The rows of the fake boot screen. A row appears once its time is reached and stays, and
/// the memory counter only ever moves forward. The footer is not part of the rows.
/// </summary>
public static class BootScript
{
    public const double TotalSeconds = 6.0;
    public const double MemoryStartSeconds = 1.2;
    public const double MemoryEndSeconds = 3.0;
    public const double FloppyStartSeconds = 3.2;
    public const double FloppyEndSeconds = 5.0;
    public const string Footer = "Press DEL to enter SETUP";

    private const double TitleSeconds = 0.20;
    private const double ProcessorSeconds = 0.60;
    private const double CorrectionSeconds = 1.00;
    private const double StartRowSeconds = 5.30;

    private const int CounterStepKilobytes = 1024;

    private const string TitleRow = "DegaussNG Retro BIOS v1.0, a very green product";
    private const string CopyrightRow = "Copyright (C) 1994-2026 DegaussNG contributors";
    private const string ProcessorRow = "Main Processor : 486DX2-66 compatible";
    private const string CorrectionIndent = "                 ";
    private const string MemoryPrefix = "Memory Testing : ";
    private const string FloppyRow = "Detecting Floppy Drive A: ...";
    private const string FloppySuffix = " 1.44M 3.5 in. OK";
    private const string StartRow = "Starting operating system...";

    /// <summary>
    /// The counting memory test. Nothing is counted before the test starts, and the full
    /// value is reached at its end. With reduced motion the value is there from the start.
    /// </summary>
    public static long MemoryCounterAt(long memoryKilobytes, double seconds, bool reducedMotion)
    {
        if (seconds < MemoryStartSeconds)
        {
            return 0;
        }

        if (reducedMotion || seconds >= MemoryEndSeconds)
        {
            return memoryKilobytes;
        }

        double p = (seconds - MemoryStartSeconds) / (MemoryEndSeconds - MemoryStartSeconds);
        long counted = (long)Math.Floor(memoryKilobytes * p);

        return counted - counted % CounterStepKilobytes;
    }

    /// <summary>
    /// The rows that are visible at the given time, in the order they appear on screen.
    /// </summary>
    public static IReadOnlyList<string> LinesAt(BootInfo info, double seconds, bool reducedMotion)
    {
        List<string> lines = new List<string>();

        if (seconds >= TitleSeconds)
        {
            lines.Add(TitleRow);
            lines.Add(CopyrightRow);
            lines.Add(string.Empty);
        }

        if (seconds >= ProcessorSeconds)
        {
            lines.Add(ProcessorRow);
        }

        if (seconds >= CorrectionSeconds)
        {
            lines.Add(CorrectionIndent + CorrectionRow(info));
        }

        if (seconds >= MemoryStartSeconds)
        {
            string counted = Number(MemoryCounterAt(info.MemoryKilobytes, seconds, reducedMotion));
            lines.Add(MemoryPrefix + counted + "K" + (seconds >= MemoryEndSeconds ? " OK" : string.Empty));
        }

        if (seconds >= FloppyStartSeconds)
        {
            lines.Add(FloppyRow + (seconds >= FloppyEndSeconds ? FloppySuffix : string.Empty));
        }

        if (seconds >= StartRowSeconds)
        {
            lines.Add(string.Empty);
            lines.Add(StartRow);
        }

        return lines;
    }

    private static string CorrectionRow(BootInfo info)
    {
        return info.Megahertz == 0
            ? "...correction: " + info.Cores + " cores. Turbo ON."
            : "...correction: " + info.Cores + " cores at " + Number(info.Megahertz) + " MHz. Turbo ON.";
    }

    /// <summary>
    /// Plain digits in the invariant culture, so no thousands separator appears.
    /// </summary>
    private static string Number(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
