using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using ETS2LA.Logging;
using ETS2LA.Settings;

namespace LocalizationLibrary;

/// <summary>
///  Singleton translation engine. Holds all embedded language packs, exposes the
///  currently selected language and translates English UI strings into it.
///  Works off English source text because ETS2LA hardcodes English strings in its UI.
/// </summary>
public class LocalizationManager
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Current => _instance.Value;

    public const string EnglishCode = "en-US";
    public const string SettingsFileName = "LocalizationSettings.json";

    /// <summary>
    ///  Fires when the active language changes. Re-apply translations to any open UI.
    /// </summary>
    public event Action<Language>? LanguageChanged;

    private readonly List<Language> _languages = new();
    private readonly SettingsHandler _settingsHandler = new();
    private LocalizationSettings _options = new();
    private Language _current;
    private Dictionary<string, string> _currentStrings = new();
    private List<TranslationPattern> _currentPatterns = new();

    private LocalizationManager()
    {
        _current = CreateEnglishFallback();
    }

    /// <summary>
    ///  Loads all embedded language packs and restores the saved selection.
    ///  Call once from the main plugin's <c>Init()</c>.
    /// </summary>
    public void Initialize()
    {
        var assembly = typeof(LocalizationManager).Assembly;
        foreach (string resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;

                var language = JsonSerializer.Deserialize<Language>(stream, JsonOptions);
                if (language == null || string.IsNullOrWhiteSpace(language.Code))
                    continue;

                // The English pack is identity; don't duplicate it.
                if (string.Equals(language.Code, EnglishCode, StringComparison.OrdinalIgnoreCase))
                    continue;

                _languages.Add(language);
                Logger.Info($"Localization: Loaded language pack [gray]{language.Code}[/] ({language.EnglishName}).");
            }
            catch (Exception ex)
            {
                Logger.Error($"Localization: Failed to load language pack from [gray]{resourceName}[/]: {ex}");
            }
        }

        _languages.Sort((a, b) => string.Compare(a.EnglishName, b.EnglishName, StringComparison.OrdinalIgnoreCase));

        _options = _settingsHandler.Load<LocalizationSettings>(SettingsFileName);
        _current = CreateEnglishFallback();
        SetLanguage(_options.LanguageCode, persist: false);
        Logger.Info($"Localization: Active language: [gray]{_current.Code}[/] ({_current.EnglishName}).");
    }

    /// <summary>
    ///  All available languages, including the built-in English fallback.
    /// </summary>
    public IReadOnlyList<Language> Languages
    {
        get
        {
            var list = new List<Language> { CreateEnglishFallback() };
            list.AddRange(_languages);
            return list;
        }
    }

    /// <summary>
    ///  The currently selected language.
    /// </summary>
    public Language CurrentLanguage => _current;

    /// <summary>
    ///  Translates English UI text. Falls back to the original text when no
    ///  translation exists for the active language.
    /// </summary>
    public string Translate(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        if (_currentStrings.TryGetValue(text, out var translation))
            return translation;

        foreach (var pattern in _currentPatterns)
        {
            var match = pattern.Matcher.Match(text);
            if (match.Success)
                return pattern.Format(match);
        }

        return text;
    }

    /// <summary>
    ///  Switches the active language and persists the choice.
    /// </summary>
    public void SetLanguage(string? code)
    {
        SetLanguage(code, persist: true);
    }

    /// <summary>
    ///  Finds a language by BCP-47 code, or null.
    /// </summary>
    public Language? GetLanguage(string? code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        foreach (var language in Languages)
        {
            if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
                return language;
        }

        return null;
    }

    private void SetLanguage(string? code, bool persist)
    {
        var language = GetLanguage(code) ?? CreateEnglishFallback();
        _current = language;
        _currentStrings = new Dictionary<string, string>(language.Strings, StringComparer.Ordinal);
        _currentPatterns = BuildPatterns(language.Strings);

        if (persist)
        {
            _options.LanguageCode = language.Code;
            SaveOptions();
            Logger.Info($"Localization: Language changed to [gray]{language.Code}[/] ({language.EnglishName}).");
        }

        LanguageChanged?.Invoke(language);
    }

    /// <summary>
    ///  Whether window titles are translated. Persisted with the rest of the settings.
    /// </summary>
    public bool TranslateWindowTitles
    {
        get => _options.TranslateWindowTitles;
        set
        {
            if (_options.TranslateWindowTitles == value) return;
            _options.TranslateWindowTitles = value;
            SaveOptions();
        }
    }

    /// <summary>
    ///  Whether accessibility strings (AutomationProperties.Name) are translated.
    /// </summary>
    public bool TranslateAccessibilityNames
    {
        get => _options.TranslateAccessibilityNames;
        set
        {
            if (_options.TranslateAccessibilityNames == value) return;
            _options.TranslateAccessibilityNames = value;
            SaveOptions();
        }
    }

    private void SaveOptions()
    {
        _settingsHandler.Save(SettingsFileName, _options);
        Logger.Info($"Localization: Settings saved: titles=[gray]{_options.TranslateWindowTitles}[/], " +
                    $"accessibility=[gray]{_options.TranslateAccessibilityNames}[/].");
    }

    private static List<TranslationPattern> BuildPatterns(IReadOnlyDictionary<string, string> strings)
    {
        return strings
            .Where(pair => PlaceholderRegex.IsMatch(pair.Key))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => TranslationPattern.Create(pair.Key, pair.Value))
            .ToList();
    }

    private static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);

    private static Language CreateEnglishFallback() => new Language
    {
        Code = EnglishCode,
        Name = "English",
        EnglishName = "English",
        Strings = new Dictionary<string, string>(),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private sealed class TranslationPattern
    {
        private readonly IReadOnlyDictionary<int, string[]> _groups;

        private TranslationPattern(
            Regex matcher,
            string translation,
            IReadOnlyDictionary<int, string[]> groups)
        {
            Matcher = matcher;
            Translation = translation;
            _groups = groups;
        }

        public Regex Matcher { get; }
        private string Translation { get; }

        public string Format(Match match)
        {
            return PlaceholderRegex.Replace(Translation, placeholder =>
            {
                int index = int.Parse(placeholder.Groups[1].Value);
                if (!_groups.TryGetValue(index, out var names))
                    return placeholder.Value;

                foreach (var name in names)
                {
                    var group = match.Groups[name];
                    if (group.Success)
                        return group.Value;
                }

                return string.Empty;
            });
        }

        public static TranslationPattern Create(string source, string translation)
        {
            var groups = new Dictionary<int, List<string>>();
            var pattern = new System.Text.StringBuilder("^");
            int cursor = 0;
            int occurrence = 0;

            foreach (Match placeholder in PlaceholderRegex.Matches(source))
            {
                pattern.Append(Regex.Escape(source[cursor..placeholder.Index]));

                int index = int.Parse(placeholder.Groups[1].Value);
                string groupName = $"p{occurrence++}";
                pattern.Append($"(?<{groupName}>.*?)");
                if (!groups.TryGetValue(index, out var names))
                {
                    names = new List<string>();
                    groups[index] = names;
                }
                names.Add(groupName);
                cursor = placeholder.Index + placeholder.Length;
            }

            pattern.Append(Regex.Escape(source[cursor..]));
            pattern.Append("$");

            return new TranslationPattern(
                new Regex(pattern.ToString(), RegexOptions.Compiled | RegexOptions.Singleline),
                translation,
                groups.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }
    }
}

[Serializable]
public class LocalizationSettings
{
    public string LanguageCode { get; set; } = LocalizationManager.EnglishCode;

    /// <summary>
    ///  Whether window titles should be translated.
    /// </summary>
    public bool TranslateWindowTitles { get; set; } = true;

    /// <summary>
    ///  Whether accessibility (AutomationProperties.Name) strings should be translated.
    /// </summary>
    public bool TranslateAccessibilityNames { get; set; } = false;

}
