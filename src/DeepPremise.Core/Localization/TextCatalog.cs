using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeepPremise.Core.Localization;

// English remains the canonical simulation text. Localization never mutates world state.
public sealed class TextCatalog
{
    private sealed record Entry(string En, string Ko);
    private sealed record CatalogData(Entry[] Entries, Dictionary<string, string[]> Topics);
    private static readonly CatalogData Data = ReadCatalog();
    private static readonly string Names = "Mara|Iven|Sela|Orren|Neri|Tavi|You";
    private static readonly (Regex Pattern, string Translation)[] Patterns = Data.Entries
        .OrderByDescending(e => e.En.Length)
        .Select(e => (new Regex("\\G" + BuildPattern(e.En), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), e.Ko)).ToArray();
    private readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);
    public string Language { get; }
    public TextCatalog(string language) => Language = language == "ko" ? "ko" : "en";
    private static CatalogData ReadCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DeepPremise.Core.Resources.ko.json")
            ?? throw new InvalidOperationException("Missing Korean catalog.");
        return JsonSerializer.Deserialize<CatalogData>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    private static string BuildPattern(string text)
    {
        var pattern = Regex.Escape(text);
        foreach (var key in new[] { "a", "b" }) pattern = pattern.Replace(Regex.Escape("{" + key + "}"), "(?<" + key + ">" + Names + ")");
        pattern = pattern.Replace(Regex.Escape("{n}"), "(?<n>[0-9]+)");
        return pattern;
    }
    public string Text(string english)
    {
        if (Language == "en" || string.IsNullOrEmpty(english)) return english;
        if (cache.TryGetValue(english, out var cached)) return cached;
        var result = new StringBuilder();
        var offset = 0;
        while (offset < english.Length)
        {
            var found = false;
            foreach (var (pattern, translation) in Patterns)
            {
                var match = pattern.Match(english, offset);
                if (!match.Success || match.Index != offset) continue;
                var localized = translation;
                foreach (var key in new[] { "a", "b", "n" })
                    if (match.Groups[key].Success) localized = localized.Replace("{" + key + "}", Text(match.Groups[key].Value));
                result.Append(localized); offset += match.Length; found = true; break;
            }
            if (!found) result.Append(english[offset++]);
        }
        if (cache.Count > 2048) cache.Clear();
        return cache[english] = result.ToString();
    }
    public string Time(long tick) => Text($"Day {tick / 96 + 1}") + $"  /  {tick % 96 / 4:00}:{tick % 4 * 15:00}";
    public string Line(ConversationLine line)
    {
        if (line.Voices?.TryGetValue(Language, out var generated) == true) return generated;
        return line.IsPlayerInput ? line.Text : Text(line.Text);
    }
    public static string TopicFor(string question)
    {
        foreach (var (topic, keywords) in Data.Topics)
            if (keywords.Any(word => question.Contains(word, StringComparison.OrdinalIgnoreCase))) return topic;
        return "day";
    }
}

public sealed class PlayerPreferences
{
    public string Language { get; set; } = "en";
    public static PlayerPreferences Load(string directory, string systemLanguage)
    {
        var fallback = new PlayerPreferences { Language = systemLanguage.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? "ko" : "en" };
        try
        {
            var path = Path.Combine(directory, "preferences.json");
            if (!File.Exists(path)) return fallback;
            var value = JsonSerializer.Deserialize<PlayerPreferences>(File.ReadAllText(path));
            return value?.Language is "en" or "ko" ? value : fallback;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return fallback; }
    }
    public void Save(string directory)
    {
        if (Language is not ("en" or "ko")) throw new InvalidDataException("Unsupported language.");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "preferences.json");
        File.WriteAllText(target + ".tmp", JsonSerializer.Serialize(this));
        File.Move(target + ".tmp", target, true);
    }
}
