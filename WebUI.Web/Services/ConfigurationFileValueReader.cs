namespace WebUI.Web.Services;

internal static class ConfigurationFileValueReader
{
    public static string ReadRequiredValueOrFile(
        IConfiguration section,
        string valueKey,
        string filePathKey,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(section);

        var directValue = section[valueKey]?.Trim();
        var configuredFilePath = section[filePathKey]?.Trim();

        var hasDirectValue =
            !string.IsNullOrWhiteSpace(directValue);
        var hasFilePath =
            !string.IsNullOrWhiteSpace(configuredFilePath);

        if (hasDirectValue && hasFilePath)
        {
            throw new InvalidOperationException(
                $"Für '{displayName}' dürfen nicht gleichzeitig ein direkter Wert und ein Dateipfad konfiguriert sein.");
        }

        if (hasDirectValue)
        {
            return directValue!;
        }

        if (!hasFilePath)
        {
            throw new InvalidOperationException(
                $"Die Einstellung '{displayName}' fehlt.");
        }

        var requiredFilePath = configuredFilePath!;

        if (!Path.IsPathFullyQualified(requiredFilePath))
        {
            throw new InvalidOperationException(
                $"Der Dateipfad für '{displayName}' muss absolut sein.");
        }

        var fullFilePath =
            Path.GetFullPath(requiredFilePath);

        if (!File.Exists(fullFilePath))
        {
            throw new InvalidOperationException(
                $"Die konfigurierte Datei für '{displayName}' wurde nicht gefunden.");
        }

        string value;

        try
        {
            value = File.ReadAllText(fullFilePath)
                .TrimEnd('\r', '\n')
                .Trim();
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Die konfigurierte Datei für '{displayName}' konnte nicht gelesen werden.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('\r') ||
            value.Contains('\n'))
        {
            throw new InvalidOperationException(
                $"Die konfigurierte Datei für '{displayName}' muss genau einen nicht leeren Wert enthalten.");
        }

        return value;
    }
}
