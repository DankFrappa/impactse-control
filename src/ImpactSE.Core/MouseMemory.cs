namespace ImpactSE.Core;

/// <summary>
/// A copy of the mouse's settings memory, plus typed accessors for the parts that are confirmed.
/// Edits change only this copy; <see cref="MouseMemory.Changes"/> lists what differs from the original.
/// </summary>
public sealed class MouseMemory
{
    public const int Size = 0x500;
    public const int ProfileCount = 5;
    public const int DpiStageCount = 5;
    public const int ButtonCount = 24;
    public const int FirstSideButton = 6;
    public const int SideButtonCount = 12;
    public const int WheelUpButton = 22;
    public const int WheelDownButton = 23;

    /// <summary>The top buttons, numbered as in Redragon's picture, then the scroll wheel.</summary>
    public static readonly IReadOnlyList<(int Button, string Name)> MainButtons = new[]
    {
        (0, "Button 1 (left)"), (1, "Button 2 (right)"), (2, "Button 3 (wheel press)"),
        (3, "Button 4"), (4, "Button 5"), (5, "Button 6"),
        (WheelUpButton, "Scroll wheel up"), (WheelDownButton, "Scroll wheel down"),
    };

    // Memory map (see docs/protocol.md).
    public const int ActiveProfileAddress = 0x2C;
    public const int PollRateAddress = 0x32;      // 2 bytes per profile
    public const int LightingAddress = 0x448;     // 8 bytes per profile: ?? R G B mode ...
    static readonly int[] ProfileBase = { 0x40, 0x100, 0x1B0, 0x260, 0x310 };
    const int DpiHeaderOffset = 0;               // count 00 currentStage 00
    const int DpiStagesOffset = 4;               // then 6 bytes per stage: en xLo xHi yLo yHi 00
    const int DpiStageLength = 6;
    const int ButtonsOffset = 0x42;              // 4 bytes per button: type mod key 00

    readonly byte[] _original;
    readonly byte[] _data;

    public MouseMemory(byte[] image)
    {
        if (image.Length != Size) throw new ArgumentException($"Expected {Size} bytes.", nameof(image));
        _original = (byte[])image.Clone();
        _data = (byte[])image.Clone();
    }

    public ReadOnlySpan<byte> Bytes => _data;

    public byte[] ToArray() => (byte[])_data.Clone();

    public static int ProfileAddress(int profile) => ProfileBase[CheckProfile(profile)];

    public int ActiveProfile
    {
        get => _data[ActiveProfileAddress];
        set => _data[ActiveProfileAddress] = (byte)CheckProfile(value);
    }

    public PollRate GetPollRate(int profile) => (PollRate)_data[PollRateAddress + 2 * CheckProfile(profile)];

    public void SetPollRate(int profile, PollRate rate)
    {
        if (!Enum.IsDefined(rate)) throw new ArgumentOutOfRangeException(nameof(rate));
        _data[PollRateAddress + 2 * CheckProfile(profile)] = (byte)rate;
    }

    public DpiStage GetDpiStage(int profile, int stage)
    {
        int a = DpiStageAddress(profile, stage);
        return new DpiStage(
            _data[a] != 0,
            DpiStage.FromRaw(_data[a + 1] | _data[a + 2] << 8),
            DpiStage.FromRaw(_data[a + 3] | _data[a + 4] << 8));
    }

    public void SetDpiStage(int profile, int stage, DpiStage value)
    {
        int a = DpiStageAddress(profile, stage);
        int x = DpiStage.ToRaw(value.X), y = DpiStage.ToRaw(value.Y);
        _data[a] = value.Enabled ? (byte)1 : (byte)0;
        _data[a + 1] = (byte)x;
        _data[a + 2] = (byte)(x >> 8);
        _data[a + 3] = (byte)y;
        _data[a + 4] = (byte)(y >> 8);
        KeepStartStageOn(profile);
    }

    /// <summary>
    /// The mouse starts on (and the DPI button returns to) the start stage, so it must be switched on.
    /// If it was just switched off, start on the first stage that is on instead.
    /// </summary>
    void KeepStartStageOn(int profile)
    {
        int start = GetDpiStartStage(profile);
        if (start < DpiStageCount && GetDpiStage(profile, start).Enabled) return;
        for (int s = 0; s < DpiStageCount; s++)
        {
            if (!GetDpiStage(profile, s).Enabled) continue;
            _data[ProfileAddress(profile) + DpiHeaderOffset + 2] = (byte)s;
            return;
        }
    }

    public int EnabledDpiStageCount(int profile) =>
        Enumerable.Range(0, DpiStageCount).Count(s => GetDpiStage(profile, s).Enabled);

    /// <summary>The DPI stage (0–4) the profile starts on.</summary>
    public int GetDpiStartStage(int profile) => _data[ProfileAddress(profile) + DpiHeaderOffset + 2];

    public void SetDpiStartStage(int profile, int stage)
    {
        if (stage < 0 || stage >= DpiStageCount) throw new ArgumentOutOfRangeException(nameof(stage));
        int a = ProfileAddress(profile) + DpiHeaderOffset;
        _data[a] = DpiStageCount;
        _data[a + 2] = (byte)stage;
    }

