using ReimaginedLauncher.Utilities;
using Xunit;

namespace ReimaginedLauncher.Tests;

public sealed class MacLauncherUpdateTests
{
    [Theory]
    [InlineData(true, "https://github.com/atsay714/reimagined-launcher-macos")]
    [InlineData(false, "https://github.com/D2R-Reimagined/reimagined-launcher")]
    public void PlatformSelectsItsOwnReleaseFeed(bool macOS, string expected)
        => Assert.Equal(expected, LauncherUpdateService.GetRepositoryUrl(macOS));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ActiveWorkPreventsRestart(bool gameRunning, bool busy)
        => Assert.NotNull(LauncherUpdateService.GetRestartBlockReason(gameRunning, busy));

    [Fact]
    public void IdleLauncherCanRestart()
        => Assert.Null(LauncherUpdateService.GetRestartBlockReason(false, false));

    [Fact]
    public async Task BackupPreservesBundleAndResourceSymlinks()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var root = Path.Combine(Path.GetTempPath(), "mac-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var app = Path.Combine(root, "Original.app");
            Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
            Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources", "Assets"));
            await File.WriteAllTextAsync(Path.Combine(app, "Contents", "Info.plist"), "fixture");
            await File.WriteAllTextAsync(Path.Combine(app, "Contents", "MacOS", "ReimaginedLauncher"), "original");
            Directory.CreateSymbolicLink(Path.Combine(app, "Contents", "MacOS", "Assets"), "../Resources/Assets");

            var copy = await MacLauncherUpdateBackup.CreateAsync(app, Path.Combine(root, "rollback"));

            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(copy, "Contents", "MacOS", "ReimaginedLauncher")));
            Assert.Equal("../Resources/Assets", new DirectoryInfo(Path.Combine(copy, "Contents", "MacOS", "Assets")).LinkTarget);
            Assert.True(File.Exists(Path.Combine(app, "Contents", "Info.plist")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BackupRejectsRecursiveDestinationWithoutChangingOriginal()
    {
        var root = Path.Combine(Path.GetTempPath(), "mac-update-" + Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "Original.app");
        Directory.CreateDirectory(app);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(app, "sentinel"), "original");
            await Assert.ThrowsAsync<InvalidOperationException>(() => MacLauncherUpdateBackup.CreateAsync(app, Path.Combine(app, "rollback")));
            Assert.Equal("original", await File.ReadAllTextAsync(Path.Combine(app, "sentinel")));
        }
        finally { Directory.Delete(root, true); }
    }
}
