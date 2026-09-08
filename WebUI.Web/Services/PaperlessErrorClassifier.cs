using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace WebUI.Web.Services;

public sealed record PaperlessErrorInfo(
    string EventId,
    string Category,
    int? StatusCode,
    string UserMessage,
    string ExceptionType,
    bool IsTimeout,
    bool IsControlledCancellation);

public sealed record CentralSyncFailure(
    bool IsFullRefresh,
    PaperlessErrorInfo Error);

public static class PaperlessErrorClassifier
{
    public static PaperlessErrorInfo Classify(
        Exception exception,
        CancellationToken cancellationToken = default,
        string fallbackMessage = "Die Paperless-Daten konnten nicht geladen werden.")
    {
        ArgumentNullException.ThrowIfNull(exception);

        var eventId = CreateEventId();
        var exceptionType = exception.GetType().Name;

        if (exception is OperationCanceledException)
        {
            var controlled = cancellationToken.IsCancellationRequested;
            return new(
                eventId,
                controlled ? "Kontrollierter Abbruch" : "Zeitüberschreitung",
                null,
                controlled ? string.Empty : "Paperless hat nicht rechtzeitig geantwortet.",
                exceptionType,
                IsTimeout: !controlled,
                IsControlledCancellation: controlled);
        }

        if (exception is HttpRequestException httpException)
        {
            int? statusCode = httpException.StatusCode is null
                ? null
                : (int)httpException.StatusCode.Value;

            if (statusCode is not null)
            {
                return FromStatusCode(eventId, statusCode.Value, exceptionType);
            }

            return new(
                eventId,
                "Netzwerkfehler",
                null,
                "Paperless ist derzeit nicht erreichbar.",
                exceptionType,
                IsTimeout: false,
                IsControlledCancellation: false);
        }

        if (exception is JsonException)
        {
            return new(
                eventId,
                "Unerwartete Antwort",
                null,
                "Paperless hat eine unerwartete Antwort geliefert.",
                exceptionType,
                IsTimeout: false,
                IsControlledCancellation: false);
        }

        if (exception is InvalidOperationException invalidOperationException)
        {
            if (TryGetControlledUserMessage(
                    invalidOperationException.Message,
                    out var controlledUserMessage))
            {
                return new(
                    eventId,
                    "Kontrollierter Fachfehler",
                    null,
                    controlledUserMessage,
                    exceptionType,
                    IsTimeout: false,
                    IsControlledCancellation: false);
            }

            if (IsKnownUnexpectedPaperlessResponse(
                    invalidOperationException.Message))
            {
                return new(
                    eventId,
                    "Ungültige oder unvollständige Antwort",
                    null,
                    "Paperless hat unvollständige oder unerwartete Daten geliefert.",
                    exceptionType,
                    IsTimeout: false,
                    IsControlledCancellation: false);
            }
        }

        return new(
            eventId,
            "Unbekannter technischer Fehler",
            null,
            fallbackMessage,
            exceptionType,
            IsTimeout: false,
            IsControlledCancellation: false);
    }


    private static bool TryGetControlledUserMessage(
        string message,
        out string controlledUserMessage)
    {
        controlledUserMessage = message switch
        {
            "Für den angemeldeten Benutzer ist noch kein persönlicher Paperless-Zugang hinterlegt." => message,
            "Der benutzergebundene Paperless-Zugriff ist noch nicht initialisiert." => message,
            "Der benutzerbezogene Cacheschlüssel fehlt." => message,
            "Die Paperless-Basisadresse ist für diese Sitzung nicht verfügbar." => message,
            "Das Paperless-Konto konnte aus der Profilantwort nicht eindeutig bestimmt werden." => message,
            "Der Dokumentbestand hat sich während beider automatischen Cacheaufbauten geändert." => message,
            _ => string.Empty
        };

        return controlledUserMessage.Length > 0;
    }

    private static bool IsKnownUnexpectedPaperlessResponse(string message)
    {
        return message is
            "Die Paperless-API hat eine bereits geladene Folgeseite erneut gemeldet." or
            "Die Paperless-API hat für eine Stammdatenseite keine auswertbare Antwort geliefert." or
            "Die Paperless-API hat eine leere Stammdatenseite mit einer weiteren Folgeseite gemeldet.";
    }

    private static PaperlessErrorInfo FromStatusCode(
        string eventId,
        int statusCode,
        string exceptionType)
    {
        var message = statusCode switch
        {
            401 => "Der persönliche Paperless-Zugang wurde nicht akzeptiert. [#401]",
            403 => "Der persönliche Paperless-Zugang besitzt nicht die erforderliche Berechtigung. [#403]",
            404 => "Die angeforderten Daten sind in Paperless nicht vorhanden. [#404]",
            408 => "Paperless hat nicht rechtzeitig geantwortet. [#408]",
            429 => "Paperless erhält derzeit zu viele Anfragen. Bitte versuche es später erneut. [#429]",
            500 => "Paperless konnte die Anfrage wegen eines internen Fehlers nicht verarbeiten. [#500]",
            502 => "Paperless ist über die bestehende Verbindung derzeit nicht erreichbar. [#502]",
            503 => "Paperless ist derzeit nicht verfügbar. Bitte versuche es später erneut. [#503]",
            504 => "Paperless hat nicht rechtzeitig geantwortet. [#504]",
            >= 400 and <= 499 => $"Die Anfrage an Paperless konnte nicht ausgeführt werden. [#{statusCode}]",
            >= 500 and <= 599 => $"Paperless konnte die Anfrage vorübergehend nicht verarbeiten. [#{statusCode}]",
            _ => $"Die Paperless-Anfrage konnte nicht ausgeführt werden. [#{statusCode}]"
        };

        var category = statusCode switch
        {
            401 => "Zugang nicht akzeptiert",
            403 => "Berechtigung nicht ausreichend",
            404 => "Daten nicht vorhanden",
            408 or 504 => "Zeitüberschreitung",
            429 => "Anfragelimit erreicht",
            >= 500 and <= 599 => "Paperless-Serverfehler",
            _ => "HTTP-Fehler"
        };

        return new(
            eventId,
            category,
            statusCode,
            message,
            exceptionType,
            IsTimeout: statusCode is 408 or 504,
            IsControlledCancellation: false);
    }

    private static string CreateEventId()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        return $"PLS-{Convert.ToHexString(bytes)}";
    }
}
