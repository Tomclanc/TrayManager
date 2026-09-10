using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace TrayManager;

internal sealed partial class MainWindow
{
    private Grid? page;
    private Border? ownCard;
    private Button? largeButton;
    private TextBlock? inputHint;
    private bool largeMode, dialogOpen;
    private bool xboxMode, synchronizingMode;
    private readonly DispatcherTimer gamingTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private OverlappedPresenter? windowedPresenter;
    private int hotkeyId = 200;
    private bool hotkeyRegistered;
    private readonly DispatcherTimer inputTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private ushort lastButtons;
    private long lastMove;
    private readonly ConditionalWeakTable<FrameworkElement, Sizing> normalSizes = new();
    private sealed record Sizing(double Font, double MinHeight);

    private void SetupInteraction(Grid root, StackPanel heading)
    {
        page = root;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(heading);
        root.ActualThemeChanged += (_, _) => UpdateCaptionColors();
        root.Loaded += (_, _) => UpdateCaptionColors();
        root.Loaded += (_, _) =>
        {
            root.XamlRoot.Changed += (_, _) => ApplyLargeSizing();
            ApplyLargeSizing();
        };
        root.SizeChanged += (_, _) => ApplyLargeSizing();
        UpdateCaptionColors();
        windowedPresenter = AppWindow.Presenter as OverlappedPresenter;
        var header = new Grid { ColumnSpacing = 16 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Remove(heading); header.Children.Add(heading); root.Children.Add(header);
        largeButton = RoundedButton("大屏模式");
        // Manual button hidden by request; Xbox detection and F11 remain available.
        largeButton.Visibility = Visibility.Collapsed;
        largeButton.Click += (_, _) => SetLargeMode(!largeMode);
        Grid.SetColumn(largeButton, 1); header.Children.Add(largeButton);
        inputHint = new TextBlock { Text = "", FontSize = 12, Opacity = .7, TextWrapping = TextWrapping.Wrap };
        heading.Children.Add(inputHint);
        root.KeyDown += (_, e) =>
        {
            if (dialogOpen) return;
            if (e.Key == Windows.System.VirtualKey.F11) { SetLargeMode(!largeMode); e.Handled = true; }
            else if (e.Key == Windows.System.VirtualKey.Escape && largeMode) { SetLargeMode(false); e.Handled = true; }
        };
        hotkeyRegistered = Hotkey.Valid(preferences.HotkeyModifiers, preferences.HotkeyKey) &&
            Native.RegisterHotKey(hwnd, hotkeyId, preferences.HotkeyModifiers | 0x4000, preferences.HotkeyKey);
        if (preferences.HotkeyKey != 0 && !hotkeyRegistered)
            inputHint.Text = "打开快捷键被占用，请在快捷键设置中重新绑定。";
        else UpdateInputHint();
        inputTimer.Tick += (_, _) => PollGamepad();
        inputTimer.Start();
        // Follow actual presenter changes, including transitions initiated outside our buttons.
        AppWindow.Changed += (_, args) =>
        {
            if (args.DidPresenterChange || args.DidSizeChange) SynchronizeWindowMode();
        };
        Activated += (_, _) => { SynchronizeWindowMode(); UpdateCaptionColors(); };
        gamingTimer.Tick += (_, _) => SynchronizeWindowMode();
        gamingTimer.Start();
        SynchronizeWindowMode();
    }

    private void UpdateCaptionColors()
    {
        var bar = AppWindow.TitleBar;
        var dark = page?.ActualTheme == ElementTheme.Dark;
        bar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        bar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        bar.ButtonForegroundColor = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        bar.ButtonInactiveForegroundColor = dark ? Windows.UI.Color.FromArgb(255, 160, 160, 160) : Windows.UI.Color.FromArgb(255, 100, 100, 100);
        bar.ButtonHoverBackgroundColor = dark ? Windows.UI.Color.FromArgb(35, 255, 255, 255) : Windows.UI.Color.FromArgb(20, 0, 0, 0);
        bar.ButtonPressedBackgroundColor = dark ? Windows.UI.Color.FromArgb(55, 255, 255, 255) : Windows.UI.Color.FromArgb(35, 0, 0, 0);
        bar.ButtonHoverForegroundColor = bar.ButtonForegroundColor;
        bar.ButtonPressedForegroundColor = bar.ButtonForegroundColor;
    }

    private void UpdateInputHint()
    {
        if (inputHint == null) return;
        inputHint.Text = xboxMode ? "已检测到 Xbox 大屏体验 · 方向键 / 左摇杆导航 · A 确认 · 退出程序请用下方按钮" : largeMode ? "方向键 / 左摇杆导航 · A 确认 · B 返回窗口 · F11 / Esc 返回窗口" :
            "打开快捷键：" + Hotkey.Label(preferences.HotkeyModifiers, preferences.HotkeyKey) + " · F11 切换大屏";
    }

    private void SetLargeMode(bool value)
    {
        if (page == null || xboxMode || largeMode == value) return;
        try
        {
            // Hide the entire non-client title bar, not just the glyphs or hit-test targets.
            if (value)
            {
                windowedPresenter?.SetBorderAndTitleBar(false, false);
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
            else
            {
                if (windowedPresenter != null) AppWindow.SetPresenter(windowedPresenter);
                else AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                (AppWindow.Presenter as OverlappedPresenter)?.SetBorderAndTitleBar(true, true);
            }
        }
        catch (Exception e)
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(true, true);
            status.Text = "无法切换大屏：" + e.Message; return;
        }
        SynchronizeWindowMode();
        search.Focus(FocusState.Keyboard);
    }

    private void SynchronizeWindowMode()
    {
        if (page == null || largeButton == null || synchronizingMode || exiting) return;
        synchronizingMode = true;
        try
        {
        bool wasXbox = xboxMode;
        xboxMode = GamingExperience.IsActive();
        bool value = xboxMode || AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        // Xbox may fullscreen an overlapped window without changing its presenter kind.
        // Remove both the WinUI caption and native title bar in that environment.
        if (ExtendsContentIntoTitleBar == xboxMode) ExtendsContentIntoTitleBar = !xboxMode;
        if (AppWindow.Presenter is OverlappedPresenter presenter && presenter.HasTitleBar == value)
            presenter.SetBorderAndTitleBar(!value, !value);
        largeButton.IsEnabled = !xboxMode;
        largeButton.Content = xboxMode ? "Xbox 大屏体验" : value ? "返回窗口" : "大屏模式";
        if (value == largeMode && wasXbox == xboxMode) return;
        largeMode = value;
        ownCard!.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        page.Padding = new Thickness(value ? 36 : 28, 22, value ? 36 : 28, 22);
        rows.Spacing = value ? 12 : 8;
        UpdateInputHint(); ApplyLargeSizing();
        }
        finally { synchronizingMode = false; }
    }

    private void ApplyLargeSizing()
    {
        if (page == null) return;
        var metrics = LayoutMetrics.For(largeMode, page.ActualWidth, page.ActualHeight);
        page.Padding = new Thickness(metrics.Compact ? 16 : 28, metrics.Compact ? 12 : 22,
            metrics.Compact ? 16 : 28, metrics.Compact ? 12 : 22);
        page.RowSpacing = metrics.Compact ? 10 : 16;
        rows.Spacing = 4;
        if (footerNote != null) footerNote.Visibility = metrics.Compact ? Visibility.Collapsed : Visibility.Visible;
        if (subtitle != null) subtitle.Visibility = metrics.Compact ? Visibility.Collapsed : Visibility.Visible;
        void Visit(FrameworkElement element)
        {
            // Keep the requested 68-DIP app rows consistent in desktop and Xbox modes.
            if (element is Border && element.Tag as string == "CompactAppRow") return;
            var original = normalSizes.GetValue(element, e => new Sizing(e is Control c ? c.FontSize : e is TextBlock t ? t.FontSize : 0, e.MinHeight));
            double factor = metrics.FontScale;
            // Do not scale ScrollViewer/ContentControl: their inherited font propagates
            // to new rows, which would then be scaled for a second time after refresh.
            if (element is Button or ToggleSwitch or TextBox)
                ((Control)element).FontSize = original.Font * factor;
            else if (element is TextBlock text) text.FontSize = original.Font * factor;
            if (element is Button or ToggleSwitch or TextBox) element.MinHeight = Math.Max(metrics.TargetHeight, original.MinHeight);
            // Only walk the app-owned tree; do not rescale generated control templates twice.
            if (element is Panel panel) foreach (var child in panel.Children.OfType<FrameworkElement>()) Visit(child);
            else if (element is Border border && border.Child is FrameworkElement child) Visit(child);
            else if (element is ScrollViewer viewer && viewer.Content is FrameworkElement content) Visit(content);
        }
        Visit(page);
    }

    private async Task ConfigureHotkey()
    {
        if (dialogOpen || page?.XamlRoot == null) return;
        dialogOpen = true;
        var ctrl = new CheckBox { Content = "Ctrl", IsChecked = preferences.HotkeyKey == 0 || (preferences.HotkeyModifiers & 2) != 0 };
        var alt = new CheckBox { Content = "Alt", IsChecked = preferences.HotkeyKey == 0 || (preferences.HotkeyModifiers & 1) != 0 };
        var shift = new CheckBox { Content = "Shift", IsChecked = (preferences.HotkeyModifiers & 4) != 0 };
        var keys = Enumerable.Range(0x41, 26).Concat(Enumerable.Range(0x30, 10)).Concat(Enumerable.Range(0x70, 11)).ToArray();
        var key = new ComboBox { Header = "按键", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = keys.Select(k => k >= 0x70 ? "F" + (k - 0x70 + 1) : ((char)k).ToString()).ToArray(),
            SelectedIndex = Array.IndexOf(keys, preferences.HotkeyKey == 0 ? 0x54 : (int)preferences.HotkeyKey) };
        var large = new CheckBox { Content = "按快捷键时以大屏模式打开", IsChecked = preferences.HotkeyOpensLarge };
        var modifiers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modifiers.Children.Add(ctrl); modifiers.Children.Add(alt); modifiers.Children.Add(shift);
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "程序在后台运行时生效，即使隐藏自身托盘图标也可打开。退出程序后快捷键失效。" };
        var content = new StackPanel { Spacing = 12, MinWidth = 320 };
        content.Children.Add(modifiers); content.Children.Add(key); content.Children.Add(large); content.Children.Add(message);
        var dialog = new ContentDialog { XamlRoot = page.XamlRoot, Title = "打开窗口的全局快捷键", Content = content,
            PrimaryButtonText = "保存", SecondaryButtonText = "清除绑定", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Primary };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            uint mods = (ctrl.IsChecked == true ? 2u : 0) | (alt.IsChecked == true ? 1u : 0) | (shift.IsChecked == true ? 4u : 0);
            uint vk = key.SelectedIndex < 0 ? 0 : (uint)keys[key.SelectedIndex];
            if (!Hotkey.Valid(mods, vk)) { message.Text = "请至少选择一个修饰键和一个按键。"; args.Cancel = true; return; }
            if (!ChangeHotkey(mods, vk, large.IsChecked == true, out var error)) { message.Text = error; args.Cancel = true; }
        };
        dialog.SecondaryButtonClick += (_, args) =>
        {
            if (!ChangeHotkey(0, 0, false, out var error)) { message.Text = error; args.Cancel = true; }
        };
        try { await dialog.ShowAsync(); }
        finally { dialogOpen = false; }
    }

    private bool ChangeHotkey(uint mods, uint key, bool large, out string error)
    {
        error = "";
        bool same = mods == preferences.HotkeyModifiers && key == preferences.HotkeyKey && (key == 0 || hotkeyRegistered);
        int candidate = hotkeyId == 200 ? 201 : 200;
        if (!same && key != 0 && !Native.RegisterHotKey(hwnd, candidate, mods | 0x4000, key))
        { error = "组合键已被占用或系统不允许注册，请换一个。原绑定未改变。"; return false; }
        var previous = (preferences.HotkeyModifiers, preferences.HotkeyKey, preferences.HotkeyOpensLarge);
        preferences.HotkeyModifiers = mods; preferences.HotkeyKey = key; preferences.HotkeyOpensLarge = large;
        if (!Save())
        {
            (preferences.HotkeyModifiers, preferences.HotkeyKey, preferences.HotkeyOpensLarge) = previous;
            if (!same && key != 0) Native.UnregisterHotKey(hwnd, candidate);
            error = "保存失败，原绑定未改变。"; return false;
        }
        if (!same) { Native.UnregisterHotKey(hwnd, hotkeyId); hotkeyId = candidate; }
        hotkeyRegistered = key != 0;
        UpdateInputHint(); return true;
    }

    private void PollGamepad()
    {
        if (!largeMode || dialogOpen || Native.GetForegroundWindow() != hwnd) { lastButtons = 0; return; }
        Native.GamepadState pad = default; bool connected = false;
        for (uint index = 0; index < 4; index++) if (Native.XInputGetState(index, out pad) == 0) { connected = true; break; }
        if (!connected) { lastButtons = 0; return; }
        ushort buttons = pad.Buttons;
        if (pad.LeftY > 18000) buttons |= 1; if (pad.LeftY < -18000) buttons |= 2;
        if (pad.LeftX < -18000) buttons |= 4; if (pad.LeftX > 18000) buttons |= 8;
        ushort pressed = (ushort)(buttons & ~lastButtons);
        long now = Environment.TickCount64;
        if ((buttons & 15) != 0 && ((pressed & 15) != 0 || now - lastMove > 300))
        {
            var direction = (buttons & 5) != 0 ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next;
            FocusManager.TryMoveFocus(direction, new FindNextElementOptions { SearchRoot = page }); lastMove = now;
        }
        if ((pressed & 0x1000) != 0)
        {
            var focused = FocusManager.GetFocusedElement(page!.XamlRoot);
            if (focused is ToggleSwitch toggle) toggle.IsOn = !toggle.IsOn;
            else if (focused is Button button && button.IsEnabled)
                (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
        }
        if ((pressed & 0x2000) != 0) SetLargeMode(false);
        lastButtons = buttons;
    }
}
