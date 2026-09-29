using System.Text.Json;

namespace BatteryTray;

internal enum SettingsLoadStatus
{
    Missing,
    Loaded,
    RecoveredInvalidJson,
    IoError
}

internal sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    string? Detail = null);

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatteryTray",
            "settings.json");
    }

    public SettingsLoadResult Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new SettingsLoadResult(
                    new AppSettings { OnboardingCompleted = false },
                    SettingsLoadStatus.Missing);
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
            return settings is null
                ? RecoverInvalidJson()
                : new SettingsLoadResult(settings, SettingsLoadStatus.Loaded);
        }
        catch (JsonException)
        {
            return RecoverInvalidJson();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.IoError, exception.Message);
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private SettingsLoadResult RecoverInvalidJson()
    {
        try
        {
            var backupPath = _path + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            File.Move(_path, backupPath, overwrite: true);
            return new SettingsLoadResult(
                new AppSettings(),
                SettingsLoadStatus.RecoveredInvalidJson,
                backupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(new AppSettings(), SettingsLoadStatus.IoError, exception.Message);
        }
    }
}
