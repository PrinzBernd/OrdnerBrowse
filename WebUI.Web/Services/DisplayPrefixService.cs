using System.Buffers;
using System.Text;
using System.Text.Json;

namespace WebUI.Web.Services;

public sealed class DisplayPrefixService : IDisposable
{
    private const int ReloadDelayMilliseconds = 200;

    private static readonly HashSet<string> DatePatterns =
    [
        "yyyyMMdd",
        "yyMMdd",
        "yyyy-MM-dd",
        "yy-MM-dd",
        "yyyy.MM.dd",
        "yy.MM.dd",
        "ddMMyyyy",
        "ddMMyy",
        "dd-MM-yyyy",
        "dd-MM-yy",
        "dd.MM.yyyy",
        "dd.MM.yy"
    ];

    private readonly ILogger<DisplayPrefixService> _logger;
    private readonly string? _filePath;
    private readonly FileSystemWatcher? _watcher;
    private readonly object _reloadSync = new();
    private Timer? _reloadTimer;
    private DisplayPrefixConfiguration _current = DisplayPrefixConfiguration.Empty;
    private bool _disposed;

    public DisplayPrefixService(
        IConfiguration configuration,
        ILogger<DisplayPrefixService> logger)
    {
        _logger = logger;
        _filePath = ResolveFilePath(configuration);

        ReloadConfiguration(isInitialLoad: true);
        _watcher = CreateWatcher();
    }

    public event Action? Changed;

    public string ApplyArea(string name) =>
        Apply(name, _current.Areas);

    public string ApplyCorrespondent(string name) =>
        Apply(name, _current.Correspondents);

    public string ApplyDocumentType(string name) =>
        Apply(name, _current.DocumentTypes);

    public string ApplyDocument(string name) =>
        Apply(name, _current.Documents);

    private static string? ResolveFilePath(IConfiguration configuration)
    {
        var configuredPath = configuration["WebUi:DisplayPrefixesFilePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return null;
        }

        return Path.Combine(
            home,
            "Library",
            "Application Support",
            "webui",
            "config",
            "display-prefixes.json");
    }

