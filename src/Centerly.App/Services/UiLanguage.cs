using System.Globalization;
using Centerly.Core.Settings;

namespace Centerly.App.Services;

internal static class UiLanguage
{
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

    /// <summary>Sets the UI culture for resource lookups. Call before any window is created; changes need a restart.</summary>
    public static void Apply(AppLanguage language)
    {
        var culture = language switch
        {
            AppLanguage.Russian => CultureInfo.GetCultureInfo("ru-RU"),
            AppLanguage.English => CultureInfo.GetCultureInfo("en-US"),
            _ => SystemUiCulture.TwoLetterISOLanguageName == "ru" ? CultureInfo.GetCultureInfo("ru-RU") : CultureInfo.GetCultureInfo("en-US"),
        };

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
