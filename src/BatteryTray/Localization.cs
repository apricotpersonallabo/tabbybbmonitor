using System.Globalization;

namespace BatteryTray;

internal enum TextId
{
    AppName,
    BatteryInfoUnavailableTitle,
    BluetoothEnumerationFailed,
    DirectionDecreased,
    DirectionRecovered,
    TransitionTitle,
    TransitionMessage,
    FetchError,
    NoBluetoothDevices,
    BatteryUnknown,
    RefreshNow,
    SettingsMenu,
    Exit,
    BandCritical,
    BandLow,
    BandCaution,
    BandGood,
    BandUnknown,
    NotifyOnThresholdCrossing,
    ColumnMonitor,
    ColumnDevice,
    ColumnBattery,
    ColumnState,
    HeaderDescription,
    Save,
    Cancel,
    DevicesTab,
    SearchDevices,
    ShowUnavailable,
    Rediscover,
    SelectDevice,
    MonitorThisDevice,
    ResetDefaults,
    ApplyThresholdsToAll,
    DeleteDeviceSettings,
    CriticalAtOrBelow,
    LowAtOrBelow,
    CautionAtOrBelow,
    NotificationThresholds,
    ThresholdOrderHelp,
    GeneralTab,
    MonitoringSection,
    PollInterval,
    SecondsRange,
    Notifications,
    NewDeviceDefaults,
    PercentAtOrBelow,
    DefaultsNote,
    Unavailable,
    DeviceCount,
    CurrentlyUnavailable,
    BatteryStatus,
    BatteryLevelUnavailable,
    Discovering,
    DiscoveryFailed,
    DeleteDeviceConfirmation,
    DeleteUnavailableTitle,
    AppliedThresholds,
    ApplyAllTitle,
    InvalidDefaultThresholds,
    InvalidDeviceThresholds,
    InvalidThresholds,
    ThresholdOrderInstruction,
    SettingsError,
    OnboardingTitle,
    OnboardingDescription,
    OnboardingNoDevicesHelp,
    OpenBluetoothSettings,
    StartMonitoring,
    StartAtSignIn,
    StartupDisabledByUser,
    StartupControlledByPolicy,
    StartupUnavailable,
    SettingsRecovered,
    SettingsLoadFailed
}

internal static partial class I18n
{
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;
    private static string? _languageOverride;
    private static readonly IReadOnlyDictionary<string, string[]> Translations;
    private static readonly IReadOnlyDictionary<string, (string Language, string SystemDefault)> LanguageSelectorText;

    private static readonly LanguageChoice[] LanguageChoices =
    [
        new("ja", "日本語"),
        new("en", "English"),
        new("zh-Hans", "简体中文"),
        new("zh-Hant", "繁體中文"),
        new("ko", "한국어"),
        new("it", "Italiano"),
        new("es", "Español"),
        new("hi", "हिन्दी"),
        new("fr", "Français"),
        new("ru", "Русский"),
        new("pt", "Português"),
        new("ar", "العربية"),
        new("tr", "Türkçe"),
        new("de", "Deutsch"),
        new("id", "Bahasa Indonesia"),
        new("vi", "Tiếng Việt"),
        new("pl", "Polski"),
        new("th", "ไทย")
    ];

    static I18n()
    {
        Translations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = English,
            ["ja"] = Japanese,
            ["zh-Hans"] = SimplifiedChinese,
            ["zh-Hant"] = TraditionalChinese,
            ["ko"] = Korean,
            ["it"] = Italian,
            ["es"] = Spanish,
            ["hi"] = Hindi,
            ["fr"] = French,
            ["ru"] = Russian,
            ["pt"] = Portuguese,
            ["ar"] = Arabic,
            ["tr"] = Turkish,
            ["de"] = German,
            ["id"] = Indonesian,
            ["vi"] = Vietnamese,
            ["pl"] = Polish,
            ["th"] = Thai
        };

