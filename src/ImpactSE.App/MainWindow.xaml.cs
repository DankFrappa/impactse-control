using System.Diagnostics;
using ImpactSE.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace ImpactSE.App;

public sealed partial class MainWindow : Window
{
    static readonly PollRate[] PollRates = { PollRate.Hz1000, PollRate.Hz500, PollRate.Hz250, PollRate.Hz125 };

    ImpactMouse? _mouse;
    MouseMemory? _memory;
    int _profile;
    bool _populating;

    readonly ComboBox[] _sideBoxes = new ComboBox[MouseMemory.SideButtonCount];
    readonly ComboBox[] _mainBoxes = new ComboBox[MouseMemory.MainButtons.Count];
    readonly ToggleSwitch[] _dpiToggles = new ToggleSwitch[MouseMemory.DpiStageCount];
    readonly NumberBox[] _dpiBoxes = new NumberBox[MouseMemory.DpiStageCount];

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 820));
        BuildSideGrid();
        BuildMainGrid();
        BuildDpiRows();
        foreach (var (name, _) in HidKeys.SidePresets) PresetBox.Items.Add(name);
        for (int p = 0; p < MouseMemory.ProfileCount; p++) ProfileBox.Items.Add($"Profile {p + 1}");
        Nav.SelectedItem = Nav.MenuItems[0];
        _ = ConnectAsync();
    }

    async Task ConnectAsync()
    {
        StatusText.Text = "Looking for your Impact SE…";
        try
        {
            var (mouse, memory) = await Task.Run(() =>
            {
                var transport = HidTransport.OpenFirst()
                    ?? throw new MouseException("Impact SE not found. Plug it in, close Redragon's app, then restart this app.");
                var m = new ImpactMouse(transport);
                var mem = m.Load();
                Backups.Save(mem.Bytes);
                return (m, mem);
            });
            _mouse = mouse;
            _memory = memory;
            _profile = memory.ActiveProfile;
            ProfileBox.IsEnabled = Nav.IsEnabled = true;
            ProfileBox.SelectedIndex = _profile;
            Populate();
            StatusText.Text = "Connected.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "";
            Show(InfoBarSeverity.Error, "Couldn't connect", ex.Message);
        }
    }

    // ---- Building the page controls ----

    void BuildSideGrid()
    {
        // Same layout as the mouse's side panel: 4 rows of 3.
        for (int c = 0; c < 3; c++) SideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        for (int r = 0; r < 4; r++) SideGrid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < MouseMemory.SideButtonCount; i++)
        {
            var box = NewButtonBox($"Side button {i + 1}", MouseMemory.FirstSideButton + i);
            Grid.SetRow(box, i / 3);
            Grid.SetColumn(box, i % 3);
            SideGrid.Children.Add(box);
            _sideBoxes[i] = box;
        }
    }

    void BuildMainGrid()
    {
        for (int c = 0; c < 3; c++) MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        for (int r = 0; r < 3; r++) MainGrid.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < MouseMemory.MainButtons.Count; i++)
        {
            var (button, name) = MouseMemory.MainButtons[i];
            var box = NewButtonBox(name, button);
            Grid.SetRow(box, i / 3);
            Grid.SetColumn(box, i % 3);
            MainGrid.Children.Add(box);
            _mainBoxes[i] = box;
        }
    }

    /// <summary>A menu of every choice for one button; Tag holds the button's number in mouse memory.</summary>
    ComboBox NewButtonBox(string header, int button)
    {
        var box = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch, Tag = button, MaxDropDownHeight = 480 };
        foreach (var (_, name) in ButtonChoices.All) box.Items.Add(name);
        box.SelectionChanged += ButtonBox_SelectionChanged;
        return box;
    }

    void BuildDpiRows()
    {
        for (int i = 0; i < MouseMemory.DpiStageCount; i++)
        {
            var toggle = new ToggleSwitch { OnContent = $"Stage {i + 1}", OffContent = $"Stage {i + 1} (off)", MinWidth = 160, Tag = i };
            var box = new NumberBox
            {
                Minimum = DpiStage.Min, Maximum = DpiStage.Max, SmallChange = DpiStage.Step, LargeChange = 1000,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Width = 180, Tag = i,
            };
            toggle.Toggled += Dpi_Changed;
            box.ValueChanged += (s, e) => Dpi_Changed(s, null!);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            row.Children.Add(toggle);
            row.Children.Add(box);
            DpiRows.Children.Add(row);
            _dpiToggles[i] = toggle;
            _dpiBoxes[i] = box;
        }
    }

    // ---- Showing the current profile ----

    void Populate()
    {
        if (_memory is null) return;
        _populating = true;
        try
        {
            foreach (var box in _sideBoxes.Concat(_mainBoxes))
            {
                var action = _memory.GetButton(_profile, (int)box.Tag);
                box.SelectedIndex = ButtonChoices.IndexOf(action);
                box.PlaceholderText = HidKeys.Describe(action);
            }

            for (int i = 0; i < MouseMemory.DpiStageCount; i++)
            {
                var stage = _memory.GetDpiStage(_profile, i);
                _dpiToggles[i].IsOn = stage.Enabled;
                _dpiBoxes[i].Value = Math.Clamp(stage.X, DpiStage.Min, DpiStage.Max);
            }

            var (r, g, b) = _memory.GetColor(_profile);
            ColorWheel.Color = Color.FromArgb(255, r, g, b);

            PollRateButtons.SelectedIndex = Array.IndexOf(PollRates, _memory.GetPollRate(_profile));
            ActiveProfileText.Text = $"The mouse is using Profile {_memory.ActiveProfile + 1}.";

            var backups = Backups.List();
            BackupsText.Text = backups.Count == 0
                ? "No backups yet."
                : $"{backups.Count} backup(s) in {Backups.Folder}\nOriginal: {Path.GetFileName(backups[0])}";
        }
        finally { _populating = false; }
        UpdateDirty();
    }

    void UpdateDirty()
    {
        bool dirty = _memory?.HasChanges == true;
        SaveButton.IsEnabled = DiscardButton.IsEnabled = dirty;
        if (dirty) StatusText.Text = "Unsaved changes.";
    }

    // ---- Edits (they change the in-memory copy; nothing is sent until Save) ----

    void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || ProfileBox.SelectedIndex < 0) return;
        _profile = ProfileBox.SelectedIndex;
        Populate();
    }

    void ButtonBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || _memory is null || sender is not ComboBox { SelectedIndex: >= 0 } box) return;
        _memory.SetButton(_profile, (int)box.Tag, ButtonChoices.All[box.SelectedIndex].Action);
        UpdateDirty();
    }

    void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_memory is null || PresetBox.SelectedIndex < 0) return;
        var usages = HidKeys.SidePresets[PresetBox.SelectedIndex].Usages;
        for (int i = 0; i < usages.Length; i++)
            _memory.SetButton(_profile, MouseMemory.FirstSideButton + i, ButtonAction.Key(usages[i]));
        Populate();
    }

    void Dpi_Changed(object sender, RoutedEventArgs e)
    {
        if (_populating || _memory is null) return;
        int i = (int)((FrameworkElement)sender).Tag;
        double value = _dpiBoxes[i].Value;
        if (double.IsNaN(value)) return;
        int dpi = (int)Math.Round(value / DpiStage.Step) * DpiStage.Step;
        dpi = Math.Clamp(dpi, DpiStage.Min, DpiStage.Max);
        _memory.SetDpiStage(_profile, i, new DpiStage(_dpiToggles[i].IsOn, dpi, dpi));
        UpdateDirty();
    }

    void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_populating || _memory is null) return;
        _memory.SetColor(_profile, args.NewColor.R, args.NewColor.G, args.NewColor.B);
        UpdateDirty();
    }

    void PollRate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || _memory is null || PollRateButtons.SelectedIndex < 0) return;
        _memory.SetPollRate(_profile, PollRates[PollRateButtons.SelectedIndex]);
        UpdateDirty();
    }

    void MakeActive_Click(object sender, RoutedEventArgs e)
    {
        if (_memory is null) return;
        _memory.ActiveProfile = _profile;
        ActiveProfileText.Text = $"Profile {_profile + 1} will be used after you save.";
        UpdateDirty();
    }

    // ---- Saving ----

    async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_mouse is null || _memory is null) return;
        var mouse = _mouse;
        var memory = _memory;
        for (int p = 0; p < MouseMemory.ProfileCount; p++)
        {
            if (memory.HasLeftClick(p)) continue;
            Show(InfoBarSeverity.Warning, "Keep a left click",
                $"No button in Profile {p + 1} does a left click. Set one button to Left click, then save again.");
            return;
        }
        SaveButton.IsEnabled = DiscardButton.IsEnabled = false;
        StatusText.Text = "Saving to the mouse…";
        try
        {
            _memory = await Task.Run(() => mouse.Save(memory));
            Populate();
            StatusText.Text = "Saved.";
            Show(InfoBarSeverity.Success, "Saved", "Your mouse has the new settings.");
        }
        catch (Exception ex)
        {
            UpdateDirty();
            Show(InfoBarSeverity.Error, "Couldn't save", ex.Message);
        }
    }

    async void Discard_Click(object sender, RoutedEventArgs e)
    {
        if (_mouse is null) return;
        var mouse = _mouse;
        _memory = await Task.Run(() => mouse.Load());
        Populate();
        StatusText.Text = "Changes discarded.";
    }

    void RestoreOriginal_Click(object sender, RoutedEventArgs e)
    {
        var backups = Backups.List();
        if (_memory is null || backups.Count == 0) return;
        _memory.LoadFrom(Backups.Load(backups[0]));
        Populate();
        Show(InfoBarSeverity.Informational, "Original settings loaded", "Click Save to mouse to put them back on the mouse.");
    }

    void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Backups.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", Backups.Folder) { UseShellExecute = true });
    }

    // ---- Navigation and messages ----

    void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        ButtonsPanel.Visibility = Vis(tag == "Buttons");
        DpiPanel.Visibility = Vis(tag == "Dpi");
        LightingPanel.Visibility = Vis(tag == "Lighting");
        PerformancePanel.Visibility = Vis(tag == "Performance");
        BackupsPanel.Visibility = Vis(tag == "Backups");
    }

    static Visibility Vis(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    void Show(InfoBarSeverity severity, string title, string message)
    {
        Banner.Severity = severity;
        Banner.Title = title;
        Banner.Message = message;
        Banner.IsOpen = true;
    }
}
