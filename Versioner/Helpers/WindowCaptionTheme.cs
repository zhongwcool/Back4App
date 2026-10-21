using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MaterialDesignThemes.Wpf;

namespace Versioner.Helpers;

/// <summary>
/// WPF 标题栏默认保持浅色。通过 DWM 让系统按当前应用主题绘制标题栏。
/// </summary>
public static class WindowCaptionTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => Apply(window);
        window.Loaded += (_, _) => Apply(window);
    }

    public static void ApplyToOpenWindows()
    {
        if (Application.Current is null)
            return;

        foreach (Window window in Application.Current.Windows)
            Apply(window);
    }

    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var enabled = Theme.GetSystemTheme() == BaseTheme.Dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
