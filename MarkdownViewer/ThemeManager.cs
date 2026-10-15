using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MarkdownViewer;

public enum ThemeSetting {
  System,
  Light,
  Dark
}

/// <summary>
/// アプリ全体のライト/ダーク切り替えを担当する。
/// - Application.ThemeMode (Fluent テーマ) でコントロールの見た目を切り替える
/// - Themes/*.xaml の独自ブラシ (AppXxxBrush) を差し替える
/// - タイトルバーをダークモード化する (DWM)
/// 実際の配色の適用結果は IsDark / ThemeChanged で各所へ通知する。
/// </summary>
public static class ThemeManager {
  private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
  private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

  private static readonly Uri LightColorsUri = new("/MarkdownViewer;component/Themes/LightColors.xaml", UriKind.Relative);
  private static readonly Uri DarkColorsUri = new("/MarkdownViewer;component/Themes/DarkColors.xaml", UriKind.Relative);

  private static ResourceDictionary? activeColors;
  private static bool isInitialized;

  public static ThemeSetting Setting { get; private set; } = ThemeSetting.System;

  /// <summary>現在ダーク配色が適用されているか (System 設定の場合は OS の状態を反映)。</summary>
  public static bool IsDark { get; private set; }

  /// <summary>IsDark が変わったときに UI スレッドで発生する。</summary>
  public static event EventHandler? ThemeChanged;

  public static ThemeSetting ParseSetting(string? value) {
    return Enum.TryParse<ThemeSetting>(value, ignoreCase: true, out var result) ? result : ThemeSetting.System;
  }

  /// <summary>Application 生成後、最初のウインドウを作る前に 1 回呼ぶ。</summary>
  public static void Initialize(ThemeSetting setting) {
    if(isInitialized) {
      SetSetting(setting);
      return;
    }

    isInitialized = true;
    Setting = setting;

    EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

    Apply(force: true);
  }

  public static void Shutdown() {
    if(!isInitialized) {
      return;
    }

    SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    isInitialized = false;
  }

  public static void SetSetting(ThemeSetting setting) {
    if(!isInitialized) {
      Initialize(setting);
      return;
    }

    Setting = setting;
    Apply(force: false);
  }

  private static void Apply(bool force) {
    var application = Application.Current;
    if(application is null) {
      return;
    }

    var useDark = Setting switch {
      ThemeSetting.Dark => true,
      ThemeSetting.Light => false,
      _ => IsSystemAppModeDark()
    };

    if(!force && useDark == IsDark) {
      return;
    }

    IsDark = useDark;

    // 独自ブラシ (DynamicResource で参照している色) を差し替える。
    var mergedDictionaries = application.Resources.MergedDictionaries;
    if(activeColors is not null) {
      mergedDictionaries.Remove(activeColors);
    }

    activeColors = new ResourceDictionary { Source = useDark ? DarkColorsUri : LightColorsUri };
    mergedDictionaries.Add(activeColors);

    // 標準コントロール (Button / ListBox / ComboBox / ContextMenu など) は Fluent テーマに任せる。
    application.ThemeMode = useDark ? ThemeMode.Dark : ThemeMode.Light;

    foreach(Window window in application.Windows) {
      ApplyTitleBarTheme(window);
    }

    ThemeChanged?.Invoke(null, EventArgs.Empty);
  }

  private static bool IsSystemAppModeDark() {
    try {
      using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
      // AppsUseLightTheme: 0 = ダーク / 1 = ライト。値が無い場合はライト扱い。
      return key?.GetValue(AppsUseLightThemeValueName) is int value && value == 0;
    } catch {
      return false;
    }
  }

  private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) {
    if(Setting != ThemeSetting.System) {
      return;
    }

    // SystemEvents は UI スレッド以外から発生するため Dispatcher に戻す。
    Application.Current?.Dispatcher.BeginInvoke(
      () => Apply(force: false),
      System.Windows.Threading.DispatcherPriority.Background);
  }

  private static void OnWindowLoaded(object sender, RoutedEventArgs e) {
    if(sender is Window window) {
      ApplyTitleBarTheme(window);
    }
  }

  private static void ApplyTitleBarTheme(Window window) {
    var handle = new WindowInteropHelper(window).Handle;
    if(handle == IntPtr.Zero) {
      return;
    }

    var enabled = IsDark ? 1 : 0;
    // Windows 10 1809〜1903 は 19、それ以降は 20 が「ダークタイトルバー」の属性番号。
    if(DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0) {
      _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
    }
  }

  private const int DwmUseImmersiveDarkMode = 20;
  private const int DwmUseImmersiveDarkModeLegacy = 19;

  [DllImport("dwmapi.dll")]
  private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
