using ReimaginedLauncher.Utilities;
using Xunit;

namespace ReimaginedLauncher.Tests;

public sealed class WineRuntimeTests
{
    private const string CrossOverWine = "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine";
    private const string Bottle = "/Users/player/Library/Application Support/CrossOver/Bottles/D2R";

    [Fact]
    public void CrossOverSelectsTheBottleByNameAndParentDirectory()
    {
        var environment = WineRuntime.GetPrefixEnvironment(CrossOverWine, Bottle + "/");

        Assert.Equal("D2R", environment["CX_BOTTLE"]);
        Assert.Equal("/Users/player/Library/Application Support/CrossOver/Bottles", environment["CX_BOTTLE_PATH"]);
        Assert.False(environment.ContainsKey("WINEPREFIX"));
    }

    [Fact]
    public void PlainWineUsesWinePrefix()
    {
        var environment = WineRuntime.GetPrefixEnvironment("/usr/bin/wine", "/home/player/.wine");

        Assert.Equal("/home/player/.wine", Assert.Single(environment).Value);
        Assert.True(environment.ContainsKey("WINEPREFIX"));
    }

    [Fact]
    public void PreviewEnvironmentIsEmptyWithoutAPrefix()
    {
        Assert.Equal(string.Empty, WineRuntime.FormatPrefixEnvironment(CrossOverWine, null));
        Assert.Equal("WINEPREFIX=\"/p\" ", WineRuntime.FormatPrefixEnvironment("/usr/bin/wine", "/p"));
    }

    [Fact]
    public void FindsGameInsideEveryCrossOverBottle()
    {
        var home = Path.Combine(Path.GetTempPath(), $"reimagined-wine-tests-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(home, "Library", "Application Support", "CrossOver", "Bottles", "D2R"));

            var candidate = Assert.Single(WineRuntime.GetCrossOverGameCandidates(home));

            Assert.EndsWith(Path.Combine("D2R", "drive_c", "Program Files (x86)", "Diablo II Resurrected", "D2R.exe"), candidate);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}
