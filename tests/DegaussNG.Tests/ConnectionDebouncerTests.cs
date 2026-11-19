using System;
using System.Collections.Generic;
using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class ConnectionDebouncerTests
{
    private static TimeSpan Now(double seconds) => TimeSpan.FromSeconds(seconds);

    private static List<double> Firings(ConnectionDebouncer debouncer, int lastSecond)
    {
        List<double> fired = new List<double>();

        for (int second = 0; second <= lastSecond; second++)
        {
            if (debouncer.Poll(Now(second)))
                fired.Add(second);
        }

        return fired;
    }

    [Fact]
    public void AnInstanceCreatedAsConnectedNeverFires()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(true);

        Assert.Empty(Firings(debouncer, 60));
    }

    [Fact]
    public void AConnectionThatStaysUpFiresOnceAfterTheStableTime()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(false);

        List<double> fired = new List<double>();

        for (int second = 0; second <= 30; second++)
        {
            if (second == 10)
                debouncer.Report(true, Now(second));

            if (debouncer.Poll(Now(second)))
                fired.Add(second);
        }

        Assert.Equal(new List<double> { 14 }, fired);
    }

    [Fact]
    public void AConnectionThatWasAlreadyConfirmedStaysSilent()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(true);

        List<double> fired = new List<double>();

        for (int second = 0; second <= 120; second++)
        {
            if (second == 10)
                debouncer.Report(false, Now(second));
            if (second == 15)
                debouncer.Report(true, Now(second));

            if (debouncer.Poll(Now(second)))
                fired.Add(second);
        }

        Assert.Empty(fired);
    }

    [Fact]
    public void ARealDropAndReconnectFiresOnce()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(true);

        List<double> fired = new List<double>();

        for (int second = 0; second <= 60; second++)
        {
            if (second == 10)
                debouncer.Report(false, Now(second));
            if (second == 30)
                debouncer.Report(true, Now(second));

            if (debouncer.Poll(Now(second)))
                fired.Add(second);
        }

        Assert.Equal(new List<double> { 34 }, fired);
    }

    [Fact]
    public void FlickerWhileConnectingFiresExactlyOnce()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(false);

        List<double> fired = new List<double>();

        for (int second = 0; second <= 40; second++)
        {
            if (second == 1)
                debouncer.Report(true, Now(second));
            if (second == 2)
                debouncer.Report(false, Now(second));
            if (second == 3)
                debouncer.Report(true, Now(second));

            if (debouncer.Poll(Now(second)))
                fired.Add(second);
        }

        Assert.Equal(new List<double> { 7 }, fired);
    }

    [Fact]
    public void ASecondConnectionWithinTheCooldownStaysSilentAndALaterOneFires()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(false);

        List<double> fired = new List<double>();

        for (int second = 0; second <= 120; second++)
        {
            if (second == 0)
                debouncer.Report(true, Now(second));
            if (second == 10)
                debouncer.Report(false, Now(second));
            if (second == 20)
                debouncer.Report(true, Now(second));
            if (second == 70)
                debouncer.Report(false, Now(second));
            if (second == 80)
                debouncer.Report(true, Now(second));

            if (debouncer.Poll(Now(second)))
                fired.Add(second);
        }

        Assert.Equal(new List<double> { 4, 84 }, fired);
    }

    [Fact]
    public void TheSameStateDoesNotMoveTheChangeTime()
    {
        ConnectionDebouncer debouncer = new ConnectionDebouncer(false);

        debouncer.Report(true, Now(10));
        debouncer.Report(true, Now(20));

        Assert.False(debouncer.Poll(Now(13)));
        Assert.True(debouncer.Poll(Now(14)));
    }

    [Fact]
    public void TheDefaultsAreFourSecondsEightSecondsAndSixtySeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(4), ConnectionDebouncer.DefaultStableOnline);
        Assert.Equal(TimeSpan.FromSeconds(8), ConnectionDebouncer.DefaultMinimumOffline);
        Assert.Equal(TimeSpan.FromSeconds(60), ConnectionDebouncer.DefaultCooldown);
    }
}
