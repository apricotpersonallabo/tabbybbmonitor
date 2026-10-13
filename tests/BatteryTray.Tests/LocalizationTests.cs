namespace BatteryTray.Tests;

[Collection("UI language")]
public sealed class LocalizationTests
{
    [Fact]
    public void EverySupportedLanguageHasOnboardingText()
    {
        foreach (var choice in I18n.GetLanguageChoices().Where(choice => choice.Code != "system"))
        {
            I18n.SetLanguage(choice.Code);
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.OnboardingTitle)));
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.StartAtSignIn)));
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.SettingsLoadFailed)));
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.ColumnBluetoothAddress)));
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.HelpTab)));
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.AppVersion)));
            Assert.False(string.IsNullOrWhiteSpace(I18n.Get(TextId.SupportSite)));
        }

        I18n.SetLanguage("system");
    }
}
