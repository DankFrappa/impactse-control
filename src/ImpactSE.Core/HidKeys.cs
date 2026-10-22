namespace ImpactSE.Core;

/// <summary>Names for HID keyboard usages and the side-button presets.</summary>
public static class HidKeys
{
    public static readonly IReadOnlyList<(byte Usage, string Name)> All = Build();

    static List<(byte, string)> Build()
    {
        var keys = new List<(byte, string)>();
        for (int i = 0; i < 26; i++) keys.Add(((byte)(0x04 + i), ((char)('A' + i)).ToString()));
        for (int i = 0; i < 9; i++) keys.Add(((byte)(0x1E + i), (i + 1).ToString()));
        keys.Add((0x27, "0"));
        keys.Add((0x28, "Enter"));
        keys.Add((0x29, "Esc"));
        keys.Add((0x2A, "Backspace"));
        keys.Add((0x2B, "Tab"));
        keys.Add((0x2C, "Space"));
        keys.Add((0x2D, "-"));
        keys.Add((0x2E, "="));
        keys.Add((0x2F, "["));
        keys.Add((0x30, "]"));
        keys.Add((0x33, ";"));
        keys.Add((0x34, "'"));
        keys.Add((0x35, "`"));
        keys.Add((0x36, ","));
        keys.Add((0x37, "."));
        keys.Add((0x38, "/"));
        for (int i = 0; i < 12; i++) keys.Add(((byte)(0x3A + i), $"F{i + 1}"));
        for (int i = 0; i < 12; i++) keys.Add(((byte)(0x68 + i), $"F{i + 13}"));
        keys.Add((0x49, "Insert"));
        keys.Add((0x4A, "Home"));
        keys.Add((0x4B, "Page Up"));
        keys.Add((0x4C, "Delete"));
        keys.Add((0x4D, "End"));
        keys.Add((0x4E, "Page Down"));
        keys.Add((0x4F, "Right"));
        keys.Add((0x50, "Left"));
        keys.Add((0x51, "Down"));
        keys.Add((0x52, "Up"));
        keys.Add((0x54, "Num /"));
        keys.Add((0x55, "Num *"));
        keys.Add((0x56, "Num -"));
        keys.Add((0x57, "Num +"));
        keys.Add((0x58, "Num Enter"));
        for (int i = 0; i < 9; i++) keys.Add(((byte)(0x59 + i), $"Num {i + 1}"));
        keys.Add((0x62, "Num 0"));
        keys.Add((0x63, "Num ."));
        return keys;
    }

    public static string Name(byte usage)
    {
        foreach (var (u, n) in All) if (u == usage) return n;
        return $"Key 0x{usage:X2}";
    }

    public static string Describe(ButtonAction action) => action.Type switch
    {
        ButtonAction.KeyType when action.Modifier == 0 => Name(action.Code),
        ButtonAction.KeyType => $"{Name(action.Code)} (+ modifier 0x{action.Modifier:X2})",
        0x81 => "Left click",
        0x82 => "Right click",
        0x83 => "Middle click",
        0 => "Disabled",
        _ => $"Mouse function 0x{action.Type:X2}",
    };

    /// <summary>Key presets for the 12 side buttons, in button order.</summary>
    public static readonly IReadOnlyList<(string Name, byte[] Usages)> SidePresets = new[]
    {
        ("Number row 1–0, -, =", new byte[] { 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x2D, 0x2E }),
        ("Numpad 1–0, +, -", new byte[] { 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, 0x57, 0x56 }),
        ("F13–F24 (no clashes)", new byte[] { 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70, 0x71, 0x72, 0x73 }),
    };
}
