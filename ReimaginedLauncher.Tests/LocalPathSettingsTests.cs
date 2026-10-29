using System.Text.Json;
using ReimaginedLauncher.Utilities;
using Xunit;

namespace ReimaginedLauncher.Tests;

public sealed class LocalPathSettingsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "local-paths-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void MacPathsStayLocalWhilePluginChangesAreShared()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "paths.json");
        File.WriteAllText(path, """{"Profiles":[{"Type":0,"InstallDirectory":"/mac/game","SaveDirectory":null,"IsInstallDirectoryValidated":true}],"LauncherInstallFolderOverride":"/mac/launcher"}""");
        var shared = """{"Profiles":[{"Type":0,"InstallDirectory":"C:\\Game","SaveDirectory":"C:\\Saves","IsInstallDirectoryValidated":true}],"LauncherInstallFolderOverride":"C:\\Launcher"}""";
        var settings = JsonSerializer.Deserialize<AppSettings>(shared)!;
        var local = new LocalPathSettings(path);
        local.Apply(settings);
        Assert.Equal("/mac/game", settings.Profiles[0].InstallDirectory);
        Assert.Null(settings.Profiles[0].SaveDirectory);
        settings.Profiles[0].InstallDirectory = "/mac/new-game";
        settings.Profiles[0].Plugins.Add(new PluginRegistration { FolderName = "MyPlugin", IsEnabled = true });
        var saved = JsonSerializer.Deserialize<AppSettings>(local.SerializeForSave(settings, shared))!;
        Assert.Equal(@"C:\Game", saved.Profiles[0].InstallDirectory);
        Assert.Equal(@"C:\Saves", saved.Profiles[0].SaveDirectory);
        Assert.Equal(@"C:\Launcher", saved.LauncherInstallFolderOverride);
        Assert.True(Assert.Single(saved.Profiles[0].Plugins).IsEnabled);
        Assert.Equal("/mac/new-game", settings.Profiles[0].InstallDirectory);
        var reloaded = JsonSerializer.Deserialize<AppSettings>(shared)!;
        new LocalPathSettings(path).Apply(reloaded);
        Assert.Equal("/mac/new-game", reloaded.Profiles[0].InstallDirectory);
    }

    [Fact]
    public void SavePreservesNewWindowsPathsAndMatchesProfilesByType()
    {
        Directory.CreateDirectory(root);
        var local = new LocalPathSettings(Path.Combine(root, "paths.json"));
        var settings = new AppSettings { Profiles = [new() { Type = InstallationType.BattleNet, InstallDirectory = "/mac/game" }] };
        var shared = """{"Profiles":[{"Type":1,"InstallDirectory":"D:\\Steam"},{"Type":0,"InstallDirectory":"D:\\MovedGame"}]}""";
        var result = JsonSerializer.Deserialize<AppSettings>(local.SerializeForSave(settings, shared))!;
        Assert.Equal(@"D:\MovedGame", result.Profiles[0].InstallDirectory);
        Assert.Null(result.Profiles[0].SaveDirectory);
    }

    [Fact]
    public void FirstRunClearsWindowsSaveAndBackupPaths()
    {
        Directory.CreateDirectory(root);
        var local = new LocalPathSettings(Path.Combine(root, "paths.json"));
        var settings = new AppSettings
        {
            LauncherInstallFolderOverride = @"C:\Launcher",
            Profiles = [new() { Type = InstallationType.BattleNet, InstallDirectory = "/mac/game",
                SaveDirectory = @"C:\Saves", BackupSaveDirectory = @"D:\Backups" }]
        };
        local.Apply(settings);
        Assert.Equal("/mac/game", settings.Profiles[0].InstallDirectory);
        Assert.Null(settings.Profiles[0].SaveDirectory);
        Assert.Null(settings.Profiles[0].BackupSaveDirectory);
        Assert.Null(settings.LauncherInstallFolderOverride);
    }

    [Fact]
    public void UnreadableSharedSettingsCannotBeOverwrittenWithMacPaths()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "paths.json");
        File.WriteAllText(path, "{}");
        var local = new LocalPathSettings(path);
        Assert.ThrowsAny<JsonException>(() => local.SerializeForSave(new AppSettings(), "{broken"));
        Assert.Equal("{}", File.ReadAllText(path));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
