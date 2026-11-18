using System.Threading;
using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class RunGateTests
{
    private const int Callers = 100;

    [Fact]
    public void AdmitsExactlyOneOfOneHundredParallelCallersUntilExit()
    {
        RunGate gate = new();

        Barrier barrier = new Barrier(Callers);
        List<Thread> threads = new();
        int admitted = 0;

        for (int i = 0; i < Callers; i++)
        {
            Thread thread = new Thread(() =>
            {
                barrier.SignalAndWait();

                if (gate.TryEnter())
                {
                    Interlocked.Increment(ref admitted);
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.Equal(1, admitted);

        gate.Exit();

        Assert.True(gate.TryEnter());
        Assert.False(gate.TryEnter());

        gate.Exit();
        Assert.True(gate.TryEnter());
    }
}
