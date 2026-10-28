namespace StockPin.Views;

using System.Windows.Interop;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush ClickThrough = CreateClickThrough();

    private readonly MainViewModel _vm;
    private readonly AppConfig _config;
    private ContextMenu? _menu;
    private StockEntry? _ctxStock;
    private bool _allowClose;

    private int _backdropTheme = -1;
    private LinearGradientBrush? _plate;
    private GradientStop? _plateTop;
    private GradientStop? _plateBottom;
    private SolidColorBrush? _plateBorder;
    private Color _plateTopColor;
    private Color _plateBottomColor;
    private Color _plateBorderColor;

    public MainWindow(MainViewModel vm, AppConfig config)
    {
        InitializeComponent();
        _vm = vm;
        _config = config;
        DataContext = vm;

        Topmost = true;

        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        BuildMenu();
    }

    private static App Host => App.Current;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyWorkAreaLimits();

        if (_config.Left is double left && _config.Top is double top)
        {
            Left = left;
            Top = top;
            ClampToScreen();
        }
        else
        {
            MoveToDefaultPosition();
            Host.SavePanelPosition(Left, Top);
        }

        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget == null)
            Log.Warn("panel has no composition target; DPI scaling may be wrong");

        RefreshLockState();
        RefreshBackdrop();
    }

    /// <summary>
    /// Paints the translucent plate behind the text. 0% keeps the pure-text look;
    /// higher values guarantee readability over games / busy wallpapers.
    /// </summary>
    public void RefreshBackdrop()
    {
        var palette = ThemeManager.CurrentPalette;
        double strength = Math.Clamp(_config.Backdrop, 0, 100) / 100.0;

        if (strength <= 0.005)
        {
            Root.Background = ClickThrough;
            Root.BorderThickness = new Thickness(0);
            Root.CornerRadius = new CornerRadius(0);
            return;
        }

        // Build the plate once per theme, then only mutate the alpha values while
        // the backdrop slider moves. This avoids a new brush per slider tick.
        if (_plate == null || _backdropTheme != ThemeManager.Current)
        {
            _backdropTheme = ThemeManager.Current;
            _plateTopColor = ThemePalette.Parse(palette.BgTop);
            _plateBottomColor = ThemePalette.Parse(palette.BgBottom);
            _plateBorderColor = ThemePalette.Parse(palette.Border);

            _plateTop = new GradientStop(WithAlpha(_plateTopColor, 255), 0);
            _plateBottom = new GradientStop(WithAlpha(_plateBottomColor, 255), 1);
            _plate = new LinearGradientBrush(new GradientStopCollection { _plateTop, _plateBottom },
                new Point(0, 0), new Point(0.55, 1));
            _plateBorder = new SolidColorBrush(WithAlpha(_plateBorderColor, 255));
        }

        byte alpha = (byte)Math.Round(255 * strength);
        _plateTop!.Color = WithAlpha(_plateTopColor, alpha);
        _plateBottom!.Color = WithAlpha(_plateBottomColor, alpha);
        _plateBorder!.Color = WithAlpha(_plateBorderColor, (byte)Math.Min(255, 150 + 105 * strength));

        Root.Background = _plate;
        Root.BorderBrush = _plateBorder;
        Root.BorderThickness = new Thickness(1);
        Root.CornerRadius = new CornerRadius(12);
    }

    private static SolidColorBrush CreateClickThrough()
    {
        var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color color, byte alpha)
        => Color.FromArgb(alpha, color.R, color.G, color.B);

    private void ApplyWorkAreaLimits()
    {
        var area = WorkArea();
        MaxHeight = Math.Max(240, area.Height - 48);
        StockScroll.MaxHeight = Math.Max(150, area.Height * 0.56);
    }

    public void RefreshLockState()
    {
        var buttonBg = TryFindResource("ButtonBg") as Brush ?? Brushes.White;
        var buttonText = TryFindResource("ButtonText") as Brush ?? Brushes.Black;
        var muted = TryFindResource("TextMuted") as Brush ?? Brushes.Gray;

        // Pinned: upright pin on a solid chip. Unpinned: lying pin, muted, no chip.
        LockGlyph.Text = _config.Locked ? "\uE840" : "\uE718";
        LockButton.ToolTip = _config.Locked ? "取消固定" : "固定面板位置";
        LockGlyph.Foreground = _config.Locked ? buttonText : muted;
        LockButton.Background = _config.Locked ? buttonBg : Brushes.Transparent;
        Root.Cursor = _config.Locked ? Cursors.Arrow : Cursors.SizeAll;
    }

    public void MoveToDefaultPosition()
    {
        var area = WorkArea();
        Left = Math.Max(area.Left, area.Right - Width - 28);
        Top = area.Top + 80;
        ClampToScreen();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    // ------------------------------------------------------------------ window drag

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_config.Locked) return;
        if (IsInsideButton(e.OriginalSource as DependencyObject)) return;

        // Let Windows run its own move loop. Doing this by hand while the window
        // follows the cursor causes a move → mouse-move → move feedback loop.
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        SnapToEdges();
        ClampToScreen();
        Host.SavePanelPosition(Left, Top);
    }

    private void SnapToEdges()
    {
        var area = WorkArea();
        const double gap = 6;
        const double threshold = 14;

        if (Math.Abs(Left - area.Left) <= threshold) Left = area.Left + gap;
        else if (Math.Abs(Left + ActualWidth - area.Right) <= threshold) Left = area.Right - ActualWidth - gap;

        if (Math.Abs(Top - area.Top) <= threshold) Top = area.Top + gap;
        else if (Math.Abs(Top + ActualHeight - area.Bottom) <= threshold) Top = area.Bottom - ActualHeight - gap;
    }

    private void ClampToScreen()
    {
        double minLeft = SystemParameters.VirtualScreenLeft;
        double minTop = SystemParameters.VirtualScreenTop;
        double maxLeft = minLeft + SystemParameters.VirtualScreenWidth;
        double maxTop = minTop + SystemParameters.VirtualScreenHeight;

        double keepVisibleX = Math.Min(120, Width);
        double keepVisibleY = 60;

        if (Left < minLeft - Width + keepVisibleX || Left > maxLeft - keepVisibleX ||
            Top < minTop - 8 || Top > maxTop - keepVisibleY)
        {
            MoveToDefaultPosition();
            return;
        }

        Left = Math.Clamp(Left, minLeft - Width + keepVisibleX, maxLeft - keepVisibleX);
        Top = Math.Clamp(Top, minTop - 8, maxTop - keepVisibleY);
    }

    private Rect WorkArea()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        double scaleX = 1, scaleY = 1;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            scaleX = source.CompositionTarget.TransformToDevice.M11;
            scaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        var screen = hwnd != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(hwnd)
            : System.Windows.Forms.Screen.PrimaryScreen;

        if (screen == null) return SystemParameters.WorkArea;

        var r = screen.WorkingArea;
        if (scaleX <= 0) scaleX = 1;
        if (scaleY <= 0) scaleY = 1;

        return new Rect(r.Left / scaleX, r.Top / scaleY, r.Width / scaleX, r.Height / scaleY);
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase) return true;
            if (source is System.Windows.Controls.Primitives.ScrollBar) return true;
            if (source is System.Windows.Controls.Primitives.Thumb) return true;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
        }
        return false;
    }

    // ------------------------------------------------------------------ header buttons

    private void Lock_Click(object sender, RoutedEventArgs e) => ToggleLock();

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshNowAsync();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Host.ShowSettings();

    private void Settings_Click(object sender, RoutedEventArgs e) => Host.ShowSettings();

    private void Hide_Click(object sender, RoutedEventArgs e) => Host.HidePanel();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Host.HidePanel();
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ context menu

    private void BuildMenu()
    {
        _menu = new ContextMenu();
        _menu.Closed += (_, _) => _ctxStock = null;
        Root.ContextMenu = _menu;
        Root.ContextMenuOpening += (_, _) => PopulateMenu();
    }

    private void PopulateMenu()
    {
        if (_menu == null) return;
        _menu.Items.Clear();

        var stock = _ctxStock;
        if (stock != null)
        {
            _menu.Items.Add(Item($"删除「{stock.DisplayName}」", () => _vm.RemoveStock(stock)));
            _menu.Items.Add(Item("上移", () => _vm.MoveStockBy(stock, -1)));
            _menu.Items.Add(Item("下移", () => _vm.MoveStockBy(stock, 1)));
            _menu.Items.Add(new Separator());
        }

        _menu.Items.Add(Item("添加股票…", Host.ShowSettings));
        _menu.Items.Add(Item("设置…", Host.ShowSettings));
        _menu.Items.Add(new Separator());

        var lockItem = new MenuItem
        {
            Header = "固定面板位置",
            IsCheckable = true,
            IsChecked = _config.Locked
        };
        lockItem.Click += (_, _) => ToggleLock();
        _menu.Items.Add(lockItem);

        _menu.Items.Add(new Separator());
        _menu.Items.Add(Item("隐藏面板", Host.HidePanel));
        _menu.Items.Add(Item("退出 StockPin", Host.ExitApp));
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void StockRow_RightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: StockEntry entry }) _ctxStock = entry;
    }

    private void ToggleLock()
    {
        _config.Locked = !_config.Locked;
        RefreshLockState();
        Host.ApplySettings();
    }

    // ------------------------------------------------------------------ closing

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || Host.IsExiting) return;

        e.Cancel = true;
        Hide();
        Host.Vm.SetPaused(true);
    }
}
