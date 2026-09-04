using System.Text.Json;

namespace WinPieGestures.WinUI.Services;

public sealed class ConfigurationService
{
    private readonly object _sync = new();
    private readonly string _configPath;

    public ConfigurationService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarPie");
        Directory.CreateDirectory(folder);
        _configPath = Path.Combine(folder, "config.json");
        Current = LoadCore();
    }

    public AppConfig Current { get; private set; }

    public event EventHandler? Changed;

    public void Update(Action<AppConfig> update)
    {
        lock (_sync)
        {
            update(Current);
            Normalize(Current);
            SaveCore();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public WheelProfile GetGlobalProfile()
    {
        lock (_sync)
        {
            return Current.Profiles.FirstOrDefault(profile =>
                       profile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
                   ?? Current.Profiles[0];
        }
    }

    public void ExportTo(string path)
    {
        lock (_sync)
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(Current, options));
        }
    }

    public void ImportFrom(string path)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        AppConfig imported = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("配置文件内容为空或格式不受支持。");
        Normalize(imported);
        lock (_sync)
        {
            Current = imported;
            SaveCore();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private AppConfig LoadCore()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                AppConfig? config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_configPath), options);
                if (config is not null)
                {
                    Normalize(config);
                    return config;
                }
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Unable to load configuration; defaults will be used", exception);
        }

        AppConfig fallback = CreateDefault();
        Current = fallback;
        SaveCore();
        return fallback;
    }

    private void SaveCore()
    {
        try
        {
            JsonSerializerOptions options = new() { WriteIndented = true };
            string tempPath = _configPath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Current, options));
            File.Move(tempPath, _configPath, true);
        }
        catch (Exception exception)
        {
            AppLog.Error("Unable to save configuration", exception);
        }
    }

    private static void Normalize(AppConfig config)
    {
        config.Trigger ??= new TriggerConfig();
        config.TouchTrigger ??= new TouchTriggerConfig();
        config.Profiles ??= [];
        config.CustomColorPresets ??= [];
        config.BlacklistedProcesses ??= [];
        config.WhitelistedProcesses ??= [];

        config.TouchTrigger.LongPressDelayMs = Math.Clamp(config.TouchTrigger.LongPressDelayMs, 250, 1200);
        config.TouchTrigger.HoldMovementTolerance = Math.Clamp(config.TouchTrigger.HoldMovementTolerance, 6, 48);
        config.TouchTrigger.SwipeThreshold = Math.Clamp(config.TouchTrigger.SwipeThreshold, 18, 120);
        config.TouchTrigger.DirectionCount = config.TouchTrigger.DirectionCount == 4 ? 4 : 8;
        config.TouchTrigger.PassThroughUnhandledTouch = true;
        config.WheelRadius = Math.Clamp(config.WheelRadius, 92, 240);
        config.InnerRadius = Math.Clamp(config.InnerRadius, 28, config.WheelRadius - 32);
        config.SectorGap = Math.Clamp(config.SectorGap, 0, 16);
        config.SectorIconSize = Math.Clamp(config.SectorIconSize, 14, 32);
        config.SectorFontSize = Math.Clamp(config.SectorFontSize, 9, 18);
        config.OuterEscapeDistance = Math.Clamp(config.OuterEscapeDistance, 120, 420);

        if (config.Profiles.Count == 0)
        {
            config.Profiles.Add(CreateGlobalProfile());
        }

        foreach (WheelProfile profile in config.Profiles)
        {
            profile.Actions ??= [];
            foreach (ActionItem action in profile.Actions)
            {
                action.SubActions ??= [];
            }
        }
    }

    private static AppConfig CreateDefault()
    {
        AppConfig config = new();
        config.Profiles.Add(CreateGlobalProfile());
        return config;
    }

    private static WheelProfile CreateGlobalProfile() => new()
    {
        ProcessName = "Global",
        SectorCount = 8,
        Actions =
        [
            new() { Type = "Hotkey", Name = "复制", Parameter = "Ctrl+C", IconKey = "Copy" },
            new() { Type = "Hotkey", Name = "撤销", Parameter = "Ctrl+Z", IconKey = "Undo" },
            new() { Type = "Hotkey", Name = "粘贴", Parameter = "Ctrl+V", IconKey = "Paste" },
            new() { Type = "System", Name = "音量增", Parameter = "VolumeUp", IconKey = "VolumeUp" },
            new() { Type = "System", Name = "显示桌面", Parameter = "ShowDesktop", IconKey = "ShowDesktop" },
            new() { Type = "System", Name = "音量减", Parameter = "VolumeDown", IconKey = "VolumeDown" },
            new() { Type = "System", Name = "截图", Parameter = "Screenshot", IconKey = "Screenshot" },
            new() { Type = "Hotkey", Name = "重做", Parameter = "Ctrl+Y", IconKey = "Redo" }
        ]
    };
}
