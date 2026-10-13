namespace BatteryTray;

internal static partial class I18n
{
    private static readonly IReadOnlyDictionary<string, string[]> HelpTranslations =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = ["Help", "Version", "Support website"],
            ["ja"] = ["ヘルプ", "バージョン", "サポートサイト"],
            ["zh-Hans"] = ["帮助", "版本", "支持网站"],
            ["zh-Hant"] = ["說明", "版本", "支援網站"],
            ["ko"] = ["도움말", "버전", "지원 웹사이트"],
            ["it"] = ["Aiuto", "Versione", "Sito di supporto"],
            ["es"] = ["Ayuda", "Versión", "Sitio de soporte"],
            ["hi"] = ["सहायता", "संस्करण", "सहायता वेबसाइट"],
            ["fr"] = ["Aide", "Version", "Site d’assistance"],
            ["ru"] = ["Справка", "Версия", "Сайт поддержки"],
            ["pt"] = ["Ajuda", "Versão", "Site de suporte"],
            ["ar"] = ["المساعدة", "الإصدار", "موقع الدعم"],
            ["tr"] = ["Yardım", "Sürüm", "Destek sitesi"],
            ["de"] = ["Hilfe", "Version", "Support-Website"],
            ["id"] = ["Bantuan", "Versi", "Situs dukungan"],
            ["vi"] = ["Trợ giúp", "Phiên bản", "Trang hỗ trợ"],
            ["pl"] = ["Pomoc", "Wersja", "Witryna pomocy"],
            ["th"] = ["ช่วยเหลือ", "เวอร์ชัน", "เว็บไซต์สนับสนุน"]
        };
}
