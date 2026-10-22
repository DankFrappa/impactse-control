namespace ImpactSE.Core;

/// <summary>
/// High-level access: load the whole settings memory, then save only the bytes that changed.
/// Every load and save is wrapped in the open/close session the mouse requires.
/// </summary>
public sealed class ImpactMouse : IDisposable
{
    readonly IFeatureTransport _transport;
    readonly MouseProtocol _protocol;

    public ImpactMouse(IFeatureTransport transport, Action<int>? sleep = null)
    {
        _transport = transport;
        _protocol = new MouseProtocol(transport, sleep);
    }

    public MouseMemory Load() => InSession(() => new MouseMemory(_protocol.Read(0, MouseMemory.Size)));

    /// <summary>
    /// Writes the changed bytes of <paramref name="memory"/>, asks the mouse to reload, and returns
    /// a fresh copy read back from the mouse.
    /// </summary>
    public MouseMemory Save(MouseMemory memory)
    {
        var changes = memory.Changes();
        if (changes.Count == 0) return memory;
        return InSession(() =>
        {
            foreach (var (address, data) in changes) _protocol.Write(address, data);
            _protocol.Apply();
            return new MouseMemory(_protocol.Read(0, MouseMemory.Size));
        });
    }

    T InSession<T>(Func<T> action)
    {
        _protocol.OpenSession();
        try { return action(); }
        finally { _protocol.CloseSession(); }
    }

    public void Dispose() => _transport.Dispose();
}
