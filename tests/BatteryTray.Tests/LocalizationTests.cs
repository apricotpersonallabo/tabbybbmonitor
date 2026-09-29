namespace BatteryTray.Tests;

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
        }

        I18n.SetLanguage("system");
    }
}
