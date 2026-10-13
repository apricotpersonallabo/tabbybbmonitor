namespace BatteryTray;

internal static partial class I18n
{
    private static readonly IReadOnlyDictionary<string, string[]> StartupTranslations =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = ["Auto-start", "Enabled"],
            ["ja"] = ["自動起動", "有効"],
            ["zh-Hans"] = ["自动启动", "启用"],
            ["zh-Hant"] = ["自動啟動", "啟用"],
            ["ko"] = ["자동 시작", "사용"],
            ["it"] = ["Avvio automatico", "Abilitato"],
            ["es"] = ["Inicio automático", "Habilitado"],
            ["hi"] = ["स्वचालित प्रारंभ", "सक्षम"],
            ["fr"] = ["Démarrage automatique", "Activé"],
            ["ru"] = ["Автозапуск", "Включено"],
            ["pt"] = ["Inicialização automática", "Ativado"],
            ["ar"] = ["التشغيل التلقائي", "مفعّل"],
            ["tr"] = ["Otomatik başlatma", "Etkin"],
            ["de"] = ["Autostart", "Aktiviert"],
            ["id"] = ["Mulai otomatis", "Aktif"],
            ["vi"] = ["Khởi động tự động", "Bật"],
            ["pl"] = ["Autostart", "Włączone"],
            ["th"] = ["เริ่มอัตโนมัติ", "เปิดใช้งาน"]
        };
}
