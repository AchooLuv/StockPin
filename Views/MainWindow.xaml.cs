namespace StockPin.Views;

using System.Windows.Interop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AppConfig _config;
    private ContextMenu? _menu;
    private StockEntry? _ctxStock;
    private bool _allowClose;

    public MainWindow(MainViewModel vm, AppConfig config)
    {
        InitializeComponent();
        _vm = vm;
        _config = config;
        DataContext = vm;

        Topmost = config.TopMost;

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
    }

    private void ApplyWorkAreaLimits()
    {
        var area = WorkArea();
        MaxHeight = Math.Max(240, area.Height - 48);
        StockScroll.MaxHeight = Math.Max(150, area.Height * 0.56);
    }

    public void RefreshLockState()
    {
        var accent = TryFindResource("Accent") as Brush ?? Brushes.White;
        var onAccent = TryFindResource("OnAccent") as Brush ?? Brushes.Black;
        var muted = TryFindResource("TextMuted") as Brush ?? Brushes.Gray;

        // Pinned: upright pin on a solid accent chip. Unpinned: lying pin, muted, no chip.
        LockGlyph.Text = _config.Locked ? "\uE840" : "\uE718";
        LockButton.ToolTip = _config.Locked ? "取消固定" : "固定面板位置";
        LockGlyph.Foreground = _config.Locked ? onAccent : muted;
        LockButton.Background = _config.Locked ? accent : Brushes.Transparent;
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

        var topItem = new MenuItem
        {
            Header = "窗口置顶",
            IsCheckable = true,
            IsChecked = _config.TopMost
        };
        topItem.Click += (_, _) =>
        {
            _config.TopMost = !_config.TopMost;
            Topmost = _config.TopMost;
            Host.ApplySettings();
        };
        _menu.Items.Add(topItem);

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
