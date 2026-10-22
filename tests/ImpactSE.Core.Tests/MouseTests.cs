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
}
