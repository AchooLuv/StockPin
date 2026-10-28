namespace StockPin.Views;

using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppConfig _config;
    private StockEntry? _dragEntry;
    private Point _dragStart;
    private bool _dragArmed;
    private bool _dragging;
    private ListBoxItem? _dragSourceItem;
    private DragGhostAdorner? _ghostAdorner;
    private AdornerLayer? _ghostLayer;
    private Point _grabOffset;
    private int _dragOriginalIndex = -1;
    private CancellationTokenSource? _searchCts;

    public SettingsWindow(AppConfig config, MainViewModel main)
    {
        InitializeComponent();
        _config = config;
        _vm = new SettingsViewModel(config, main);
        _vm.Changed += () => App.Current.ApplySettings();
        _vm.Results.CollectionChanged += (_, _) =>
            ResultPanel.Visibility = _vm.Results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        DataContext = _vm;

        Loaded += OnLoaded;
        Closed += OnClosed;
        Deactivated += (_, _) =>
        {
            if (_dragging) EndDrag(true);
            else if (_dragArmed) CancelArmed();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (_dragging)
            {
                CancelDrag();
                e.Handled = true;
                return;
            }
            Close();
            e.Handled = true;
        };

        foreach (var radio in IntervalPanel.Children.OfType<RadioButton>())
        {
            radio.IsChecked = string.Equals(radio.Tag as string, config.IntervalSec.ToString(), StringComparison.Ordinal);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_config.SettingsLeft is double left && _config.SettingsTop is double top)
        {
            Left = left;
            Top = top;
        }

        ClampToScreen();
        AddBox.Focus();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        EndDrag(false);
        _searchCts?.Cancel();
        App.Current.SaveSettingsPosition(Left, Top);
    }

    private void ClampToScreen()
    {
        var area = SystemParameters.WorkArea;
        if (double.IsNaN(Left) || Left < area.Left - 40 || Left > area.Right - 140 ||
            double.IsNaN(Top) || Top < area.Top - 10 || Top > area.Bottom - 90)
        {
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }
    }

    // ------------------------------------------------------------------ chrome

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>Used when the panel changes shared state (pin, theme) while this window is open.</summary>
    public void RefreshFromConfig()
    {
        _vm.RefreshFromConfig();

        foreach (var radio in IntervalPanel.Children.OfType<RadioButton>())
        {
            bool should = string.Equals(radio.Tag as string, _vm.IntervalSec.ToString(), StringComparison.Ordinal);
            if (radio.IsChecked != should) radio.IsChecked = should;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Used by the design-preview screenshot mode.</summary>
    public void SelectTab(int index)
    {
        switch (index)
        {
            case 1:
                TabLook.IsChecked = true;
                break;
            case 2:
                TabGeneral.IsChecked = true;
                break;
            default:
                TabWatch.IsChecked = true;
                break;
        }
    }

    private void Tabs_Checked(object sender, RoutedEventArgs e)
    {
        if (PaneWatch == null || PaneLook == null || PaneGeneral == null) return;
        if (sender is not RadioButton radio || radio.Tag is not string tag) return;

        PaneWatch.Visibility = tag == "0" ? Visibility.Visible : Visibility.Collapsed;
        PaneLook.Visibility = tag == "1" ? Visibility.Visible : Visibility.Collapsed;
        PaneGeneral.Visibility = tag == "2" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Interval_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is RadioButton { Tag: string tag } && int.TryParse(tag, out var seconds))
            _vm.IntervalSec = seconds;
    }

    private void ResetPosition_Click(object sender, RoutedEventArgs e) => App.Current.ResetPanelPosition();

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/AchooLuv/StockPin") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn("open github failed: " + ex.Message);
        }
    }

    // ------------------------------------------------------------------ add / remove

    private void AddBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AddPlaceholder != null)
            AddPlaceholder.Visibility = string.IsNullOrEmpty(AddBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await AddFromInputAsync();
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e) => await AddFromInputAsync();

    private async Task AddFromInputAsync()
    {
        var keyword = AddBox.Text.Trim();
        if (keyword.Length == 0)
        {
            AddBox.Focus();
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _vm.Results.Clear();
        SetStatus("正在查询…", false);

        List<StockSearchResult> results;
        try
        {
            results = await App.Current.Quotes.SearchAsync(keyword, token);
        }
        catch (Exception ex)
        {
            Log.Warn("search failed: " + ex.Message);
            SetStatus("查询失败，请稍后重试", true);
            return;
        }

        if (token.IsCancellationRequested) return;

        if (results.Count == 0)
        {
            SetStatus($"没有找到「{keyword}」，请检查代码或换个关键词", true);
            return;
        }

        if (results.Count == 1)
        {
            AddOne(results[0]);
            return;
        }

        foreach (var result in results) _vm.Results.Add(result);
        SetStatus($"找到 {results.Count} 个结果，点击选择要添加的股票", false);
    }

    private void Result_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StockSearchResult result }) AddOne(result);
    }

    private void AddOne(StockSearchResult result)
    {
        if (!App.Current.Vm.AddStock(result))
        {
            SetStatus($"「{result.Name} {result.Code}」已经在自选列表里了", true);
            return;
        }

        _vm.Results.Clear();
        AddBox.Clear();
        SetStatus($"已添加 {result.Name} {result.Code}", false);
        StockList.ScrollIntoView(App.Current.Vm.Stocks[^1]);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StockEntry entry }) App.Current.Vm.RemoveStock(entry);
    }

    private void SetStatus(string text, bool error)
    {
        AddStatus.Text = text;
        AddStatus.Visibility = Visibility.Visible;
        var brush = TryFindResource(error ? "Up" : "TextMuted") as Brush;
        if (brush != null) AddStatus.Foreground = brush;
    }

    // ------------------------------------------------------------------ drag reorder

    private void Handle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StockEntry entry }) return;

        _dragEntry = entry;
        _dragStart = e.GetPosition(RootCard);
        _dragOriginalIndex = App.Current.Vm.Stocks.IndexOf(entry);
        _dragArmed = true;
        Mouse.Capture(StockList, CaptureMode.Element);
        e.Handled = true;
    }

    private void StockList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed || _dragEntry == null) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_dragging) EndDrag(true);
            else CancelArmed();
            return;
        }

        var cardPoint = e.GetPosition(RootCard);
        if (!_dragging)
        {
            if (Math.Abs(cardPoint.X - _dragStart.X) < 4 &&
                Math.Abs(cardPoint.Y - _dragStart.Y) < 4) return;

            if (!BeginDrag())
            {
                CancelArmed();
                return;
            }
        }

        UpdateDrag(cardPoint, e.GetPosition(StockList));
        e.Handled = true;
    }

    private void StockList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragArmed && !_dragging) return;

        if (_dragging) EndDrag(true);
        else CancelArmed();

        e.Handled = true;
    }

    private bool BeginDrag()
    {
        if (_dragEntry == null) return false;
        if (StockList.ItemContainerGenerator.ContainerFromItem(_dragEntry) is not ListBoxItem item) return false;

        var layer = AdornerLayer.GetAdornerLayer(RootCard);
        if (layer == null) return false;

        ResetRowTransforms();

        var itemOrigin = item.TranslatePoint(new Point(0, 0), RootCard);
        _grabOffset = new Point(_dragStart.X - itemOrigin.X, _dragStart.Y - itemOrigin.Y);

        _ghostAdorner = new DragGhostAdorner(RootCard, CreateGhost(item));
        _ghostLayer = layer;
        layer.Add(_ghostAdorner);

        _dragSourceItem = item;
        item.Opacity = 0.18;
        _dragging = true;
        Mouse.OverrideCursor = Cursors.SizeAll;

        _ghostAdorner.MoveTo(itemOrigin);
        return true;
    }

    private UIElement CreateGhost(ListBoxItem item)
    {
        return new Border
        {
            Width = item.ActualWidth,
            Height = item.ActualHeight,
            CornerRadius = new CornerRadius(8),
            Background = TryFindResource("CardBg") as Brush ?? Brushes.Black,
            BorderBrush = TryFindResource("PanelBorder") as Brush,
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
            Child = new Image { Source = Snapshot(item), Stretch = Stretch.Fill },
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.4,
                Color = Colors.Black
            }
        };
    }

    private static ImageSource Snapshot(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX));
        int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY));

        var bitmap = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return bitmap;
    }

    private void UpdateDrag(Point cardPoint, Point listPoint)
    {
        _ghostAdorner?.MoveTo(new Point(cardPoint.X - _grabOffset.X, cardPoint.Y - _grabOffset.Y));

        var stocks = App.Current.Vm.Stocks;
        var entry = _dragEntry;
        if (entry == null) return;

        int from = stocks.IndexOf(entry);
        if (from < 0) return;

        int insertBefore = stocks.Count;
        foreach (var other in stocks)
        {
            if (other == entry) continue;
            if (StockList.ItemContainerGenerator.ContainerFromItem(other) is not ListBoxItem item) continue;

            double center = RowTop(item) + item.ActualHeight / 2;
            if (listPoint.Y < center)
            {
                insertBefore = stocks.IndexOf(other);
                break;
            }
        }

        int target = insertBefore - (from < insertBefore ? 1 : 0);
        target = Math.Clamp(target, 0, stocks.Count - 1);
        if (target == from) return;

        var positions = CaptureRowPositions();
        App.Current.Vm.MoveStock(entry, target, persist: false);
        AnimateReorder(positions, entry, 120);

        if (StockList.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem moved)
        {
            moved.Opacity = 0.18;
            if (moved.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0;
            }
        }
    }

    private void EndDrag(bool persist)
    {
        _dragArmed = false;

        if (!_dragging)
        {
            Mouse.Capture(null);
            _dragEntry = null;
            return;
        }

        _dragging = false;
        Mouse.OverrideCursor = null;

        if (_ghostLayer != null && _ghostAdorner != null)
            _ghostLayer.Remove(_ghostAdorner);
        _ghostAdorner = null;
        _ghostLayer = null;

        if (_dragSourceItem != null) _dragSourceItem.Opacity = 1;

        if (_dragEntry != null &&
            StockList.ItemContainerGenerator.ContainerFromItem(_dragEntry) is ListBoxItem item)
        {
            item.Opacity = 1;
            if (item.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0;
            }
            item.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.18, 1, TimeSpan.FromMilliseconds(120)));
        }

        Mouse.Capture(null);
        if (persist) App.Current.Vm.PersistOrder();

        _dragEntry = null;
        _dragSourceItem = null;
        _dragOriginalIndex = -1;
    }

    private void CancelArmed()
    {
        _dragArmed = false;
        _dragEntry = null;
        Mouse.Capture(null);
    }

    private void CancelDrag()
    {
        if (_dragging && _dragEntry != null && _dragOriginalIndex >= 0)
        {
            App.Current.Vm.MoveStock(_dragEntry, _dragOriginalIndex, persist: false);
            App.Current.Vm.PersistOrder();
        }

        EndDrag(false);
    }

    private void ResetRowTransforms()
    {
        foreach (var entry in App.Current.Vm.Stocks)
        {
            if (StockList.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem item &&
                item.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0;
            }
        }
    }

    private Dictionary<StockEntry, double> CaptureRowPositions()
    {
        var positions = new Dictionary<StockEntry, double>();
        foreach (var entry in App.Current.Vm.Stocks)
        {
            if (StockList.ItemContainerGenerator.ContainerFromItem(entry) is ListBoxItem item)
                positions[entry] = RowTop(item);
        }
        return positions;
    }

    private void AnimateReorder(IReadOnlyDictionary<StockEntry, double> oldPositions,
        StockEntry? exclude = null, int durationMs = 180)
    {
        if (!SystemParameters.ClientAreaAnimation) return;

        StockList.UpdateLayout();

        foreach (var entry in App.Current.Vm.Stocks)
        {
            if (entry == exclude) continue;
            if (!oldPositions.TryGetValue(entry, out double oldY)) continue;
            if (StockList.ItemContainerGenerator.ContainerFromItem(entry) is not ListBoxItem item) continue;

            double newY = RowTop(item);
            double offset = oldY - newY;
            if (Math.Abs(offset) < 0.5) continue;

            if (item.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                item.RenderTransform = transform;
            }

            transform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(durationMs))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }
    }

    private double RowTop(ListBoxItem item)
    {
        double top = item.TranslatePoint(new Point(0, 0), StockList).Y;
        if (item.RenderTransform is TranslateTransform transform) top -= transform.Y;
        return top;
    }
}
