using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReimaginedLauncher.Utilities;

internal sealed class LocalPathSettings(string path)
{
    private static readonly string[] GlobalFields = [nameof(AppSettings.LauncherSettingsFolderOverride), nameof(AppSettings.LauncherInstallFolderOverride)];
    private static readonly string[] ProfileFields =
    [
        nameof(InstallationProfile.InstallDirectory), nameof(InstallationProfile.SteamDirectory),
        nameof(InstallationProfile.ProtonExecutable), nameof(InstallationProfile.SaveDirectory),
        nameof(InstallationProfile.ReimaginedModFolderOverride), nameof(InstallationProfile.BackupSaveDirectory),
        nameof(InstallationProfile.IsInstallDirectoryValidated), nameof(InstallationProfile.LutrisGameId),
        nameof(InstallationProfile.LutrisGameSlug), nameof(InstallationProfile.LutrisGameName)
    ];

    public void Apply(AppSettings settings)
    {
        JsonObject local;
        if (File.Exists(path))
        {
            local = ReadObject(File.ReadAllText(path));
        }
        else
        {
            local = Capture(settings);
            foreach (var field in GlobalFields) ClearForeignPath(local, field);
            foreach (var profile in local["Profiles"]!.AsArray().OfType<JsonObject>())
            {
                foreach (var field in ProfileFields) ClearForeignPath(profile, field);
                profile[nameof(InstallationProfile.IsInstallDirectoryValidated)] = false;
                if (profile["Type"]!.GetValue<int>() == (int)InstallationType.BattleNet
                    && profile[nameof(InstallationProfile.InstallDirectory)] is null)
                {
                    var game = WineRuntime.GetCrossOverGameCandidates(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                        .FirstOrDefault(File.Exists);
                    if (game is not null)
                        profile[nameof(InstallationProfile.InstallDirectory)] = Path.GetDirectoryName(game);
                }
            }
            Write(local);
        }

        ApplyFields(settings, local, GlobalFields);
        foreach (var profile in settings.Profiles)
        {
            var values = FindProfile(local, profile.Type);
            // Missing local profiles must not inherit Windows paths.
            ApplyFields(profile, values ?? CaptureFields(new InstallationProfile(), ProfileFields), ProfileFields);
        }
    }

    public string SerializeForSave(AppSettings settings, string sharedJson)
    {
        var shared = ReadObject(sharedJson);
        var output = JsonSerializer.SerializeToNode(settings)!.AsObject();
        CopyFields(shared, output, GlobalFields);
        foreach (var profile in output["Profiles"]!.AsArray().OfType<JsonObject>())
        {
            var original = FindProfile(shared, (InstallationType)profile["Type"]!.GetValue<int>());
            CopyFields(original ?? CaptureFields(new InstallationProfile(), ProfileFields), profile, ProfileFields);
        }
        Write(Capture(settings));
        return output.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonObject ReadObject(string json)
        => JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Expected a settings object.");

    private static JsonObject? FindProfile(JsonObject source, InstallationType type)
        => (source["Profiles"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(profile => profile["Type"]?.GetValue<int>() == (int)type);

    private static JsonObject Capture(AppSettings settings)
    {
        var result = CaptureFields(settings, GlobalFields);
        var profiles = new JsonArray();
        foreach (var profile in settings.Profiles)
        {
            var values = CaptureFields(profile, ProfileFields);
            values["Type"] = (int)profile.Type;
            profiles.Add(values);
        }
        result["Profiles"] = profiles;
        return result;
    }

    private static JsonObject CaptureFields(object value, string[] fields)
    {
        var result = new JsonObject();
        foreach (var field in fields)
            result[field] = JsonSerializer.SerializeToNode(value.GetType().GetProperty(field)!.GetValue(value));
        return result;
    }

    private static void ApplyFields(object target, JsonObject values, string[] fields)
    {
        foreach (var field in fields)
        {
            var property = target.GetType().GetProperty(field)!;
            property.SetValue(target, values[field]?.Deserialize(property.PropertyType));
        }
    }

    private static void CopyFields(JsonObject source, JsonObject target, string[] fields)
    {
        foreach (var field in fields)
        {
            if (source.TryGetPropertyValue(field, out var value)) target[field] = value?.DeepClone();
            else target.Remove(field);
        }
    }

    private static void ClearForeignPath(JsonObject values, string field)
    {
        if (values[field] is JsonValue value && value.TryGetValue<string>(out var text)
            && (!Path.IsPathRooted(text) || text.Contains('\\')))
            values[field] = null;
    }

    private void Write(JsonObject values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, values.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
