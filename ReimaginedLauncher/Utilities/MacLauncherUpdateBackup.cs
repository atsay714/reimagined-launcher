using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ReimaginedLauncher.Utilities;

internal static class MacLauncherUpdateBackup
{
    internal static async Task<string> CreateAsync(string appDirectory, string backupRoot)
    {
        appDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory));
        backupRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupRoot));
        if (backupRoot.Equals(appDirectory, StringComparison.OrdinalIgnoreCase)
            || backupRoot.StartsWith(appDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The rollback folder must be outside the application bundle.");
        if (!appDirectory.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(appDirectory, "Contents", "Info.plist"))
            || !File.Exists(Path.Combine(appDirectory, "Contents", "MacOS", "ReimaginedLauncher")))
            throw new InvalidOperationException("Could not locate the installed Mac launcher bundle for backup.");

        var destination = Path.Combine(backupRoot, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}", Path.GetFileName(appDirectory));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var start = new ProcessStartInfo("/usr/bin/ditto") { UseShellExecute = false, RedirectStandardError = true };
        start.ArgumentList.Add(appDirectory);
        start.ArgumentList.Add(destination);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the launcher backup.");
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new IOException("Could not back up the launcher: " + await error);
        return destination;
    }
}