        LanguageSelectorText = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = ("Language", "Use Windows display language"),
            ["ja"] = ("言語", "Windows の表示言語を使用"),
            ["zh-Hans"] = ("语言", "使用 Windows 显示语言"),
            ["zh-Hant"] = ("語言", "使用 Windows 顯示語言"),
            ["ko"] = ("언어", "Windows 표시 언어 사용"),
            ["it"] = ("Lingua", "Usa la lingua di visualizzazione di Windows"),
            ["es"] = ("Idioma", "Usar el idioma de visualización de Windows"),
            ["hi"] = ("भाषा", "Windows प्रदर्शन भाषा का उपयोग करें"),
            ["fr"] = ("Langue", "Utiliser la langue Windows"),
            ["ru"] = ("Язык", "Использовать язык интерфейса Windows"),
            ["pt"] = ("Idioma", "Usar o idioma de apresentação do Windows"),
            ["ar"] = ("اللغة", "استخدام لغة عرض Windows"),
            ["tr"] = ("Dil", "Windows görüntüleme dilini kullan"),
            ["de"] = ("Sprache", "Windows-Anzeigesprache verwenden"),
            ["id"] = ("Bahasa", "Gunakan bahasa tampilan Windows"),
            ["vi"] = ("Ngôn ngữ", "Dùng ngôn ngữ hiển thị của Windows"),
            ["pl"] = ("Język", "Użyj języka wyświetlania systemu Windows"),
            ["th"] = ("ภาษา", "ใช้ภาษาที่ใช้แสดงของ Windows")
        };

        var expected = (int)TextId.SettingsError + 1;
        foreach (var (language, values) in Translations)
        {
            if (values.Length != expected)
                throw new InvalidOperationException($"Translation table {language} has {values.Length} entries; expected {expected}.");
        }

        var onboardingExpected = Enum.GetValues<TextId>().Length - expected;
        foreach (var (language, values) in OnboardingTranslations)
        {
            if (values.Length != onboardingExpected)
                throw new InvalidOperationException(
                    $"Onboarding translation table {language} has {values.Length} entries; expected {onboardingExpected}.");
        }
    }

    public static bool IsRightToLeft => CurrentLanguage == "ar";

    public static string LanguageLabel => LanguageSelectorText[CurrentLanguage].Language;

    public static string CurrentLanguageSetting => _languageOverride ?? "system";

    public static string Get(TextId id)
    {
        var index = (int)id;
        var legacyCount = (int)TextId.SettingsError + 1;
        return index < legacyCount
            ? Translations[CurrentLanguage][index]
            : OnboardingTranslations[CurrentLanguage][index - legacyCount];
    }

    public static string Format(TextId id, params object?[] arguments) =>
        string.Format(SelectedCulture, Get(id), arguments);

    public static IReadOnlyList<LanguageChoice> GetLanguageChoices() =>
        [new LanguageChoice("system", LanguageSelectorText[CurrentLanguage].SystemDefault), .. LanguageChoices];

    public static void SetLanguage(string? language)
    {
        _languageOverride = !string.IsNullOrWhiteSpace(language) &&
                            !language.Equals("system", StringComparison.OrdinalIgnoreCase) &&
                            Translations.ContainsKey(language)
            ? language
            : null;
    }

    private static CultureInfo SelectedCulture => CurrentLanguage switch
    {
        "ja" => CultureInfo.GetCultureInfo("ja-JP"),
        "zh-Hans" => CultureInfo.GetCultureInfo("zh-CN"),
        "zh-Hant" => CultureInfo.GetCultureInfo("zh-TW"),
        "ko" => CultureInfo.GetCultureInfo("ko-KR"),
        "it" => CultureInfo.GetCultureInfo("it-IT"),
        "es" => CultureInfo.GetCultureInfo("es-ES"),
        "hi" => CultureInfo.GetCultureInfo("hi-IN"),
        "fr" => CultureInfo.GetCultureInfo("fr-FR"),
        "ru" => CultureInfo.GetCultureInfo("ru-RU"),
        "pt" => CultureInfo.GetCultureInfo("pt-BR"),
        "ar" => CultureInfo.GetCultureInfo("ar-SA"),
        "tr" => CultureInfo.GetCultureInfo("tr-TR"),
        "de" => CultureInfo.GetCultureInfo("de-DE"),
        "id" => CultureInfo.GetCultureInfo("id-ID"),
        "vi" => CultureInfo.GetCultureInfo("vi-VN"),
        "pl" => CultureInfo.GetCultureInfo("pl-PL"),
        "th" => CultureInfo.GetCultureInfo("th-TH"),
        "en" => CultureInfo.GetCultureInfo("en-US"),
        _ => SystemUiCulture
    };

    private static string CurrentLanguage
    {
        get
        {
            if (_languageOverride is not null)
                return _languageOverride;

            var culture = SystemUiCulture;
            if (culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                var name = culture.Name;
                return name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase)
                    ? "zh-Hant"
                    : "zh-Hans";
            }

            var language = culture.TwoLetterISOLanguageName;
            return Translations.ContainsKey(language) ? language : "en";
        }
    }
}

internal sealed record LanguageChoice(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
