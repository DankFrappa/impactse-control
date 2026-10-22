namespace ImpactSE.Core;

/// <summary>Raw copies of the mouse's settings memory, kept so any change can be undone.</summary>
public static class Backups
{
    public static string Folder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImpactSE", "Backups");

    /// <summary>Saves the image unless an identical backup already exists. Returns the file path.</summary>
    public static string Save(ReadOnlySpan<byte> image)
    {
        Directory.CreateDirectory(Folder);
        var bytes = image.ToArray();
        foreach (var existing in List())
            if (File.ReadAllBytes(existing).AsSpan().SequenceEqual(bytes)) return existing;
        var path = Path.Combine(Folder, $"impactse-{DateTime.Now:yyyyMMdd-HHmmss}.bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Backup files, oldest first. The first one is the mouse as it was before this app changed anything.</summary>
    public static IReadOnlyList<string> List() =>
        Directory.Exists(Folder)
            ? Directory.GetFiles(Folder, "impactse-*.bin").Order(StringComparer.Ordinal).ToList()
            : Array.Empty<string>();

    public static byte[] Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != MouseMemory.Size) throw new InvalidDataException("Not an Impact SE backup.");
        return bytes;
    }
}
