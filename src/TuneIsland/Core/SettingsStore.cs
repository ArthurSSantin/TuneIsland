using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TuneIsland.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load() => LoadFrom(AppPaths.SettingsFile);

    public static AppSettings LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            return Deserialize(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao carregar settings.json; usando padrões.");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings) => SaveTo(settings, AppPaths.SettingsFile);

    public static void SaveTo(AppSettings settings, string path)
    {
        try
        {
            settings.Clamp();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Serialize(settings));
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao salvar settings.json.");
        }
    }

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    public static AppSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        settings.Clamp();
        return settings;
    }
}
