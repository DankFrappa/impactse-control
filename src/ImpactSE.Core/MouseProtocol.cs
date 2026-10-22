namespace ImpactSE.Core;

/// <summary>
/// Wire protocol of the Redragon Impact SE (USB 36B7:FD16), decoded from Redragon's RDCfg.exe
/// and confirmed against a real mouse.
///
/// The mouse exposes a small settings memory. Every access is wrapped in a session:
///   open   02 F5 00
///   read   [id] F2 addrLo addrHi len            then GET_FEATURE [id]
///          reply: [id] 08 addrLo addrHi len 00 FA FA data...
///   write  [id] F3 addrLo addrHi len 00 00 00 data...
///   apply  02 F1 02 flag                         (flags 04, 01, 02, 08, 10)
///   close  02 F5 01
/// Report 2 is 16 bytes, report 3 is 64 bytes.
/// </summary>
public sealed class MouseProtocol
{
    public const byte SmallReport = 2;
    public const byte LargeReport = 3;
    public const int SmallReportLength = 16;
    public const int LargeReportLength = 64;
    public const int HeaderLength = 8;

    /// <summary>Bytes per read or write. Matches what the official app uses.</summary>
    public const int ChunkLength = 0x20;

    const byte CmdApply = 0xF1;
    const byte CmdRead = 0xF2;
    const byte CmdWrite = 0xF3;
    const byte CmdSession = 0xF5;
    const byte ReplyOk = 0x08;

    static readonly byte[] ApplyFlags = { 0x04, 0x01, 0x02, 0x08, 0x10 };

    readonly IFeatureTransport _transport;
    readonly Action<int> _sleep;

    public MouseProtocol(IFeatureTransport transport, Action<int>? sleep = null)
    {
        _transport = transport;
        _sleep = sleep ?? Thread.Sleep;
    }

    public void OpenSession() => Session(open: true);

    public void CloseSession() => Session(open: false);

    void Session(bool open)
    {
        var report = new byte[SmallReportLength];
        report[0] = SmallReport;
        report[1] = CmdSession;
        report[2] = open ? (byte)0 : (byte)1;
        Send(report);
        _sleep(50);
    }

    /// <summary>Reads <paramref name="length"/> bytes starting at <paramref name="address"/>.</summary>
    public byte[] Read(int address, int length)
    {
        var result = new byte[length];
        for (int done = 0; done < length; done += ChunkLength)
        {
            int n = Math.Min(ChunkLength, length - done);
            ReadChunk(address + done, n).CopyTo(result, done);
        }
        return result;
    }

    byte[] ReadChunk(int address, int length)
    {
        var request = new byte[LargeReportLength];
        WriteHeader(request, LargeReport, CmdRead, address, length);
        Send(request);
        _sleep(10);

        var reply = _transport.GetFeature(LargeReport, CmdRead, LargeReportLength)
            ?? throw new MouseException($"No reply reading 0x{address:X4}.");
        if (reply.Length < HeaderLength + length
            || reply[1] != ReplyOk
            || reply[2] != (byte)(address & 0xFF)
            || reply[3] != (byte)(address >> 8))
        {
            throw new MouseException($"Unexpected reply reading 0x{address:X4}: {Convert.ToHexString(reply, 0, Math.Min(8, reply.Length))}");
        }
        return reply.AsSpan(HeaderLength, length).ToArray();
    }

    /// <summary>Writes <paramref name="data"/> starting at <paramref name="address"/>.</summary>
    public void Write(int address, ReadOnlySpan<byte> data)
    {
        for (int done = 0; done < data.Length; done += ChunkLength)
        {
            int n = Math.Min(ChunkLength, data.Length - done);
            var report = new byte[LargeReportLength];
            WriteHeader(report, LargeReport, CmdWrite, address + done, n);
            data.Slice(done, n).CopyTo(report.AsSpan(HeaderLength));
            Send(report);
            _sleep(10);
        }
    }

    /// <summary>Tells the mouse to reload its settings, the same way the official app does.</summary>
    public void Apply()
    {
        foreach (var flag in ApplyFlags)
        {
            var report = new byte[SmallReportLength];
            report[0] = SmallReport;
            report[1] = CmdApply;
            report[2] = 0x02;
            report[3] = flag;
            Send(report);
            _sleep(50);
        }
    }

    static void WriteHeader(byte[] report, byte reportId, byte command, int address, int length)
    {
        if (address < 0 || address > 0xFFFF) throw new ArgumentOutOfRangeException(nameof(address));
        if (length <= 0 || length > ChunkLength) throw new ArgumentOutOfRangeException(nameof(length));
        report[0] = reportId;
        report[1] = command;
        report[2] = (byte)(address & 0xFF);
        report[3] = (byte)(address >> 8);
        report[4] = (byte)length;
    }

    void Send(byte[] report)
    {
        if (!_transport.SetFeature(report))
            throw new MouseException($"The mouse rejected command {report[1]:X2}.");
    }
}

public sealed class MouseException(string message) : Exception(message);
