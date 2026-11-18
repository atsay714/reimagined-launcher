using Velopack;
using Velopack.Sources;

VelopackApp.Build().Run();
if (args.Length != 3) throw new ArgumentException("Expected feed, result file and phase.");
var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(args[0])));
if (!manager.IsInstalled) throw new InvalidOperationException("Fixture is not update enabled.");
if (args[2] == "after")
{
    File.WriteAllText(args[1], manager.CurrentVersion!.ToString());
    return;
}
var update = await manager.CheckForUpdatesAsync() ?? throw new InvalidOperationException("No fixture update found.");
await manager.DownloadUpdatesAsync(update);
manager.ApplyUpdatesAndRestart(update, restartArgs: [args[0], args[1], "after"]);
