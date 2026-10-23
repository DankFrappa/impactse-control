namespace ImpactSE.Core;

/// <summary>
/// Ready-made setups that fill one profile: Redragon's five factory profiles, plus a few starting points.
/// The factory values come from the default profiles that ship with Redragon's app
/// (REDRAGON M908WB\Profile\Profile 1–5.prof): they differ only in light color and starting DPI stage.
/// </summary>
public static class ProfilePresets
{
    public sealed record Preset(
        string Name,
        string Description,
        ButtonAction[] SideButtons,
        int[] DpiStages,
        int StartStage,
        (byte R, byte G, byte B)? Color);

    /// <summary>Top buttons 1–6 as the mouse ships: left, right, middle, back, DPI up, DPI down.</summary>
    static readonly ButtonAction[] FactoryTop =
    {
        ButtonAction.Function(0x81), ButtonAction.Function(0x82), ButtonAction.Function(0x83),
        ButtonAction.Function(0x84), ButtonAction.Function(0x8A), ButtonAction.Function(0x89),
    };

    static readonly int[] FactoryDpi = { 400, 800, 1600, 2400, 3200 };

    static ButtonAction[] Keys(params byte[] usages) => usages.Select(ButtonAction.Key).ToArray();

    /// <summary>Number row 1–0, then Num + and Num −, as the mouse ships.</summary>
    static readonly ButtonAction[] FactorySide =
        Keys(0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x57, 0x56);

    static Preset Factory(int n, string color, byte r, byte g, byte b) =>
        new($"Factory profile {n} ({color})",
            $"Redragon's factory settings: {color} light, starts at {FactoryDpi[n - 1]} DPI.",
            FactorySide, FactoryDpi, n - 1, (r, g, b));

    public static readonly IReadOnlyList<Preset> All = new[]
    {
        Factory(1, "red", 0xFF, 0x00, 0x00),
        Factory(2, "blue", 0x00, 0x00, 0xFF),
        Factory(3, "green", 0x00, 0xFF, 0x00),
        Factory(4, "pink", 0xFF, 0x00, 0xFF),
        Factory(5, "yellow", 0xFF, 0xFF, 0x00),
        new Preset("MMO (side buttons F13–F24)",
            "Side buttons send F13–F24, keys no keyboard has, so they never clash with your keybinds.",
            Keys(0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0x70, 0x71, 0x72, 0x73),
            new[] { 800, 1200, 1600, 2400, 3200 }, 2, null),
        new Preset("Shooter (low DPI)",
            "Lower DPI steps for precise aim. Side buttons keep the number row for weapon slots.",
            FactorySide, new[] { 400, 800, 1200, 1600, 3200 }, 1, null),
        new Preset("Everyday and media",
            "Side buttons for browsing, copy and paste, and music.",
            new[]
            {
                ButtonAction.Function(0x84), ButtonAction.Function(0x85), ButtonAction.Combo(4, 0x2B),
                ButtonAction.Combo(1, 0x06), ButtonAction.Combo(1, 0x19), ButtonAction.Combo(1, 0x1B),
                ButtonAction.Combo(1, 0x1D), ButtonAction.Combo(8, 0x07), ButtonAction.Combo(8, 0x0F),
                ButtonAction.Media(0xCD), ButtonAction.Media(0xEA), ButtonAction.Media(0xE9),
            },
            new[] { 800, 1200, 1600, 2400, 3200 }, 2, null),
    };

    /// <summary>
    /// Fills one profile with the preset: buttons, DPI stages and polling rate, plus the light color
    /// for factory presets. Other profiles are left alone. Nothing reaches the mouse until it is saved.
    /// </summary>
    public static void Apply(MouseMemory memory, int profile, Preset preset)
    {
        for (int i = 0; i < FactoryTop.Length; i++) memory.SetButton(profile, i, FactoryTop[i]);
        for (int i = 0; i < MouseMemory.SideButtonCount; i++)
            memory.SetButton(profile, MouseMemory.FirstSideButton + i, preset.SideButtons[i]);
        memory.SetButton(profile, MouseMemory.WheelUpButton, ButtonAction.Function(0x8B));
        memory.SetButton(profile, MouseMemory.WheelDownButton, ButtonAction.Function(0x8C));

        for (int s = 0; s < MouseMemory.DpiStageCount; s++)
            memory.SetDpiStage(profile, s, new DpiStage(true, preset.DpiStages[s], preset.DpiStages[s]));
        memory.SetDpiStartStage(profile, preset.StartStage);
        memory.SetPollRate(profile, PollRate.Hz1000);
        if (preset.Color is var (r, g, b)) memory.SetColor(profile, r, g, b);
    }
}
