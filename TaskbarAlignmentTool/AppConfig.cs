using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskbarAlignmentTool;

/// <summary>Taskbar alignment options.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlignmentOption { Left = 0, Center = 1 }

/// <summary>Combine taskbar buttons options.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CombineButtonsOption { Always = 0, WhenFull = 1, Never = 2 }

/// <summary>Taskbar size options.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskbarSizeOption { Small = 0, Default = 1, Large = 2 }

/// <summary>How to measure the display width.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResolutionMode { Effective, Physical }

/// <summary>A resolution profile that activates at a minimum effective width.</summary>
public sealed class ProfileConfig
{
    public string Name { get; set; } = "Default";
    public int MinWidth { get; set; }
    public AlignmentOption Alignment { get; set; } = AlignmentOption.Center;
    public CombineButtonsOption CombineButtons { get; set; } = CombineButtonsOption.Always;
    public TaskbarSizeOption TaskbarSize { get; set; } = TaskbarSizeOption.Default;
}

/// <summary>Root configuration model.</summary>
public sealed class AppConfig
{
    private const string SchemaUrl = "https://raw.githubusercontent.com/duncanbeard/taskbar-alignment-tool/main/schema/config.schema.json";

    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = SchemaUrl;

    public int RefreshIntervalMs { get; set; } = 60000;
    public bool ShowNotifications { get; set; } = true;
    public int NotificationDurationMs { get; set; } = 1000;
    public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.Effective;
    public List<ProfileConfig> Profiles { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Returns the default config with 3 preset profiles.</summary>
    public static AppConfig CreateDefault() => new()
    {
        RefreshIntervalMs = 60000,
        ShowNotifications = true,
        NotificationDurationMs = 1000,
        ResolutionMode = ResolutionMode.Effective,
        Profiles =
        [
            new ProfileConfig
            {
                Name = "Compact",
                MinWidth = 0,
                Alignment = AlignmentOption.Left,
                CombineButtons = CombineButtonsOption.Never,
                TaskbarSize = TaskbarSizeOption.Small
            },
            new ProfileConfig
            {
                Name = "Standard",
                MinWidth = 1920,
                Alignment = AlignmentOption.Left,
                CombineButtons = CombineButtonsOption.WhenFull,
                TaskbarSize = TaskbarSizeOption.Default
            },
            new ProfileConfig
            {
                Name = "Ultrawide",
                MinWidth = 2560,
                Alignment = AlignmentOption.Center,
                CombineButtons = CombineButtonsOption.Always,
                TaskbarSize = TaskbarSizeOption.Default
            }
        ]
    };

    /// <summary>Gets the config file path in LocalAppData (MSIX-compatible).</summary>
    public static string GetConfigPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskbarAlignmentTool");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    /// <summary>Loads config from disk, creating a default file if it doesn't exist.
    /// Migrates from legacy exe-adjacent location if found.</summary>
    public static AppConfig Load()
    {
        var path = GetConfigPath();

        // Migrate from legacy exe-adjacent config if it exists
        MigrateLegacyConfig(path);

        if (!File.Exists(path))
        {
            var defaults = CreateDefault();
            defaults.Save();
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    /// <summary>Saves this config to disk.</summary>
    public void Save()
    {
        var path = GetConfigPath();
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Returns the best matching profile for the given effective width.
    /// Profiles are matched by the highest minWidth that is &lt;= effectiveWidth.
    /// </summary>
    public ProfileConfig? ResolveProfile(int effectiveWidth)
    {
        return Profiles
            .Where(p => effectiveWidth >= p.MinWidth)
            .OrderByDescending(p => p.MinWidth)
            .FirstOrDefault();
    }

    private static void MigrateLegacyConfig(string newPath)
    {
        if (File.Exists(newPath))
            return;

        var legacyDir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
        var legacyPath = Path.Combine(legacyDir, "config.json");
        if (File.Exists(legacyPath))
        {
            try
            {
                File.Copy(legacyPath, newPath, overwrite: false);
                File.Delete(legacyPath);
            }
            catch
            {
                // Best-effort migration; ignore failures
            }
        }
    }
}
