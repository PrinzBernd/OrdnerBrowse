using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace WebUI.Web.Services;

public sealed class OidcDiagnosticsFileLoggerSettings
{
    public const string SectionPath = "WebUi:OidcDiagnostics";

    private OidcDiagnosticsFileLoggerSettings(
        string? logDirectory,
        int retentionDays)
    {
        LogDirectory = logDirectory;
        RetentionDays = retentionDays;
    }

    public string? LogDirectory { get; }

    public int RetentionDays { get; }

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(LogDirectory);

    public static OidcDiagnosticsFileLoggerSettings Load(
        IConfiguration configuration)
    {
        var logDirectory =
            configuration[$"{SectionPath}:LogDirectory"]?.Trim();
        var retentionDays =
            configuration.GetValue<int?>(
                $"{SectionPath}:RetentionDays")
            ?? 7;

        if (retentionDays is < 1 or > 365)
        {
            throw new InvalidOperationException(
                "WebUi:OidcDiagnostics:RetentionDays muss zwischen 1 und 365 liegen.");
        }

        if (!string.IsNullOrWhiteSpace(logDirectory) &&
            !Path.IsPathRooted(logDirectory))
        {
            throw new InvalidOperationException(
                "WebUi:OidcDiagnostics:LogDirectory muss ein absoluter Pfad sein.");
        }

        return new(
            string.IsNullOrWhiteSpace(logDirectory)
                ? null
                : logDirectory,
            retentionDays);
    }
}

public sealed class OidcDiagnosticsFileLoggerProvider :
    ILoggerProvider
{
    public const string LoggerCategory =
        "WebUI.OidcDiagnostics";

    private const string FilePrefix =
        "oidc-diagnostics-";
    private const string FilePattern =
        "oidc-diagnostics-*.log";

    private readonly string _logDirectory;
    private readonly TimeSpan _retention;
    private readonly object _sync = new();
    private readonly Timer _cleanupTimer;
    private int _writeFailureReported;
    private bool _disposed;

    public OidcDiagnosticsFileLoggerProvider(
        OidcDiagnosticsFileLoggerSettings settings)
    {
        if (!settings.Enabled ||
            string.IsNullOrWhiteSpace(settings.LogDirectory))
        {
            throw new InvalidOperationException(
                "OIDC-Diagnosedateiprotokollierung benötigt ein konfiguriertes LogDirectory.");
        }

        _logDirectory = settings.LogDirectory;
        _retention =
            TimeSpan.FromDays(settings.RetentionDays);
        _cleanupTimer =
            new Timer(
                _ => CleanupExpiredFilesNoThrow(
                    DateTimeOffset.UtcNow),
                null,
                TimeSpan.Zero,
                TimeSpan.FromHours(1));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new OidcDiagnosticsFileLogger(
            this,
            string.Equals(
                categoryName,
                LoggerCategory,
                StringComparison.Ordinal));
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _cleanupTimer.Dispose();
    }

    private void Write(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception)
    {
        var now = DateTimeOffset.UtcNow;
        var line =
            $"{now:O} [{logLevel}] Version={ApplicationDisplayInfo.FullVersion} {ToSingleLine(message)}";

        if (eventId.Id != 0)
        {
            line += $" EventId={eventId.Id}";
        }

        if (exception is not null)
        {
            line +=
                $" Ausnahmetyp={exception.GetType().Name}";
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_logDirectory);
                CleanupExpiredFilesCore(now);

                var logPath =
                    Path.Combine(
                        _logDirectory,
                        $"{FilePrefix}{now:yyyy-MM-dd}.log");

                File.AppendAllText(
                    logPath,
                    line + Environment.NewLine,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false));
            }
            catch
            {
                ReportWriteFailureOnce();
            }
        }
    }

    private void CleanupExpiredFilesNoThrow(
        DateTimeOffset now)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                CleanupExpiredFilesCore(now);
            }
            catch
            {
                ReportWriteFailureOnce();
            }
        }
    }

    private void CleanupExpiredFilesCore(
        DateTimeOffset now)
    {
        if (!Directory.Exists(_logDirectory))
        {
            return;
        }

        var cutoffUtc =
            now.UtcDateTime - _retention;

        foreach (var path in Directory.EnumerateFiles(
                     _logDirectory,
                     FilePattern,
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoffUtc)
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ein einzelner nicht löschbarer Diagnosebestand darf
                // den Authentifizierungsablauf nicht beeinträchtigen.
            }
        }
    }

    private void ReportWriteFailureOnce()
    {
        if (Interlocked.Exchange(
                ref _writeFailureReported,
                1) != 0)
        {
            return;
        }

        Console.Error.WriteLine(
            "OIDC-Diagnosedateiprotokollierung ist nicht verfügbar; der Authentifizierungsablauf bleibt unverändert.");
    }

    private static string ToSingleLine(
        string value)
    {
        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private sealed class OidcDiagnosticsFileLogger :
        ILogger
    {
        private readonly OidcDiagnosticsFileLoggerProvider _provider;
        private readonly bool _matchesCategory;

        public OidcDiagnosticsFileLogger(
            OidcDiagnosticsFileLoggerProvider provider,
            bool matchesCategory)
        {
            _provider = provider;
            _matchesCategory = matchesCategory;
        }

        public IDisposable? BeginScope<TState>(
            TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(
            LogLevel logLevel)
        {
            return _matchesCategory &&
                   logLevel >= LogLevel.Information &&
                   logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(
                logLevel,
                eventId,
                formatter(state, exception),
                exception);
        }
    }

    private sealed class NullScope :
        IDisposable
    {
        public static NullScope Instance { get; } =
            new();

        public void Dispose()
        {
        }
    }
}
