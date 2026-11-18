namespace DegaussNG.Core;

/// <summary>
/// Admits exactly one caller at a time, so a trigger during a running effect is ignored.
/// </summary>
public sealed class RunGate
{
    private const int Free = 0;
    private const int Held = 1;

    private int _state;

    public bool TryEnter()
    {
        return Interlocked.CompareExchange(ref _state, Held, Free) == Free;
    }

    public void Exit()
    {
        Interlocked.Exchange(ref _state, Free);
    }
}
