using Microsoft.Win32;
using System.Security;

namespace BatteryTray;

internal sealed record TooltipTheme(bool IsDark, bool HighContrast, Color Background, Color Foreground, Color Border)
{
    public static TooltipTheme ReadWindowsTheme()
    {
        if (SystemInformation.HighContrast)
            return Create(false, true);
        try
        {
            using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Create(personalize?.GetValue("AppsUseLightTheme") is int value && value == 0);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Create(false);
        }
    }

    public static TooltipTheme Create(bool dark, bool highContrast = false) => highContrast
        ? new(false, true, SystemColors.Window, SystemColors.WindowText, SystemColors.WindowText)
        : dark
            ? new(true, false, Color.FromArgb(43, 43, 43), Color.FromArgb(245, 245, 245), Color.FromArgb(85, 85, 85))
            : new(false, false, Color.FromArgb(249, 249, 249), Color.FromArgb(26, 26, 26), Color.FromArgb(210, 210, 210));
}
