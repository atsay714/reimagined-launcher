using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReimaginedLauncher.Utilities;

/// <summary>Runs the Windows game through Wine on Linux and through CrossOver (or Wine) on macOS.</summary>
internal static class WineRuntime
{
    private const string CrossOverBundleName = "CrossOver.app";
    private static readonly string[] CrossOverWinePath = ["Contents", "SharedSupport", "CrossOver", "bin", "wine"];

    public static bool IsRequired => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    public static string? FindExecutable()
    {
        if (OperatingSystem.IsMacOS())
        {
            foreach (var app in GetCrossOverAppCandidates())
            {
                var wine = Path.Combine([app, .. CrossOverWinePath]);
                if (File.Exists(wine)) return wine;
            }
        }

        return GameLauncherService.FindExecutableOnPath("wine");
    }

    /// <summary>
    /// CrossOver ignores WINEPREFIX and resolves the bottle from its name and
    /// parent directory instead.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetPrefixEnvironment(string winePath, string winePrefix)
    {
        if (!IsCrossOver(winePath))
            return new Dictionary<string, string> { ["WINEPREFIX"] = winePrefix };

        var bottle = winePrefix.TrimEnd(Path.DirectorySeparatorChar);
        return new Dictionary<string, string>
        {
            ["CX_BOTTLE"] = Path.GetFileName(bottle),
            ["CX_BOTTLE_PATH"] = Path.GetDirectoryName(bottle) ?? string.Empty
        };
    }

    public static string FormatPrefixEnvironment(string winePath, string? winePrefix)
        => winePrefix is null
            ? string.Empty
            : string.Concat(GetPrefixEnvironment(winePath, winePrefix).Select(pair => $"{pair.Key}=\"{pair.Value}\" "));

    public static bool IsCrossOver(string winePath)
        => winePath.Contains($"{Path.DirectorySeparatorChar}{CrossOverBundleName}{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>D2R.exe inside every CrossOver bottle, for install detection on macOS.</summary>
    public static IEnumerable<string> GetCrossOverGameCandidates(string home)
    {
        var bottles = Path.Combine(home, "Library", "Application Support", "CrossOver", "Bottles");
        if (!Directory.Exists(bottles)) return [];

        try
        {
            return Directory.EnumerateDirectories(bottles)
                .Select(bottle => Path.Combine(bottle, "drive_c", "Program Files (x86)", "Diablo II Resurrected", "D2R.exe"))
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// macOS has no /proc, and Wine processes report the loader as their name,
    /// so the Windows executable is matched against each process's arguments.
    /// </summary>
    public static int[] FindMacProcesses(string executablePath)
    {
        var fileName = Path.GetFileName(executablePath);
        var result = new List<int>();
        try
        {
            using var ps = Process.Start(new ProcessStartInfo("/bin/ps", "-axww -o pid= -o args=")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (ps is null) return [];

            var output = ps.StandardOutput.ReadToEnd();
            ps.WaitForExit();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimStart();
                var space = trimmed.IndexOf(' ');
                if (space <= 0 || !int.TryParse(trimmed[..space], out var pid)) continue;

                var args = trimmed[(space + 1)..].TrimStart();
                // Skip the CrossOver launch wrappers; only the game process itself starts with its path.
                if (args.EndsWith($"\\{fileName}", StringComparison.OrdinalIgnoreCase)
                    || args.Contains($"\\{fileName} ", StringComparison.OrdinalIgnoreCase)
                    || args.StartsWith(executablePath, StringComparison.Ordinal))
                {
                    if (args.Contains("winewrapper.exe", StringComparison.OrdinalIgnoreCase)
                        || args.Contains("/bin/wine", StringComparison.Ordinal))
                        continue;
                    result.Add(pid);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        return result.ToArray();
    }

    private static IEnumerable<string> GetCrossOverAppCandidates()
    {
        yield return Path.Combine("/Applications", CrossOverBundleName);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, "Applications", CrossOverBundleName);
    }
}
