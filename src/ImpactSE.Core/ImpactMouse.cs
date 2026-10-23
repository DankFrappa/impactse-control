namespace ImpactSE.Core;

/// <summary>
/// High-level access: load the whole settings memory, then save only the bytes that changed.
/// Every load and save is wrapped in the open/close session the mouse requires.
/// </summary>
public sealed class ImpactMouse : IDisposable
{
    readonly IFeatureTransport _transport;
    readonly MouseProtocol _protocol;
    readonly Action<int> _sleep;

    public ImpactMouse(IFeatureTransport transport, Action<int>? sleep = null)
    {
        _transport = transport;
        _sleep = sleep ?? Thread.Sleep;
        _protocol = new MouseProtocol(transport, _sleep);
    }

    public MouseMemory Load() => InSession(() => new MouseMemory(_protocol.Read(0, MouseMemory.Size)));

    /// <summary>
    /// Writes the changed bytes of <paramref name="memory"/>, asks the mouse to reload, and returns
    /// a fresh copy read back from the mouse. If the mouse didn't keep some changes, it waits, reads
    /// again, and writes what is still missing one more time. <paramref name="log"/> gets a line for
    /// every step, so a save that still fails can be diagnosed.
    /// </summary>
    public MouseMemory Save(MouseMemory memory, Action<string>? log = null)
    {
        log ??= _ => { };
        var changes = memory.Changes();
        if (changes.Count == 0) return memory;

        var readBack = InSession(() =>
        {
            Write(changes, log);
            _protocol.Apply();
            return ReadBack(memory, "after save", log);
        });
        if (memory.NotKept(readBack).Count == 0) return readBack;

        // Read again after a pause, in case the mouse was still busy applying the settings.
        _sleep(500);
        readBack = InSession(() => ReadBack(memory, "after a pause", log));
        var missing = memory.NotKeptRuns(readBack);
        if (missing.Count == 0) return readBack;

        return InSession(() =>
        {
            log("Writing the missing bytes again.");
            Write(missing, log);
            _protocol.Apply();
            return ReadBack(memory, "after second try", log);
        });
    }

    void Write(IReadOnlyList<(int Address, byte[] Data)> runs, Action<string> log)
    {
        foreach (var (address, data) in runs)
        {
            log($"write 0x{address:X3}: {Convert.ToHexString(data)}");
            _protocol.Write(address, data);
        }
    }

    MouseMemory ReadBack(MouseMemory wanted, string when, Action<string> log)
    {
        var readBack = new MouseMemory(_protocol.Read(0, MouseMemory.Size));
        var missing = wanted.NotKept(readBack);
        log(missing.Count == 0
            ? $"read back {when}: all changes kept"
            : $"read back {when}: not kept at {string.Join(" ", missing.Select(a => $"0x{a:X3}={readBack.Bytes[a]:X2}"))}");
        return readBack;
    }

    T InSession<T>(Func<T> action)
    {
        _protocol.OpenSession();
        try { return action(); }
        finally { _protocol.CloseSession(); }
    }

    public void Dispose() => _transport.Dispose();
}
