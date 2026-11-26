// Hey! Sonunda beni bulabildin, seni tebrik ediyorum. <3
// made by c4brky
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ShiftHazirlayici.Helpers;

public static class WindowsThemeHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyModernWindowChromeIfSupported(Window window, bool isDarkTheme = true)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (Environment.OSVersion.Version.Build >= 19041)
            {
                int darkMode = isDarkTheme ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }

            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
        }
        catch
        {
            // Safe no-op on older Windows builds
        }
    }
}
