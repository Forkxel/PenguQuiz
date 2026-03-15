using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QuizAPI.Services;

public class UsernameValidatorService
{
    private readonly IWebHostEnvironment _env;

    private readonly Lazy<HashSet<string>> _reservedNames;
    private readonly Lazy<List<string>> _blockedWords;

    private static readonly Regex UsernameRegex =
        new("^[a-zA-Z0-9_]{3,20}$", RegexOptions.Compiled);

    public UsernameValidatorService(IWebHostEnvironment env)
    {
        _env = env;

        _reservedNames = new Lazy<HashSet<string>>(LoadReservedNames);
        _blockedWords = new Lazy<List<string>>(LoadBlockedWords);
    }

    public string? Validate(string username)
    {
        var raw = (username ?? "").Trim();

        if (string.IsNullOrWhiteSpace(raw))
            return "Username is required.";

        if (!UsernameRegex.IsMatch(raw))
            return "Invalid username format. Use 3-20 characters: letters, numbers, underscore.";

        if (raw.StartsWith("guest_", StringComparison.OrdinalIgnoreCase))
            return "Guest_ prefix is reserved.";

        if (raw.StartsWith("_") || raw.EndsWith("_"))
            return "Username cannot start or end with underscore.";

        if (raw.Contains("__"))
            return "Username cannot contain double underscore.";

        var normalized = NormalizeAggressive(raw);

        foreach (var name in _reservedNames.Value)
        {
            if (NormalizeAggressive(name) == normalized)
                return "This username is reserved.";
        }

        foreach (var word in _blockedWords.Value)
        {
            var bad = NormalizeAggressive(word);

            if (!string.IsNullOrWhiteSpace(bad) &&
                bad.Length >= 3 &&
                normalized.Contains(bad, StringComparison.OrdinalIgnoreCase))
            {
                return "Username contains forbidden word.";
            }
        }

        return null;
    }

    public string NormalizeAggressive(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim().ToLowerInvariant();

        value = RemoveDiacritics(value);

        var map = new Dictionary<char, char>
        {
            ['0'] = 'o',
            ['1'] = 'i',
            ['!'] = 'i',
            ['|'] = 'i',
            ['3'] = 'e',
            ['4'] = 'a',
            ['@'] = 'a',
            ['5'] = 's',
            ['$'] = 's',
            ['7'] = 't',
            ['8'] = 'b',
            ['9'] = 'g'
        };

        var mapped = new StringBuilder();

        foreach (var ch in value)
        {
            mapped.Append(map.TryGetValue(ch, out var replacement) ? replacement : ch);
        }

        value = mapped.ToString();

        value = Regex.Replace(value, "[^a-z0-9]", "");
        value = Regex.Replace(value, "(.)\\1{2,}", "$1$1");

        return value;
    }

    private HashSet<string> LoadReservedNames()
    {
        var file = Path.Combine(_env.ContentRootPath, "Config", "UsernameFilter", "reserved_names.txt");
        return LoadWordList(file).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private List<string> LoadBlockedWords()
    {
        var baseDir = Path.Combine(_env.ContentRootPath, "Config", "UsernameFilter");

        var words = new List<string>();

        words.AddRange(LoadWordList(Path.Combine(baseDir, "custom_blocked.txt")));
        words.AddRange(LoadProfanityDirectory(Path.Combine(baseDir, "profanity")));

        return words
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> LoadProfanityDirectory(string dir)
    {
        if (!Directory.Exists(dir))
            return new List<string>();

        var words = new List<string>();

        foreach (var file in Directory.GetFiles(dir, "*.txt", SearchOption.TopDirectoryOnly))
        {
            words.AddRange(LoadWordList(file));
        }

        return words;
    }

    private List<string> LoadWordList(string file)
    {
        if (!File.Exists(file))
            return new List<string>();

        var result = new List<string>();

        foreach (var line in File.ReadAllLines(file))
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("#"))
                continue;

            result.Add(trimmed);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}