namespace LocalizationLibrary;

/// <summary>
///  A single language pack. <see cref="Strings"/> maps English UI text to the
///  translated text. Missing entries fall back to the original English text.
/// </summary>
public class Language
{
    /// <summary>
    ///  BCP-47 language code, e.g. "zh-CN", "de-DE".
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    ///  The language's name in its own language, e.g. "简体中文".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///  The language's name in English, e.g. "Chinese (Simplified)".
    /// </summary>
    public required string EnglishName { get; init; }

    /// <summary>
    ///  English source text -> translated text.
    /// </summary>
    public required Dictionary<string, string> Strings { get; init; }

    public override string ToString() => $"{EnglishName} ({Name})";
}
