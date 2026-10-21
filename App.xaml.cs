namespace StockPin;

using System.Windows.Media.Imaging;

public partial class App : Application
{
    private const string MutexName = @"Local\StockPin.SingleInstance.7C31";
    private const string ShowSignalName = @"Local\StockPin.ShowPanel.7C31";

    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ToolStripMenuItem? _trayAutoStart;
    private SettingsWindow? _settings;

    public static new App Current => (App)Application.Current;

    public AppConfig Config { get; private set; } = new();
    public QuoteService Quotes { get; } = new();
    public MainViewModel Vm { get; private set; } = null!;
    public MainWindow? Panel { get; private set; }
    public bool IsExiting { get; private set; }
    public bool PersistEnabled { get; private set; } = true;

    private bool _demo;
    private int _appliedTheme = -1;
    private bool? _appliedAutoStart;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Warn("unhandled: " + args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();

        string[] args = e.Args;
        _demo = HasFlag(args, "--demo");
        bool screenshot = HasFlag(args, "--screenshot");
        string shotPath = ValueAfter(args, "--screenshot") ?? "panel.png";
        int? themeArg = int.TryParse(ValueAfter(args, "--theme"), out var parsedTheme) ? parsedTheme : null;
        bool firstRun = false;

        if (screenshot || _demo)
        {
            PersistEnabled = false;
            Config = BuildDemoConfig(themeArg ?? 0);
        }
        else
        {
            bool created;
            _mutex = new Mutex(true, MutexName, out created);
            if (!created)
            {
                SignalExistingInstance();
                Shutdown();
                return;
            }

            firstRun = !File.Exists(ConfigService.FilePath);
            Config = ConfigService.Load();
            if (themeArg.HasValue) Config.Theme = Math.Clamp(themeArg.Value, 0, 3);
            PersistEnabled = true;
        }

        ThemeManager.Apply(themeArg ?? Config.Theme, Resources);
        _appliedTheme = ThemeManager.Current;

        Vm = new MainViewModel(Config, Quotes, seedDefaults: firstRun);
        if (screenshot || _demo) Vm.ApplyDemoData();

        Panel = new MainWindow(Vm, Config);

        if (screenshot)
        {
            Panel.Topmost = false;
            string view = ValueAfter(args, "--view") ?? "panel";
            if (string.Equals(view, "settings", StringComparison.OrdinalIgnoreCase))
            {
                var settings = new SettingsWindow(Config, Vm) { ShowInTaskbar = false };
                _settings = settings;
                settings.Show();
                settings.SelectTab(int.TryParse(ValueAfter(args, "--tab"), out var tab) ? tab : 0);
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() => CaptureScreenshot(settings, shotPath)));
            }
            else
            {
                Panel.Show();
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() => CaptureScreenshot(Panel, shotPath)));
            }
            return;
        }

        Panel.Show();
        CreateTray();
        ListenForSecondInstance();
        ApplySettings();
        Vm.SetPaused(false);
    }

    // ------------------------------------------------------------------ panel

    public void ShowPanel()
    {
        if (Panel == null) return;
        Panel.Show();
        if (Panel.WindowState == WindowState.Minimized) Panel.WindowState = WindowState.Normal;
        Panel.Activate();
        Vm.SetPaused(false);
    }

    public void HidePanel()
    {
        Panel?.Hide();
        Vm.SetPaused(true);
    }

    public void TogglePanel()
    {
        if (Panel == null) return;
        if (Panel.IsVisible) HidePanel();
        else ShowPanel();
    }

    public void ExitApp()
    {
        if (IsExiting) return;
        IsExiting = true;

        try
        {
            if (PersistEnabled) ConfigService.SaveNow(Config);
        }
        catch (Exception ex)
        {
            Log.Warn("save on exit failed: " + ex.Message);
        }

        Vm?.SetPaused(true);
        _tray?.Dispose();
        _tray = null;
        Quotes.Dispose();
        Panel?.CloseForExit();
        Shutdown();
    }

    // ------------------------------------------------------------------ settings

    public void ShowSettings()
    {
        if (IsExiting) return;

        if (_settings == null)
        {
            _settings = new SettingsWindow(Config, Vm);
            _settings.Closed += (_, _) => _settings = null;
            _settings.Show();
        }
        else
        {
            if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
            _settings.Activate();
        }
    }

    public void ApplySettings()
    {
        if (_appliedTheme != Config.Theme)
        {
            ThemeManager.Apply(Config.Theme, Resources);
            _appliedTheme = Config.Theme;
        }

        Vm.ShowCode = Config.ShowCode;
        Vm.IntervalSec = Config.IntervalSec;

        if (Panel != null)
        {
            Panel.Topmost = true;
            Panel.RefreshLockState();
            Panel.RefreshBackdrop();
        }

        if (PersistEnabled && !_demo && _appliedAutoStart != Config.AutoStart)
        {
            StartupService.Apply(Config.AutoStart);
            _appliedAutoStart = Config.AutoStart;
        }

        if (_trayAutoStart != null) _trayAutoStart.Checked = Config.AutoStart;

        if (PersistEnabled) ConfigService.ScheduleSave(Config);
    }

    public void ResetPanelPosition()
    {
        Config.Left = null;
        Config.Top = null;
        Panel?.MoveToDefaultPosition();
        if (PersistEnabled) ConfigService.ScheduleSave(Config);
    }

    public void SavePanelPosition(double left, double top)
    {
        if (!PersistEnabled) return;
        Config.Left = left;
        Config.Top = top;
        ConfigService.ScheduleSave(Config);
    }

    public void SaveSettingsPosition(double left, double top)
    {
        if (!PersistEnabled) return;
        Config.SettingsLeft = left;
        Config.SettingsTop = top;
        ConfigService.ScheduleSave(Config);
    }

    // ------------------------------------------------------------------ tray

    private void CreateTray()
    {
        try
        {
            _tray = new System.Windows.Forms.NotifyIcon
            {
                Text = "StockPin 股票盯盘",
                Icon = LoadAppIcon(),
                Visible = true
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            var show = new System.Windows.Forms.ToolStripMenuItem("显示 / 隐藏面板");
            show.Click += (_, _) => TogglePanel();

            var settings = new System.Windows.Forms.ToolStripMenuItem("设置…");
            settings.Click += (_, _) => ShowSettings();

            _trayAutoStart = new System.Windows.Forms.ToolStripMenuItem("开机自启")
            {
                CheckOnClick = true,
                Checked = Config.AutoStart
            };
            _trayAutoStart.Click += (_, _) =>
            {
                Config.AutoStart = _trayAutoStart.Checked;
                ApplySettings();
            };

            var exit = new System.Windows.Forms.ToolStripMenuItem("退出");
            exit.Click += (_, _) => ExitApp();

            menu.Items.Add(show);
            menu.Items.Add(settings);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(_trayAutoStart);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(exit);

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (_, _) => TogglePanel();
        }
        catch (Exception ex)
        {
            Log.Warn("tray init failed: " + ex.Message);
        }
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon != null) return icon;
            }
        }
        catch
        {
            // fall through
        }

        return System.Drawing.SystemIcons.Application;
    }

    // ------------------------------------------------------------------ single instance

    private void ListenForSecondInstance()
    {
        try
        {
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (!_showSignal.WaitOne()) break;
                    }
                    catch
                    {
                        break;
                    }

                    Dispatcher.BeginInvoke(new Action(ShowPanel));
                }
            })
            {
                IsBackground = true,
                Name = "StockPin.ShowSignal"
            };
            thread.Start();
        }
        catch (Exception ex)
        {
            Log.Warn("instance listener failed: " + ex.Message);
        }
    }

    private static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var handle))
            {
                handle.Set();
                handle.Dispose();
            }
        }
        catch
        {
            // ignored
        }
    }

    // ------------------------------------------------------------------ demo / screenshot

    private static bool HasFlag(string[] args, string flag)
        => args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? ValueAfter(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    private static AppConfig BuildDemoConfig(int theme)
    {
        return new AppConfig
        {
            Theme = theme,
            ShowCode = true,
            IntervalSec = 5,
            Stocks = new List<StockConfig>
            {
                new() { SecId = "1.601872", Code = "601872", Name = "招商轮船", Market = MarketKind.A, Qty = 2000, Cost = 19.85 },
                new() { SecId = "116.00700", Code = "00700", Name = "腾讯控股", Market = MarketKind.HK, Qty = 100, Cost = 388.00 },
                new() { SecId = "106.FRO", Code = "FRO", Name = "Frontline", Market = MarketKind.US, Qty = 300, Cost = 48.00 },
                new() { SecId = "105.AAPL", Code = "AAPL", Name = "苹果", Market = MarketKind.US, Qty = 20, Cost = 330.00 },
                new() { SecId = "0.300750", Code = "300750", Name = "宁德时代", Market = MarketKind.A }
            }
        };
    }

    private void CaptureScreenshot(Window window, string path)
    {
        try
        {
            window.UpdateLayout();
            var root = (FrameworkElement)window.Content;
            double w = root.ActualWidth;
            double h = root.ActualHeight;
            if (w < 1 || h < 1) throw new InvalidOperationException("empty visual");

            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(w * 2), (int)Math.Ceiling(h * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream = File.Create(path);
            encoder.Save(stream);
            Log.Info($"screenshot saved: {path} ({w}x{h})");
        }
        catch (Exception ex)
        {
            Log.Warn("screenshot failed: " + ex.Message);
        }
        finally
        {
            ExitApp();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // ignored
        }

        _mutex?.Dispose();
        _showSignal?.Dispose();
        base.OnExit(e);
    }
}
