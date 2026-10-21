namespace StockPin.ViewModels;

/// <summary>State for the settings window; every write goes straight into <see cref="AppConfig"/>.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly MainViewModel _main;

    public SettingsViewModel(AppConfig config, MainViewModel main)
    {
        _config = config;
        _main = main;
    }

    public event Action? Changed;

    public ObservableCollection<StockEntry> Stocks => _main.Stocks;
    public ObservableCollection<StockSearchResult> Results { get; } = new();
    public IReadOnlyList<ThemePalette> Themes => ThemeManager.Themes;

    public int ThemeIndex
    {
        get => _config.Theme;
        set
        {
            int clamped = Math.Clamp(value, 0, Themes.Count - 1);
            if (_config.Theme == clamped) return;
            _config.Theme = clamped;
            OnPropertyChanged();
            Raise();
        }
    }

    public bool ShowCode
    {
        get => _config.ShowCode;
        set
        {
            if (_config.ShowCode == value) return;
            _config.ShowCode = value;
            OnPropertyChanged();
            Raise();
        }
    }

    public bool AutoStart
    {
        get => _config.AutoStart;
        set
        {
            if (_config.AutoStart == value) return;
            _config.AutoStart = value;
            OnPropertyChanged();
            Raise();
        }
    }

    public int IntervalSec
    {
        get => _config.IntervalSec;
        set
        {
            int clamped = value is 1 or 2 or 3 or 5 or 10 or 30 or 60 ? value : 3;
            if (_config.IntervalSec == clamped) return;
            _config.IntervalSec = clamped;
            OnPropertyChanged();
            Raise();
        }
    }

    /// <summary>Panel plate opacity in percent (0 = pure text).</summary>
    public double BackdropPercent
    {
        get => _config.Backdrop;
        set
        {
            int clamped = Math.Clamp((int)Math.Round(value), 0, 100);
            if (_config.Backdrop == clamped) return;
            _config.Backdrop = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackdropLabel));
            Raise();
        }
    }

    public string BackdropLabel => _config.Backdrop + "%";

    public bool Locked
    {
        get => _config.Locked;
        set
        {
            if (_config.Locked == value) return;
            _config.Locked = value;
            OnPropertyChanged();
            Raise();
        }
    }

    public string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return "v" + (version?.ToString(3) ?? "1.0.0");
        }
    }

    /// <summary>Re-reads the config so changes made on the panel (e.g. the pin) show up here too.</summary>
    public void RefreshFromConfig()
    {
        OnPropertyChanged(nameof(ThemeIndex));
        OnPropertyChanged(nameof(ShowCode));
        OnPropertyChanged(nameof(AutoStart));
        OnPropertyChanged(nameof(IntervalSec));
        OnPropertyChanged(nameof(BackdropPercent));
        OnPropertyChanged(nameof(BackdropLabel));
        OnPropertyChanged(nameof(Locked));
    }

    private void Raise() => Changed?.Invoke();
}
