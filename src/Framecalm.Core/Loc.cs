using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Framecalm.Core;

/// <summary>
/// The app's text in other languages. English is the source: every string is looked up by its English text,
/// so anything without a translation simply stays English. Translations live in Data/&lt;lang&gt;.json.
///
/// Templates may use plural forms: "{0|change|changes}" in English, "{0|изменение|изменения|изменений}" in Russian
/// (one / few / many). The number is argument 0 in that example.
/// </summary>
public static class L
{
    static Dictionary<string, string> _map = new(StringComparer.Ordinal);

    /// <summary>"en" or "ru". Set once at startup, before any window is created.</summary>
    public static string Lang { get; private set; } = "en";
    public static bool IsRussian => Lang == "ru";

    /// <summary>For dates and numbers shown on screen. The app's own parsing keeps using the invariant culture.</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentCulture;

    public static readonly string[] Supported = ["en", "ru"];

    /// <summary>"auto" follows the Windows display language.</summary>
    public static void Use(string? choice)
    {
        var lang = choice is "en" or "ru" ? choice
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        Lang = lang;
        Culture = lang == "ru" ? CultureInfo.GetCultureInfo("ru-RU") : CultureInfo.GetCultureInfo("en-US");
        _map = lang == "en" ? new(StringComparer.Ordinal) : Load(lang);
    }

    static Dictionary<string, string> Load(string lang)
    {
        try
        {
            using var s = typeof(L).Assembly.GetManifestResourceStream($"Framecalm.Core.Data.{lang}.json");
            if (s is null) return new(StringComparer.Ordinal);
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(s) ?? [];
            return new(d.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)), StringComparer.Ordinal);
        }
        catch { return new(StringComparer.Ordinal); }
    }

    /// <summary>Translates a fixed piece of text. Unknown text comes back unchanged.</summary>
    public static string T(string? english)
    {
        if (string.IsNullOrEmpty(english) || _map.Count == 0) return english ?? "";
        if (_map.TryGetValue(english, out var t)) return t;
        var trimmed = english.Trim();
        if (trimmed.Length != english.Length && _map.TryGetValue(trimmed, out t))
            return english[..(english.Length - english.TrimStart().Length)] + t + english[english.TrimEnd().Length..];
        Miss(english);
        return english;
    }

    // ---- development aid: FRAMECALM_L10N_MISSES=<file> records text shown without a translation ----
    static readonly string? MissFile = Environment.GetEnvironmentVariable("FRAMECALM_L10N_MISSES");
    static readonly HashSet<string> Misses = [];
    static void Miss(string s)
    {
        if (MissFile is null || s.Any(c => c is >= '\u0400' and <= '\u04FF')) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(s, "[A-Za-z]{3,}")) return;
        lock (Misses)
        {
            if (!Misses.Add(s)) return;
            try { File.AppendAllText(MissFile, JsonSerializer.Serialize(s) + Environment.NewLine); } catch { }
        }
    }

    /// <summary>Translates a template and fills it in. Text arguments are translated too.</summary>
    public static string F(string english, params object?[] args)
    {
        var template = T(english);
        var a = args.Select(x => x is string s ? (object)T(s) : x).ToArray();
        return Format(template, a);
    }

    /// <summary>string.Format plus "{n|one|few|many}" plural forms, chosen by the number in argument n.</summary>
    public static string Format(string template, object?[] args)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] == '{') { sb.Append("{{"); i += 2; continue; }
            if (c == '{')
            {
                var end = template.IndexOf('}', i);
                if (end < 0) { sb.Append(template[i..]); break; }
                var inner = template[(i + 1)..end];
                var bar = inner.IndexOf('|');
                if (bar > 0 && int.TryParse(inner[..bar], out var idx) && idx < args.Length)
                {
                    var forms = inner[(bar + 1)..].Split('|');
                    sb.Append(Escape(Pick(ToNumber(args[idx]), forms)));
                }
                else sb.Append('{').Append(inner).Append('}');
                i = end + 1;
                continue;
            }
            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { sb.Append("}}"); i += 2; continue; }
            sb.Append(c);
            i++;
        }
        try { return string.Format(Culture, sb.ToString(), args); }
        catch (FormatException) { return template; }
    }

    static string Escape(string s) => s.Replace("{", "{{").Replace("}", "}}");

    static double ToNumber(object? o) => o switch
    {
        null => 0,
        int n => n,
        long n => n,
        double d => d,
        float f => f,
        decimal m => (double)m,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
        _ => 0,
    };

    /// <summary>English: one / other. Russian: one / few / many.</summary>
    static string Pick(double n, string[] forms)
    {
        if (forms.Length == 0) return "";
        if (Math.Abs(n % 1) > 0.0001) return forms[Math.Min(forms.Length - 1, 1)];
        var k = (long)Math.Abs(n);
        if (forms.Length >= 3)
        {
            long m10 = k % 10, m100 = k % 100;
            if (m10 == 1 && m100 != 11) return forms[0];
            if (m10 is >= 2 and <= 4 && (m100 < 12 || m100 > 14)) return forms[1];
            return forms[2];
        }
        return k == 1 ? forms[0] : forms[^1];
    }
}
