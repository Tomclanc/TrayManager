using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using Windows.Storage.Streams;

namespace TrayManager;

internal sealed class MainWindow : Window
{
    private readonly Preferences preferences;
    private readonly StackPanel rows = new() { Spacing = 8 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox search = new() { PlaceholderText = "搜索应用或图标", MinWidth = 180 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, TrayEntry> changed = [];
    private List<TrayEntry> entries = [];
    private readonly Native.SubclassProc callback;
    private readonly nint hwnd;
    private readonly uint taskbarCreated;
    private readonly nint trayIcon;
    private RegisteredWaitHandle? signalWait;
    private bool refreshing, exiting;
    private const uint TrayMessage = 0x8001;

    internal MainWindow()
    {
        Title = "托盘管理";
        SystemBackdrop = new MicaBackdrop();
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = Native.GetDpiForWindow(hwnd) / 96.0;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min((int)(880 * scale), area.Width - 60), Math.Min((int)(720 * scale), area.Height - 60)));
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "TrayManager.ico");
        trayIcon = System.IO.File.Exists(iconPath) ? Native.LoadImage(0, iconPath, 1, 32, 32, 0x10) : 0;
        if (System.IO.File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        callback = OnMessage;
        Native.SetWindowSubclass(hwnd, callback, 1, 0);
        taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        try { preferences = Preferences.Load(); }
        catch { preferences = new(); status.Text = "设置文件无法读取，本次使用默认设置；原文件未改动。"; }
        foreach (var key in preferences.Hidden)
            if (preferences.Identities.TryGetValue(key, out var identity) && identity.Resolve(key) is TrayEntry entry) changed[key] = entry;

        var root = new Grid { Padding = new Thickness(28, 22, 28, 22), RowSpacing = 18 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Spacing = 6 };
        heading.Children.Add(new TextBlock { Text = "托盘管理", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "让托盘简洁，应用继续在后台运行。", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        root.Children.Add(heading);

        var settings = new StackPanel { Spacing = 12 };
        var own = new ToggleSwitch { IsOn = preferences.HideOwnIcon, OnContent = "隐藏", OffContent = "显示", VerticalAlignment = VerticalAlignment.Center };
        own.Toggled += (_, _) =>
        {
            var previous = preferences.HideOwnIcon;
            preferences.HideOwnIcon = own.IsOn;
            if (!Save()) { preferences.HideOwnIcon = previous; return; }
            SetOwnTray();
        };
        settings.Children.Add(Card("隐藏本程序的托盘图标", "双击托盘图标打开；隐藏后，再次启动程序即可打开此窗口。", own));
        var toolbar = new Grid { ColumnSpacing = 12 };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(search);
        search.TextChanged += (_, _) => Render();
        var refresh = new Button { Content = "刷新列表" };
        refresh.Click += async (_, _) => await Refresh(true);
        Grid.SetColumn(refresh, 1); toolbar.Children.Add(refresh);
        settings.Children.Add(toolbar);
        Grid.SetRow(settings, 1); root.Children.Add(settings);

        var scroll = new ScrollViewer { Content = rows, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled };
        Grid.SetRow(scroll, 2); root.Children.Add(scroll);

        var footer = new StackPanel { Spacing = 10 };
        footer.Children.Add(status);
        footer.Children.Add(new TextBlock { Text = "开关表示本工具的隐藏规则，并非系统原有状态。仅列出可安全定位的图标；系统图标或受保护应用可能不支持。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.65 });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var restore = new Button { Content = "恢复本工具隐藏的图标" };
        restore.Click += async (_, _) => { Restore(); await Refresh(true); };
        var quit = new Button { Content = "退出程序" };
        quit.Click += (_, _) => Quit();
        actions.Children.Add(restore); actions.Children.Add(quit); footer.Children.Add(actions);
        Grid.SetRow(footer, 3); root.Children.Add(footer);
        Content = root;
        AppWindow.Closing += (_, e) => { if (!exiting) { e.Cancel = true; AppWindow.Hide(); } };
        SetOwnTray();
        signalWait = ThreadPool.RegisterWaitForSingleObject(Program.ShowSignal!, (_, _) => DispatcherQueue.TryEnqueue(ShowSettings), null, -1, false);
        timer.Tick += async (_, _) => await Refresh(false);
        timer.Start();
        _ = Refresh(true);
    }

    private static Border Card(string title, string detail, FrameworkElement control, byte[]? image = null)
    {
        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new FontIcon { Glyph = "\uE950", FontSize = 24, Width = 32, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(icon);
        if (image is { Length: > 0 }) _ = LoadImage(grid, icon, image);
        var label = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        label.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.65, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
        ToolTipService.SetToolTip(label, detail);
        Grid.SetColumn(label, 1); grid.Children.Add(label);
        Grid.SetColumn(control, 2); grid.Children.Add(control);
        var border = new Border { Padding = new Thickness(18), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        void UpdateTheme()
        {
            var dark = border.ActualTheme == ElementTheme.Dark;
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(dark ? (byte)13 : (byte)179, 255, 255, 255));
            border.BorderBrush = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(26, 0, 0, 0) : Windows.UI.Color.FromArgb(15, 0, 0, 0));
        }
        border.ActualThemeChanged += (_, _) => UpdateTheme();
        border.Loaded += (_, _) => UpdateTheme();
        UpdateTheme();
        border.Child = grid;
        return border;
    }

    private static async Task LoadImage(Grid grid, FontIcon fallback, byte[] bytes)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(bytes); await writer.StoreAsync(); stream.Seek(0);
            var bitmap = new BitmapImage(); await bitmap.SetSourceAsync(stream);
            grid.Children.Remove(fallback);
            grid.Children.Add(new Image { Source = bitmap, Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center });
        }
        catch { /* Some shell snapshots are not encoded PNGs. Keep the neutral fallback. */ }
    }

    private async Task Refresh(bool redraw)
    {
        if (refreshing || exiting) return;
        refreshing = true;
        try
        {
            var desired = preferences.Hidden.ToHashSet();
            var current = await Task.Run(() => TrayCatalog.Read(desired));
            current.RemoveAll(e => e.Window == hwnd || string.Equals(e.Path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase));
            foreach (var entry in changed.Values.Where(e => Native.OwnerAlive(e) && !current.Any(c => c.Key == e.Key))) current.Add(entry);
            current = current.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            var different = !entries.Select(e => e.Key).SequenceEqual(current.Select(e => e.Key));
            entries = current;
            bool identityChanged = false;
            foreach (var entry in entries.Where(e => preferences.Hidden.Contains(e.Key)))
                if (Native.Hide(entry, true))
                {
                    changed[entry.Key] = entry;
                    var identity = SavedIdentity.From(entry);
                    if (!preferences.Identities.TryGetValue(entry.Key, out var saved) || identity != saved)
                    { preferences.Identities[entry.Key] = identity; identityChanged = true; }
                }
            if (identityChanged) Save();
            if (redraw || different) Render();
            status.Text = $"发现 {entries.Count} 个可管理图标 · {preferences.Hidden.Count} 条隐藏规则";
        }
        catch (Exception e) { status.Text = "读取失败：" + e.Message; }
        finally { refreshing = false; }
    }

    private void Render()
    {
        rows.Children.Clear();
        foreach (var entry in entries.Where(e => (e.Name + e.Path).Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)))
        {
            var toggle = new ToggleSwitch { IsOn = preferences.Hidden.Contains(entry.Key), OnContent = "隐藏", OffContent = "不隐藏", VerticalAlignment = VerticalAlignment.Center };
            bool resetting = false;
            toggle.Toggled += (_, _) =>
            {
                if (resetting) return;
                var hidden = toggle.IsOn;
                if (!Native.Hide(entry, hidden))
                {
                    resetting = true; toggle.IsOn = !hidden; resetting = false;
                    status.Text = "图标已变化或 Windows 拒绝修改，请刷新后重试。"; return;
                }
                if (hidden) { preferences.Hidden.Add(entry.Key); changed[entry.Key] = entry; preferences.Identities[entry.Key] = SavedIdentity.From(entry); }
                else { preferences.Hidden.Remove(entry.Key); changed.Remove(entry.Key); preferences.Identities.Remove(entry.Key); }
                if (!Save()) return;
                status.Text = hidden ? $"已隐藏：{entry.Name}（应用仍在运行）" : $"已恢复：{entry.Name}";
            };
            rows.Children.Add(Card(entry.Name, entry.Path, toggle, entry.Image));
        }
        foreach (var key in preferences.Hidden.Where(k => !entries.Any(e => e.Key == k)).ToArray())
        {
            var remove = new Button { Content = "移除规则", VerticalAlignment = VerticalAlignment.Center };
            remove.Click += (_, _) =>
            {
                preferences.Hidden.Remove(key); preferences.Identities.Remove(key); changed.Remove(key); Save(); Render();
                status.Text = "已移除规则；若图标此前已隐藏且无法定位，请重新启动对应应用恢复图标。";
            };
            rows.Children.Add(Card("暂未定位到图标", key + " · 保留隐藏规则，等待应用重新创建图标。", remove));
        }
        if (rows.Children.Count == 0) rows.Children.Add(new TextBlock { Text = "没有匹配的实时托盘图标。", Margin = new Thickness(16), Opacity = 0.65 });
    }

    private bool Save()
    {
        try { preferences.Save(); return true; }
        catch (Exception e) { status.Text = "设置未能保存：" + e.Message; return false; }
    }

    private void Restore()
    {
        var failed = preferences.Hidden.Where(k => !changed.ContainsKey(k)).ToHashSet();
        foreach (var entry in changed.Values)
            if (Native.OwnerAlive(entry) && !Native.Hide(entry, false)) failed.Add(entry.Key);
        preferences.Hidden = failed;
        foreach (var key in changed.Keys.Where(k => !failed.Contains(k)).ToArray()) { changed.Remove(key); preferences.Identities.Remove(key); }
        Save(); Render();
    }

    private void Quit()
    {
        // Keep desired rules for the next launch, but restore icons for the rest of this session.
        foreach (var entry in changed.Values) Native.Hide(entry, false);
        exiting = true; timer.Stop(); signalWait?.Unregister(null);
        var data = OwnData(); Native.Shell_NotifyIconW(2, ref data);
        if (trayIcon != 0) Native.DestroyIcon(trayIcon);
        Close(); Application.Current.Exit();
    }
    private Native.Data OwnData() => new() { cbSize = (uint)Marshal.SizeOf<Native.Data>(), hWnd = hwnd, uID = 1,
        uFlags = 1 | 2 | 4, uCallbackMessage = TrayMessage, hIcon = trayIcon != 0 ? trayIcon : Native.LoadIcon(0, 32512), szTip = "托盘管理 · 双击打开" };
    private void SetOwnTray()
    {
        var data = OwnData(); Native.Shell_NotifyIconW(2, ref data);
        if (!preferences.HideOwnIcon) Native.Shell_NotifyIconW(0, ref data);
    }
    private void ShowSettings() { Native.ShowWindow(hwnd, 9); Activate(); Native.SetForegroundWindow(hwnd); }
    private nint OnMessage(nint h, uint message, nuint w, nint l, nuint id, nuint data)
    {
        if (message == TrayMessage && ((long)l & 0xffff) == 0x203) ShowSettings();
        if (message == taskbarCreated) SetOwnTray();
        return Native.DefSubclassProc(h, message, w, l);
    }
}