    public ButtonAction GetButton(int profile, int button)
    {
        int a = ButtonAddress(profile, button);
        return new ButtonAction(_data[a], _data[a + 1], _data[a + 2], _data[a + 3]);
    }

    public void SetButton(int profile, int button, ButtonAction action)
    {
        int a = ButtonAddress(profile, button);
        _data[a] = action.Type;
        _data[a + 1] = action.Modifier;
        _data[a + 2] = action.Code;
        _data[a + 3] = action.Extra;
    }

    public (byte R, byte G, byte B) GetColor(int profile)
    {
        int a = LightingAddress + 8 * CheckProfile(profile) + 1;
        return (_data[a], _data[a + 1], _data[a + 2]);
    }

    public void SetColor(int profile, byte r, byte g, byte b)
    {
        int a = LightingAddress + 8 * CheckProfile(profile) + 1;
        _data[a] = r;
        _data[a + 1] = g;
        _data[a + 2] = b;
    }

    /// <summary>Copies every byte of <paramref name="other"/> into this copy (used to restore a backup).</summary>
    public void LoadFrom(ReadOnlySpan<byte> other)
    {
        if (other.Length != Size) throw new ArgumentException($"Expected {Size} bytes.", nameof(other));
        other.CopyTo(_data);
    }

    public bool HasChanges => Changes().Count > 0;

    /// <summary>Addresses this copy changed that <paramref name="readBack"/> (read from the mouse after saving) doesn't hold.</summary>
    public IReadOnlyList<int> NotKept(MouseMemory readBack)
    {
        var missing = new List<int>();
        foreach (var (address, data) in Changes())
            for (int i = 0; i < data.Length; i++)
                if (readBack._data[address + i] != data[i]) missing.Add(address + i);
        return missing;
    }

    /// <summary>Contiguous runs of changed bytes, as (address, new bytes).</summary>
    public IReadOnlyList<(int Address, byte[] Data)> Changes()
    {
        var runs = new List<(int, byte[])>();
        int i = 0;
        while (i < Size)
        {
            if (_data[i] == _original[i]) { i++; continue; }
            int start = i;
            while (i < Size && _data[i] != _original[i]) i++;
            runs.Add((start, _data.AsSpan(start, i - start).ToArray()));
        }
        return runs;
    }

    static int DpiStageAddress(int profile, int stage)
    {
        if (stage < 0 || stage >= DpiStageCount) throw new ArgumentOutOfRangeException(nameof(stage));
        return ProfileAddress(profile) + DpiStagesOffset + DpiStageLength * stage;
    }

    /// <summary>True if some button in the profile still does a left click, so the mouse stays usable.</summary>
    public bool HasLeftClick(int profile)
    {
        for (int b = 0; b < ButtonCount; b++)
            if (GetButton(profile, b).Type == ButtonAction.LeftClickType) return true;
        return false;
    }

    static int ButtonAddress(int profile, int button)
    {
        if (button < 0 || button >= ButtonCount) throw new ArgumentOutOfRangeException(nameof(button));
        return ProfileAddress(profile) + ButtonsOffset + 4 * button;
    }

    static int CheckProfile(int profile)
    {
        if (profile < 0 || profile >= ProfileCount) throw new ArgumentOutOfRangeException(nameof(profile));
        return profile;
    }
}

/// <summary>Polling-rate codes. 1000 Hz and 500 Hz are confirmed on the mouse; 250 and 125 follow the same pattern.</summary>
public enum PollRate : byte
{
    Hz1000 = 1,
    Hz500 = 2,
    Hz250 = 4,
    Hz125 = 8,
}

public readonly record struct DpiStage(bool Enabled, int X, int Y)
{
    public const int Min = 200;
    public const int Max = 10000;
    public const int Step = 100;

    // The mouse counts in steps of 80 DPI; Redragon's app shows the nearest 100.
    // Redragon's factory profiles store 400/800/1600/2400/3200 as 5/10/20/30/40, and its app
    // writes 800 as 0x0A and 900 as 0x0B.
    public static int FromRaw(int raw) => (4 * raw + 2) / 5 * Step;

    public static int ToRaw(int dpi)
    {
        if (dpi < Min || dpi > Max || dpi % Step != 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), $"DPI must be {Min}–{Max} in steps of {Step}.");
        return (5 * (dpi / Step) + 2) / 4;
    }
}

/// <summary>One button's 4-byte mapping: [type][modifier][code][extra].</summary>
public readonly record struct ButtonAction(byte Type, byte Modifier, byte Code, byte Extra)
{
    public const byte KeyType = 0x90;
    public const byte ComboType = 0x8F;  // modifier bits: 1 Ctrl, 2 Shift, 4 Alt, 8 Win
    public const byte MediaType = 0x8E;  // modifier 01, then a 16-bit consumer usage in code/extra
    public const byte LeftClickType = 0x81;

    public bool IsKey => Type == KeyType;

    public static ButtonAction Key(byte hidUsage) => new(KeyType, 0, hidUsage, 0);
    public static ButtonAction Combo(byte modifiers, byte hidUsage) => new(ComboType, modifiers, hidUsage, 0);
    public static ButtonAction Function(byte type) => new(type, 0, 0, 0);
    public static ButtonAction Media(ushort usage) => new(MediaType, 1, (byte)usage, (byte)(usage >> 8));
}
