using System;
using System.Collections.Generic;
using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class BootScriptTests
{
    private static readonly BootInfo Info = new BootInfo(16777216, 16, 3200);

    private static readonly string[] Forbidden =
    {
        "intel", "amd", "award", "phoenix", "american megatrends", "microsoft", "windows",
    };

    [Fact]
    public void MemoryCounterIsZeroBeforeTheTestAndFullAtItsEnd()
    {
        Assert.Equal(0, BootScript.MemoryCounterAt(16777216, 0.0, false));
        Assert.Equal(0, BootScript.MemoryCounterAt(16777216, 1.19, false));
        Assert.Equal(16777216, BootScript.MemoryCounterAt(16777216, 3.0, false));
        Assert.Equal(16777216, BootScript.MemoryCounterAt(16777216, 6.0, false));
    }

    [Fact]
    public void CounterCountsInMultiplesOf1024AndNeverDecreases()
    {
        long previous = 0;

        for (double seconds = 0.0; seconds <= BootScript.TotalSeconds; seconds += 0.01)
        {
            long value = BootScript.MemoryCounterAt(16777216, seconds, false);

            Assert.True(value % 1024 == 0, $"{seconds:F2} s gives {value}");
            Assert.True(value >= previous, $"{seconds:F2} s gives {value}, which is below {previous}");

            previous = value;
        }

        Assert.Equal(16777216, previous);
    }

    [Fact]
    public void ReducedMotionCountsNothing()
    {
        Assert.Equal(0, BootScript.MemoryCounterAt(16777216, 1.19, true));
        Assert.Equal(16777216, BootScript.MemoryCounterAt(16777216, 1.20, true));
        Assert.Equal(16777216, BootScript.MemoryCounterAt(16777216, 2.50, true));
    }

    [Fact]
    public void RowsAppearAtTheirTimes()
    {
        Assert.Empty(BootScript.LinesAt(Info, 0.1, false));

        IReadOnlyList<string> at070 = BootScript.LinesAt(Info, 0.7, false);
        Assert.Equal(4, at070.Count);
        Assert.Equal("Main Processor : 486DX2-66 compatible", at070[3]);

        IReadOnlyList<string> at200 = BootScript.LinesAt(Info, 2.0, false);
        string memory = at200[5];
        Assert.StartsWith("Memory Testing : ", memory);
        Assert.DoesNotContain(" OK", memory);

        IReadOnlyList<string> at550 = BootScript.LinesAt(Info, 5.5, false);
        Assert.Equal(9, at550.Count);
        Assert.Equal("Starting operating system...", at550[8]);
        Assert.Equal("Memory Testing : 16777216K OK", at550[5]);
        Assert.EndsWith(" 1.44M 3.5 in. OK", at550[6]);
    }

    [Fact]
    public void RowsStayInOrderAndTheLastOnesAreTheStartOfTheSystem()
    {
        IReadOnlyList<string> lines = BootScript.LinesAt(Info, 6.0, false);

        Assert.Equal("DegaussNG Retro BIOS v1.0, a very green product", lines[0]);
        Assert.Equal("Copyright (C) 1994-2026 DegaussNG contributors", lines[1]);
        Assert.Equal(string.Empty, lines[2]);
        Assert.Equal("Main Processor : 486DX2-66 compatible", lines[3]);
        Assert.StartsWith("                 ...correction:", lines[4]);
        Assert.Equal("Detecting Floppy Drive A: ... 1.44M 3.5 in. OK", lines[6]);
        Assert.Equal(string.Empty, lines[7]);
        Assert.Equal("Starting operating system...", lines[8]);
    }

    [Fact]
    public void CorrectionRowReadsTheCoresAndTheMegahertz()
    {
        IReadOnlyList<string> withMegahertz = BootScript.LinesAt(new BootInfo(16777216, 16, 3200), 1.0, false);
        Assert.Equal("                 ...correction: 16 cores at 3200 MHz. Turbo ON.", withMegahertz[4]);

        IReadOnlyList<string> withoutMegahertz = BootScript.LinesAt(new BootInfo(16777216, 4, 0), 1.0, false);
        Assert.Equal("                 ...correction: 4 cores. Turbo ON.", withoutMegahertz[4]);
    }

    [Fact]
    public void NoRowAndNoFooterNamesAManufacturer()
    {
        foreach (string row in BootScript.LinesAt(Info, 6.0, false))
        {
            AssertNotNamed(row);
        }

        AssertNotNamed(BootScript.Footer);
    }

    private static void AssertNotNamed(string row)
    {
        foreach (string word in Forbidden)
        {
            Assert.DoesNotContain(word, row, StringComparison.OrdinalIgnoreCase);
        }
    }
}
