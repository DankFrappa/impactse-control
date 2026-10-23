using ImpactSE.Core;
using Xunit;

namespace ImpactSE.Core.Tests;

public class MouseTests
{
    static MouseMemory LoadFixture(string fixture) =>
        new ImpactMouse(FakeMouse.FromDump(fixture), _ => { }).Load();

    [Fact]
    public void Load_reads_the_whole_memory_inside_a_session()
    {
        var fake = FakeMouse.FromDump("baseline.txt");
        var memory = new ImpactMouse(fake, _ => { }).Load();

        Assert.Equal(fake.Memory, memory.ToArray());
        Assert.Equal(new byte[] { 0x02, 0xF5, 0x00 }, fake.Sent[0][..3]);
        Assert.Equal(new byte[] { 0x02, 0xF5, 0x01 }, fake.Sent[^1][..3]);
    }

    [Fact]
    public void Read_without_session_fails_like_the_real_mouse()
    {
        var protocol = new MouseProtocol(FakeMouse.FromDump("baseline.txt"), _ => { });
        Assert.Throws<MouseException>(() => protocol.Read(0x2C, 2));
    }

    // Values below were set by hand in Redragon's app between the two real dumps.

    [Fact]
    public void Dpi_matches_the_official_app()
    {
        var memory = LoadFixture("dpi800-red-500hz.txt");
        Assert.Equal(new DpiStage(true, 800, 800), memory.GetDpiStage(0, 0));
        Assert.Equal(900, memory.GetDpiStage(0, 1).X);
    }

    [Fact]
    public void Poll_rate_matches_the_official_app()
    {
        Assert.Equal(PollRate.Hz1000, LoadFixture("baseline.txt").GetPollRate(0));
        Assert.Equal(PollRate.Hz500, LoadFixture("dpi800-red-500hz.txt").GetPollRate(0));
    }

    [Fact]
    public void Color_matches_the_official_app()
    {
        Assert.Equal(((byte)0xFC, (byte)0x6F, (byte)0x03), LoadFixture("baseline.txt").GetColor(0));
        Assert.Equal(((byte)0xFF, (byte)0x00, (byte)0x00), LoadFixture("dpi800-red-500hz.txt").GetColor(0));
    }

    [Fact]
    public void Side_buttons_decode_as_keys()
    {
        var before = LoadFixture("baseline.txt");
        var after = LoadFixture("dpi800-red-500hz.txt");
        Assert.Equal("1", HidKeys.Describe(before.GetButton(0, MouseMemory.FirstSideButton)));
        Assert.Equal("0", HidKeys.Describe(before.GetButton(0, MouseMemory.FirstSideButton + 9)));
        Assert.Equal("Num 1", HidKeys.Describe(after.GetButton(0, MouseMemory.FirstSideButton)));
        Assert.Equal("Left click", HidKeys.Describe(before.GetButton(0, 0)));
    }

    [Fact]
    public void Editing_the_baseline_reproduces_the_official_apps_changes_in_those_fields()
    {
        var memory = LoadFixture("baseline.txt");
        var expected = FakeMouse.LoadDump("dpi800-red-500hz.txt");

        memory.SetPollRate(0, PollRate.Hz500);
        memory.SetDpiStage(0, 0, new DpiStage(true, 800, 800));
        memory.SetDpiStage(0, 1, new DpiStage(true, 900, 900));
        memory.SetColor(0, 0xFF, 0x00, 0x00);
        var numpad = HidKeys.SidePresets[1].Usages;
        for (int i = 0; i < 10; i++)
            memory.SetButton(0, MouseMemory.FirstSideButton + i, ButtonAction.Key(numpad[i]));

        Assert.Equal(expected, memory.ToArray());
    }

