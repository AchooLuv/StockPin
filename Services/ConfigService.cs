namespace StockPin.Services;

using System.Text.Json.Serialization;

/// <summary>Loads / debounce-saves the JSON config in %APPDATA%\StockPin.</summary>
public static class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static DispatcherTimer? _debounce;
    private static AppConfig? _pending;

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StockPin");

    public static string FilePath => Path.Combine(Folder, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppConfig();
            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config == null) return new AppConfig();
            config.Stocks ??= new List<StockConfig>();
            config.Theme = Math.Clamp(config.Theme, 0, 3);
            config.Opacity = Math.Clamp(config.Opacity, 0.55, 1.0);
            config.IntervalSec = config.IntervalSec is 3 or 5 or 10 or 30 or 60 ? config.IntervalSec : 5;
            return config;
        }
        catch (Exception ex)
        {
            Log.Warn("config load failed: " + ex.Message);
            TryBackupBrokenConfig();
            return new AppConfig();
        }
    }

    /// <summary>Coalesces frequent changes (slider drags) into one write.</summary>
    public static void ScheduleSave(AppConfig config)
    {
        _pending = config;
        if (_debounce == null)
        {
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _debounce.Tick += (_, _) =>
            {
                _debounce!.Stop();
                if (_pending != null) SaveNow(_pending);
            };
        }

        _debounce.Stop();
        _debounce.Start();
    }

    public static void SaveNow(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            File.Move(temp, FilePath, true);
        }
        catch (Exception ex)
        {
            Log.Warn("config save failed: " + ex.Message);
        }
    }

    private static void TryBackupBrokenConfig()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var backup = Path.Combine(Folder, $"config.broken-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(FilePath, backup, true);
        }
        catch
        {
            // ignored
        }
    }
}
