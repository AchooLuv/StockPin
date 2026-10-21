namespace StockPin.Views;

public partial class SettingsWindow : Window
{
    private const string DragFormat = "StockPin.StockEntry";

    private readonly SettingsViewModel _vm;
    private readonly AppConfig _config;
    private StockEntry? _dragEntry;
    private Point _dragStart;
    private bool _dragArmed;
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
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
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
        if (sender is FrameworkElement { DataContext: StockEntry entry })
        {
            _dragEntry = entry;
            _dragStart = e.GetPosition(this);
            _dragArmed = true;
        }
    }

    private void Handle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed || _dragEntry == null) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragArmed = false;
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < 4 && Math.Abs(position.Y - _dragStart.Y) < 4) return;

        _dragArmed = false;
        var dragged = _dragEntry;
        _dragEntry = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DragFormat, dragged), DragDropEffects.Move);
    }

    private void StockList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void StockList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is not StockEntry source) return;

        var stocks = App.Current.Vm.Stocks;
        int index = stocks.Count - 1;
        var container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (container?.DataContext is StockEntry target) index = stocks.IndexOf(target);

        App.Current.Vm.MoveStock(source, index);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T match) return match;
            source = source is Visual ? VisualTreeHelper.GetParent(source) : null;
        }
        return null;
    }
}