    [Fact]
    public void Save_writes_only_changed_bytes_then_applies()
    {
        var fake = FakeMouse.FromDump("baseline.txt");
        var mouse = new ImpactMouse(fake, _ => { });
        var memory = mouse.Load();
        fake.Sent.Clear();

        memory.SetColor(0, 0xFF, 0x00, 0x00);
        var reloaded = mouse.Save(memory);

        var writes = fake.Sent.Where(r => r[1] == 0xF3).ToList();
        var write = Assert.Single(writes);
        Assert.Equal(new byte[] { 0x03, 0xF3, 0x49, 0x04, 0x03, 0, 0, 0, 0xFF, 0x00, 0x00 }, write[..11]);
        Assert.Equal(5, fake.Sent.Count(r => r[1] == 0xF1));
        Assert.Equal(((byte)0xFF, (byte)0x00, (byte)0x00), reloaded.GetColor(0));
        Assert.False(reloaded.HasChanges);
        Assert.Equal(new byte[] { 0x02, 0xF5, 0x01 }, fake.Sent[^1][..3]);
    }

    [Fact]
    public void Save_with_no_changes_sends_nothing()
    {
        var fake = FakeMouse.FromDump("baseline.txt");
        var mouse = new ImpactMouse(fake, _ => { });
        var memory = mouse.Load();
        fake.Sent.Clear();

        mouse.Save(memory);

        Assert.Empty(fake.Sent);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(10100)]
    public void Dpi_outside_the_supported_range_is_rejected(int dpi)
    {
        var memory = LoadFixture("baseline.txt");
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.SetDpiStage(0, 0, new DpiStage(true, dpi, dpi)));
    }

    [Fact]
    public void Backups_skip_duplicates()
    {
        Backups.Folder = Path.Combine(Path.GetTempPath(), "impactse-tests-" + Guid.NewGuid());
        var image = FakeMouse.LoadDump("baseline.txt");

        var first = Backups.Save(image);
        var second = Backups.Save(image);

        Assert.Equal(first, second);
        Assert.Single(Backups.List());
        Assert.Equal(image, Backups.Load(first));
    }

    [Fact]
    public void Main_buttons_read_as_the_factory_layout()
    {
        var memory = LoadFixture("baseline.txt");
        string[] expected = { "Left click", "Right click", "Middle click", "Back", "DPI up", "DPI down", "Scroll up", "Scroll down" };

        Assert.Equal(expected, MouseMemory.MainButtons.Select(b => HidKeys.Describe(memory.GetButton(0, b.Button))));
    }

    [Fact]
    public void Every_listed_choice_has_a_unique_encoding_and_reads_back_by_name()
    {
        Assert.Equal(ButtonChoices.All.Count, ButtonChoices.All.Select(c => c.Action).Distinct().Count());
        Assert.Equal("Ctrl+C", HidKeys.Describe(ButtonChoices.All[ButtonChoices.IndexOf(ButtonAction.Combo(1, 0x06))].Action));
        Assert.Equal("Volume up", HidKeys.Describe(ButtonAction.Media(0xE9)));
    }

    [Fact]
    public void Profile_without_left_click_is_detected()
    {
        var memory = LoadFixture("baseline.txt");
        Assert.True(memory.HasLeftClick(0));

        memory.SetButton(0, 0, ButtonAction.Key(0x68));
        Assert.False(memory.HasLeftClick(0));
    }

    [Fact]
    public void Factory_dpi_stages_read_as_redragons_defaults()
    {
        // Redragon's default profiles list 400/800/1600/2400/3200; the mouse stores 5/10/20/30/40.
        var memory = LoadFixture("baseline.txt");
        Assert.Equal(new[] { 400, 800, 1600, 2400, 3200 },
            Enumerable.Range(0, MouseMemory.DpiStageCount).Select(s => memory.GetDpiStage(1, s).X));
    }

    [Fact]
    public void Every_dpi_step_survives_a_round_trip()
    {
        for (int dpi = DpiStage.Min; dpi <= DpiStage.Max; dpi += DpiStage.Step)
            Assert.Equal(dpi, DpiStage.FromRaw(DpiStage.ToRaw(dpi)));
    }

    [Fact]
    public void Factory_preset_writes_redragons_values()
    {
        var memory = LoadFixture("dpi800-red-500hz.txt");
        ProfilePresets.Apply(memory, 0, ProfilePresets.All[2]);

        int b = MouseMemory.ProfileAddress(0);
        Assert.Equal(new byte[] { 5, 0, 2, 0 }, memory.Bytes.Slice(b, 4).ToArray());
        Assert.Equal(new byte[] { 1, 5, 0, 5, 0, 0, 1, 10, 0, 10, 0, 0, 1, 20, 0, 20, 0, 0, 1, 30, 0, 30, 0, 0, 1, 40, 0, 40, 0, 0 },
            memory.Bytes.Slice(b + 4, 30).ToArray());
        Assert.Equal(PollRate.Hz1000, memory.GetPollRate(0));
        Assert.Equal(((byte)0x00, (byte)0xFF, (byte)0x00), memory.GetColor(0));
        Assert.Equal(new[] { "Left click", "Right click", "Middle click", "Back", "DPI up", "DPI down", "Scroll up", "Scroll down" },
            MouseMemory.MainButtons.Select(m => HidKeys.Describe(memory.GetButton(0, m.Button))));
        Assert.Equal("1", HidKeys.Describe(memory.GetButton(0, MouseMemory.FirstSideButton)));
        Assert.Equal("Num -", HidKeys.Describe(memory.GetButton(0, MouseMemory.FirstSideButton + 11)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Presets_change_only_the_profile_being_edited(int profile)
    {
        foreach (var preset in ProfilePresets.All)
        {
            var memory = LoadFixture("baseline.txt");
            ProfilePresets.Apply(memory, profile, preset);

            int start = MouseMemory.ProfileAddress(profile);
            int end = start + 0x42 + 4 * MouseMemory.ButtonCount;
            int poll = MouseMemory.PollRateAddress + 2 * profile;
            int light = MouseMemory.LightingAddress + 8 * profile;
            Assert.All(memory.Changes(), c => Assert.True(
                (c.Address >= start && c.Address + c.Data.Length <= end)
                || c.Address == poll
                || (c.Address > light && c.Address + c.Data.Length <= light + 4),
                $"{preset.Name} changed 0x{c.Address:X3}"));
            Assert.True(memory.HasLeftClick(profile));
        }
    }

    [Fact]
    public void Switching_off_the_start_stage_moves_the_start_to_a_stage_that_is_on()
    {
        var memory = LoadFixture("baseline.txt");
        ProfilePresets.Apply(memory, 0, ProfilePresets.All[0]);
        Assert.Equal(0, memory.GetDpiStartStage(0));

        memory.SetDpiStage(0, 0, new DpiStage(false, 400, 400));
        Assert.Equal(1, memory.GetDpiStartStage(0));
        memory.SetDpiStage(0, 2, new DpiStage(false, 1600, 1600));
        Assert.Equal(1, memory.GetDpiStartStage(0));
        Assert.Equal(3, memory.EnabledDpiStageCount(0));
    }

    [Fact]
    public void Not_kept_lists_changes_the_mouse_did_not_hold()
    {
        var wanted = LoadFixture("baseline.txt");
        wanted.SetColor(0, 0xFF, 0x00, 0x00);
        Assert.Empty(wanted.NotKept(new MouseMemory(wanted.ToArray())));
        Assert.Equal(new[] { 0x449, 0x44A, 0x44B }, wanted.NotKept(LoadFixture("baseline.txt")));
    }

    [Fact]
    public void Save_writes_dropped_changes_again()
    {
        var fake = FakeMouse.FromDump("baseline.txt");
        var mouse = new ImpactMouse(fake, _ => { });
        var memory = mouse.Load();
        memory.SetDpiStage(0, 0, new DpiStage(true, 800, 800));
        var log = new List<string>();
        fake.DropWrites = 1;

        var saved = mouse.Save(memory, log.Add);

        Assert.Empty(memory.NotKept(saved));
        Assert.Contains(log, l => l.StartsWith("read back after save: not kept"));
        Assert.Equal("read back after second try: all changes kept", log[^1]);
    }
}
