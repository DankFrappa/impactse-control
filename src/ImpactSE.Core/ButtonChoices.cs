namespace ImpactSE.Core;

/// <summary>
/// Everything a button can be set to. The mouse-function and media codes come from
/// the lookup table in Redragon's app (RDCfg.exe, 0x4a8980).
/// </summary>
public static class ButtonChoices
{
    public static readonly IReadOnlyList<(ButtonAction Action, string Name)> MouseFunctions = new[]
    {
        (ButtonAction.Function(0x81), "Left click"),
        (ButtonAction.Function(0x82), "Right click"),
        (ButtonAction.Function(0x83), "Middle click"),
        (ButtonAction.Function(0x84), "Back"),
        (ButtonAction.Function(0x85), "Forward"),
        (ButtonAction.Function(0x8B), "Scroll up"),
        (ButtonAction.Function(0x8C), "Scroll down"),
        (ButtonAction.Function(0x86), "Scroll left"),
        (ButtonAction.Function(0x87), "Scroll right"),
        (ButtonAction.Function(0x88), "DPI cycle"),
        (ButtonAction.Function(0x8A), "DPI up"),
        (ButtonAction.Function(0x89), "DPI down"),
        (ButtonAction.Function(0x8D), "Profile cycle"),
        (ButtonAction.Function(0x94), "Profile up"),
        (ButtonAction.Function(0x95), "Profile down"),
        (ButtonAction.Function(0x97), "Polling rate up"),
        (ButtonAction.Function(0x98), "Polling rate down"),
        (new ButtonAction(0x9B, 4, 0, 0), "Lights on/off"),
        (ButtonAction.Function(0x00), "Disabled"),
    };

    public static readonly IReadOnlyList<(ButtonAction Action, string Name)> Media = new[]
    {
        (ButtonAction.Media(0xCD), "Play / pause"),
        (ButtonAction.Media(0xB7), "Stop"),
        (ButtonAction.Media(0xB6), "Previous track"),
        (ButtonAction.Media(0xB5), "Next track"),
        (ButtonAction.Media(0xE9), "Volume up"),
        (ButtonAction.Media(0xEA), "Volume down"),
        (ButtonAction.Media(0xE2), "Mute"),
    };

    public static readonly IReadOnlyList<(ButtonAction Action, string Name)> Shortcuts = new[]
    {
        (ButtonAction.Combo(1, 0x06), "Copy (Ctrl+C)"),
        (ButtonAction.Combo(1, 0x19), "Paste (Ctrl+V)"),
        (ButtonAction.Combo(1, 0x1B), "Cut (Ctrl+X)"),
        (ButtonAction.Combo(1, 0x1D), "Undo (Ctrl+Z)"),
        (ButtonAction.Combo(4, 0x2B), "Switch window (Alt+Tab)"),
        (ButtonAction.Combo(8, 0x07), "Show desktop (Win+D)"),
        (ButtonAction.Combo(8, 0x0F), "Lock PC (Win+L)"),
    };

    /// <summary>The full list shown in each button's menu: mouse functions, then keys, then media and shortcuts.</summary>
    public static readonly IReadOnlyList<(ButtonAction Action, string Name)> All =
        MouseFunctions
            .Concat(HidKeys.All.Select(k => (ButtonAction.Key(k.Usage), k.Name)))
            .Concat(Media)
            .Concat(Shortcuts)
            .ToList();

    /// <summary>Position of the action in <see cref="All"/>, or -1 if it isn't one of the listed choices.</summary>
    public static int IndexOf(ButtonAction action)
    {
        for (int i = 0; i < All.Count; i++) if (All[i].Action == action) return i;
        return -1;
    }
}