    private FileSystemWatcher? CreateWatcher()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(_filePath);
        var fileName = Path.GetFileName(_filePath);
        if (string.IsNullOrWhiteSpace(directory) ||
            string.IsNullOrWhiteSpace(fileName) ||
            !Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory, fileName)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.CreationTime |
                               NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnWatchedFileChanged;
            watcher.Created += OnWatchedFileChanged;
            watcher.Deleted += OnWatchedFileChanged;
            watcher.Renamed += OnWatchedFileRenamed;
            return watcher;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "Die automatische Überwachung der Display-Präfix-Konfiguration konnte nicht aktiviert werden.");
            return null;
        }
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs args) =>
        ScheduleReload();

    private void OnWatchedFileRenamed(object sender, RenamedEventArgs args) =>
        ScheduleReload();

    private void ScheduleReload()
    {
        lock (_reloadSync)
        {
            if (_disposed)
            {
                return;
            }

            _reloadTimer ??= new Timer(
                _ => ReloadConfiguration(isInitialLoad: false),
                null,
                Timeout.Infinite,
                Timeout.Infinite);

            _reloadTimer.Change(
                ReloadDelayMilliseconds,
                Timeout.Infinite);
        }
    }

    private void ReloadConfiguration(bool isInitialLoad)
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            return;
        }

        if (!File.Exists(_filePath))
        {
            ReplaceConfiguration(DisplayPrefixConfiguration.Empty, isInitialLoad);
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(_filePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            WarnLoadFailure(isInitialLoad);
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            ReplaceConfiguration(DisplayPrefixConfiguration.Empty, isInitialLoad);
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                WarnLoadFailure(isInitialLoad);
                return;
            }

            var invalidRuleCount = 0;
            var configuration = new DisplayPrefixConfiguration(
                ReadRules(document.RootElement, "Areas", ref invalidRuleCount),
                ReadRules(document.RootElement, "Correspondents", ref invalidRuleCount),
                ReadRules(document.RootElement, "DocumentTypes", ref invalidRuleCount),
                ReadRules(document.RootElement, "Documents", ref invalidRuleCount));

            if (invalidRuleCount > 0)
            {
                _logger.LogWarning(
                    "Die Display-Präfix-Konfiguration enthält {InvalidRuleCount} ungültige Regel(n); diese wurden ignoriert.",
                    invalidRuleCount);
            }

            ReplaceConfiguration(configuration, isInitialLoad);
        }
        catch (JsonException)
        {
            WarnLoadFailure(isInitialLoad);
        }
    }

    private void WarnLoadFailure(bool isInitialLoad)
    {
        _logger.LogWarning(
            isInitialLoad
                ? "Die Display-Präfix-Konfiguration ist ungültig und wurde ignoriert."
                : "Die geänderte Display-Präfix-Konfiguration ist ungültig; die letzte gültige Konfiguration bleibt aktiv.");
    }

    private void ReplaceConfiguration(
        DisplayPrefixConfiguration configuration,
        bool isInitialLoad)
    {
        _current = configuration;
        if (!isInitialLoad)
        {
            Changed?.Invoke();
        }
    }

    private static DisplayPrefixRule[] ReadRules(
        JsonElement root,
        string sectionName,
        ref int invalidRuleCount)
    {
        JsonElement section = default;
        var found = false;
        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            section = property.Value;
            found = true;
            break;
        }

        if (!found)
        {
            return [];
        }

        if (section.ValueKind != JsonValueKind.Array)
        {
            invalidRuleCount++;
            return [];
        }

        var rules = new List<DisplayPrefixRule>();
        foreach (var item in section.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                invalidRuleCount++;
                continue;
            }

            var value = item.GetString();
            if (string.IsNullOrEmpty(value) || !TryCompileRule(value, out var rule))
            {
                invalidRuleCount++;
                continue;
            }

            rules.Add(rule);
        }

        return [.. rules];
    }

    private static bool TryCompileRule(
        string value,
        out DisplayPrefixRule rule)
    {
        var segments = new List<DisplayPrefixSegment>();
        var fixedStart = 0;
        var index = 0;

        while (index < value.Length)
        {
            if (value[index] == '}')
            {
                rule = default!;
                return false;
            }

            if (value[index] != '{')
            {
                index++;
                continue;
            }

            if (index > fixedStart)
            {
                segments.Add(DisplayPrefixSegment.Fixed(value[fixedStart..index]));
            }

            var closingBrace = value.IndexOf('}', index + 1);
            if (closingBrace < 0)
            {
                rule = default!;
                return false;
            }

            var pattern = value[(index + 1)..closingBrace];
            if (pattern.Length == 0 || pattern.Contains('{'))
            {
                rule = default!;
                return false;
            }

            if (DatePatterns.Contains(pattern))
            {
                segments.Add(DisplayPrefixSegment.Date(pattern));
            }
            else if (pattern.Contains('?') || pattern.Contains('#'))
            {
                segments.Add(DisplayPrefixSegment.CharacterPattern(pattern));
            }
            else
            {
                rule = default!;
                return false;
            }

            index = closingBrace + 1;
            fixedStart = index;
        }

        if (fixedStart < value.Length)
        {
            segments.Add(DisplayPrefixSegment.Fixed(value[fixedStart..]));
        }

        if (segments.Count == 0)
        {
            rule = default!;
            return false;
        }

        rule = new DisplayPrefixRule([.. segments]);
        return true;
    }

    private static string Apply(
        string name,
        IReadOnlyList<DisplayPrefixRule> rules)
    {
        ArgumentNullException.ThrowIfNull(name);

        var longestMatch = 0;
        foreach (var rule in rules)
        {
            if (TryMatch(name, rule, out var consumedLength) &&
                consumedLength > longestMatch)
            {
                longestMatch = consumedLength;
            }
        }

        return longestMatch == 0
            ? name
            : name[longestMatch..];
    }

    private static bool TryMatch(
        string name,
        DisplayPrefixRule rule,
        out int consumedLength)
    {
        var offset = 0;
        foreach (var segment in rule.Segments)
        {
            switch (segment.Kind)
            {
                case DisplayPrefixSegmentKind.Date:
                    if (!TryMatchDate(name, ref offset, segment.Text))
                    {
                        consumedLength = 0;
                        return false;
                    }
                    break;

                case DisplayPrefixSegmentKind.CharacterPattern:
                    if (!TryMatchCharacterPattern(name, ref offset, segment.Text))
                    {
                        consumedLength = 0;
                        return false;
                    }
                    break;

                default:
                    if (offset + segment.Text.Length > name.Length ||
                        !name.AsSpan(offset, segment.Text.Length).Equals(
                            segment.Text,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        consumedLength = 0;
                        return false;
                    }

                    offset += segment.Text.Length;
                    break;
            }
        }

        consumedLength = offset;
        return offset > 0;
    }

    private static bool TryMatchDate(
        string name,
        ref int offset,
        string pattern)
    {
        if (offset + pattern.Length > name.Length)
        {
            return false;
        }

        var text = name.AsSpan(offset, pattern.Length);
        if (!TryParseDatePattern(text, pattern))
        {
            return false;
        }

        offset += pattern.Length;
        return true;
    }

    private static bool TryParseDatePattern(
        ReadOnlySpan<char> text,
        string pattern)
    {
        var textOffset = 0;
        var patternOffset = 0;
        var year = 0;
        var month = 0;
        var day = 0;
        var yearDigits = 0;

        while (patternOffset < pattern.Length)
        {
            if (pattern.AsSpan(patternOffset).StartsWith("yyyy", StringComparison.Ordinal))
            {
                if (!TryReadAsciiNumber(text, ref textOffset, 4, out year))
                {
                    return false;
                }

                yearDigits = 4;
                patternOffset += 4;
                continue;
            }

            if (pattern.AsSpan(patternOffset).StartsWith("yy", StringComparison.Ordinal))
            {
                if (!TryReadAsciiNumber(text, ref textOffset, 2, out year))
                {
                    return false;
                }

                yearDigits = 2;
                patternOffset += 2;
                continue;
            }

            if (pattern.AsSpan(patternOffset).StartsWith("MM", StringComparison.Ordinal))
            {
                if (!TryReadAsciiNumber(text, ref textOffset, 2, out month))
                {
                    return false;
                }

                patternOffset += 2;
                continue;
            }

            if (pattern.AsSpan(patternOffset).StartsWith("dd", StringComparison.Ordinal))
            {
                if (!TryReadAsciiNumber(text, ref textOffset, 2, out day))
                {
                    return false;
                }

                patternOffset += 2;
                continue;
            }

            if (textOffset >= text.Length ||
                text[textOffset] != pattern[patternOffset])
            {
                return false;
            }

            textOffset++;
            patternOffset++;
        }

        if (textOffset != text.Length ||
            yearDigits == 0 ||
            month == 0 ||
            day == 0)
        {
            return false;
        }

        if (yearDigits == 2)
        {
            year += 2000;
        }

        try
        {
            _ = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryReadAsciiNumber(
        ReadOnlySpan<char> text,
        ref int offset,
        int length,
        out int value)
    {
        value = 0;
        if (offset + length > text.Length)
        {
            return false;
        }

        for (var index = 0; index < length; index++)
        {
            var character = text[offset + index];
            if (character is < '0' or > '9')
            {
                return false;
            }

            value = (value * 10) + (character - '0');
        }

        offset += length;
        return true;
    }

    private static bool TryMatchCharacterPattern(
        string name,
        ref int offset,
        string pattern)
    {
        var patternOffset = 0;

        while (patternOffset < pattern.Length)
        {
            if (!TryReadRune(pattern, ref patternOffset, out var patternRune) ||
                !TryReadRune(name, ref offset, out var nameRune))
            {
                return false;
            }

            if (patternRune.Value == '?')
            {
                if (!Rune.IsLetter(nameRune) && !IsAsciiDigit(nameRune))
                {
                    return false;
                }

                continue;
            }

            if (patternRune.Value == '#')
            {
                if (!IsAsciiDigit(nameRune))
                {
                    return false;
                }

                continue;
            }

            if (Rune.ToUpperInvariant(patternRune) !=
                Rune.ToUpperInvariant(nameRune))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadRune(
        string value,
        ref int offset,
        out Rune rune)
    {
        if (offset >= value.Length)
        {
            rune = default;
            return false;
        }

        var status = Rune.DecodeFromUtf16(
            value.AsSpan(offset),
            out rune,
            out var consumed);

        if (status != OperationStatus.Done)
        {
            return false;
        }

        offset += consumed;
        return true;
    }

    private static bool IsAsciiDigit(Rune rune) =>
        rune.Value >= '0' && rune.Value <= '9';

    public void Dispose()
    {
        lock (_reloadSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _reloadTimer?.Dispose();
            _reloadTimer = null;
        }

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnWatchedFileChanged;
            _watcher.Created -= OnWatchedFileChanged;
            _watcher.Deleted -= OnWatchedFileChanged;
            _watcher.Renamed -= OnWatchedFileRenamed;
            _watcher.Dispose();
        }
    }

    private sealed record DisplayPrefixConfiguration(
        DisplayPrefixRule[] Areas,
        DisplayPrefixRule[] Correspondents,
        DisplayPrefixRule[] DocumentTypes,
        DisplayPrefixRule[] Documents)
    {
        public static DisplayPrefixConfiguration Empty { get; } =
            new([], [], [], []);
    }

    private sealed record DisplayPrefixRule(
        DisplayPrefixSegment[] Segments);

    private enum DisplayPrefixSegmentKind
    {
        Fixed,
        Date,
        CharacterPattern
    }

    private sealed record DisplayPrefixSegment(
        string Text,
        DisplayPrefixSegmentKind Kind)
    {
        public static DisplayPrefixSegment Fixed(string text) =>
            new(text, DisplayPrefixSegmentKind.Fixed);

        public static DisplayPrefixSegment Date(string pattern) =>
            new(pattern, DisplayPrefixSegmentKind.Date);

        public static DisplayPrefixSegment CharacterPattern(string pattern) =>
            new(pattern, DisplayPrefixSegmentKind.CharacterPattern);
    }
}
