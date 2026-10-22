using System.Globalization;
using ImpactSE.Core;

namespace ImpactSE.Core.Tests;

/// <summary>
/// Simulates the mouse's feature-report behaviour as observed on real hardware,
/// including returning zeros when no session is open.
/// </summary>
sealed class FakeMouse : IFeatureTransport
{
    public readonly byte[] Memory;
    public readonly List<byte[]> Sent = new();
    bool _session;
    byte[]? _pendingRead;

    public FakeMouse(byte[] memory) => Memory = memory;

    public static FakeMouse FromDump(string fixture) => new(LoadDump(fixture));

    public static byte[] LoadDump(string fixture)
    {
        var memory = new byte[MouseMemory.Size];
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)))
        {
            int address = int.Parse(line[..4], NumberStyles.HexNumber);
            var bytes = line[6..].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => byte.Parse(b, NumberStyles.HexNumber)).ToArray();
            bytes.AsSpan(MouseProtocol.HeaderLength, MouseProtocol.ChunkLength).CopyTo(memory.AsSpan(address));
        }
        return memory;
    }

    public bool SetFeature(byte[] report)
    {
        Sent.Add((byte[])report.Clone());
        int address = report[2] | report[3] << 8, length = report[4];
        switch (report[1])
        {
            case 0xF5:
                _session = report[2] == 0;
                break;
            case 0xF2:
                _pendingRead = report[..MouseProtocol.HeaderLength];
                break;
            case 0xF3 when _session:
                report.AsSpan(MouseProtocol.HeaderLength, length).CopyTo(Memory.AsSpan(address));
                break;
        }
        return true;
    }

    public byte[]? GetFeature(byte reportId, byte command, int length)
    {
        var reply = new byte[length];
        reply[0] = reportId;
        if (!_session || _pendingRead is null) return reply;
        int address = _pendingRead[2] | _pendingRead[3] << 8, n = _pendingRead[4];
        reply[1] = 0x08;
        reply[2] = _pendingRead[2];
        reply[3] = _pendingRead[3];
        reply[4] = (byte)n;
        reply[6] = reply[7] = 0xFA;
        Memory.AsSpan(address, n).CopyTo(reply.AsSpan(MouseProtocol.HeaderLength));
        _pendingRead = null;
        return reply;
    }

    public void Dispose() { }
}
