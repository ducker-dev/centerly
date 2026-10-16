using Centerly.Core.AutoCenter;
using Centerly.Core.Centering;
using Centerly.Core.Hotkeys;
using Centerly.Core.Settings;

namespace Centerly.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Centerly.Tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var result = new SettingsStore(FilePath).Load();

        Assert.Equal(SettingsLoadStatus.Missing, result.Status);
        Assert.Equal(new AppSettings(), result.Settings);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new SettingsStore(FilePath);
        var settings = new AppSettings
        {
            CenterHotkey = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift, 'K'),
            Oversize = OversizeBehavior.ShrinkToFit,
        };

        store.Save(settings);
        var result = store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(settings, result.Settings);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Rules_round_trip_with_cyrillic_names()
    {
        var store = new SettingsStore(FilePath);
        var rule = new AppRule
        {
            ProcessName = "notepad.exe",
            DisplayName = "Блокнот",
            ExecutablePath = @"C:\Windows\notepad.exe",
            OnlyFirstWindow = true,
            IncludeDialogs = true,
        };

        store.Save(new AppSettings { Rules = [rule] });
        var loaded = store.Load().Settings;

        Assert.Equal(rule, Assert.Single(loaded.Rules));
        Assert.Contains("Блокнот", File.ReadAllText(FilePath));
    }

    [Fact]
    public void New_options_round_trip()
    {
        var store = new SettingsStore(FilePath);
        var settings = new AppSettings
        {
            PreviousMonitorHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x25),
            UndoHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'Z'),
            AutoCenterMode = AutoCenterMode.AllApps,
            Rules = [new AppRule { ProcessName = "obs64.exe", Action = RuleAction.Ignore }],
        };

        store.Save(settings);
        var json = File.ReadAllText(FilePath);

        Assert.Equal(settings, store.Load().Settings);
        Assert.Contains("\"undoHotkey\": \"Ctrl+Alt+Z\"", json);
        Assert.Contains("\"autoCenterMode\": \"AllApps\"", json);
        Assert.Contains("\"action\": \"Ignore\"", json);
    }

    [Fact]
    public void Settings_from_previous_version_keep_old_behavior()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, """
            { "version": 1, "rules": [ { "processName": "notepad.exe", "isEnabled": true } ] }
            """);

        var settings = new SettingsStore(FilePath).Load().Settings;

        Assert.Equal(AutoCenterMode.ListedApps, settings.AutoCenterMode);
        Assert.Equal(RuleAction.Center, Assert.Single(settings.Rules).Action);
        Assert.Equal(Hotkey.None, settings.UndoHotkey);
    }

    [Fact]
    public void File_is_human_readable()
    {
        new SettingsStore(FilePath).Save(new AppSettings());

        var json = File.ReadAllText(FilePath);

        Assert.Contains("\"centerHotkey\": \"Ctrl+Alt+C\"", json);
        Assert.Contains("\"oversize\": \"KeepSize\"", json);
    }

    [Fact]
    public void Disabled_hotkey_is_stored_as_empty_string()
    {
        var store = new SettingsStore(FilePath);
        store.Save(new AppSettings { CenterHotkey = Hotkey.None });

        Assert.Equal(Hotkey.None, store.Load().Settings.CenterHotkey);
    }

    [Fact]
    public void Missing_properties_fall_back_to_defaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, """{ "oversize": "ShrinkToFit" }""");

        var settings = new SettingsStore(FilePath).Load().Settings;

        Assert.Equal(OversizeBehavior.ShrinkToFit, settings.Oversize);
        Assert.Equal(AppSettings.DefaultCenterHotkey, settings.CenterHotkey);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "centerHotkey": "Ctrl+Banana" }""")]
    [InlineData("null")]
    public void Corrupted_file_is_backed_up_and_defaults_are_used(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, content);

        var result = new SettingsStore(FilePath).Load();

        Assert.Equal(SettingsLoadStatus.Corrupted, result.Status);
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.False(File.Exists(FilePath));
        Assert.NotNull(result.BackupPath);
        Assert.Equal(content, File.ReadAllText(result.BackupPath));
    }
}
