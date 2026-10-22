namespace ImpactSE.Core;

/// <summary>
/// Sends and receives HID feature reports on the mouse's configuration collection.
/// Buffers always start with the report ID.
/// </summary>
public interface IFeatureTransport : IDisposable
{
    bool SetFeature(byte[] report);

    /// <summary>Requests feature report <paramref name="reportId"/>; returns null on failure.</summary>
    byte[]? GetFeature(byte reportId, byte command, int length);
}
