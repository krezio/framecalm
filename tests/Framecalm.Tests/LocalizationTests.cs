using System.Text.Json;
using System.Text.RegularExpressions;
using Framecalm.Core;

namespace Framecalm.Tests;

public class LocalizationTests
{
    static string Fmt(string template, params object[] args) => L.Format(template, args);

    [Theory]
    [InlineData(1, "1 изменение")]
    [InlineData(2, "2 изменения")]
    [InlineData(4, "4 изменения")]
    [InlineData(5, "5 изменений")]
    [InlineData(11, "11 изменений")]
    [InlineData(12, "12 изменений")]
    [InlineData(21, "21 изменение")]
    [InlineData(22, "22 изменения")]
    [InlineData(37, "37 изменений")]
    [InlineData(111, "111 изменений")]
    public void Russian_plurals_follow_one_few_many(int n, string expected) =>
        Assert.Equal(expected, Fmt("{0} {0|изменение|изменения|изменений}", n));

    [Theory]
    [InlineData(1, "Apply 1 change")]
    [InlineData(0, "Apply 0 changes")]
    [InlineData(37, "Apply 37 changes")]
    public void English_plurals_use_one_and_other(int n, string expected) =>
        Assert.Equal(expected, Fmt("Apply {0} {0|change|changes}", n));

    [Fact]
    public void Plain_placeholders_and_formats_still_work() =>
        Assert.Equal("Round 2: changing 3 settings", Fmt("Round {0}: changing {1} {1|setting|settings}", 2, 3));

    [Fact]
    public void Unknown_text_comes_back_unchanged() =>
        Assert.Equal("Some text nobody translated", L.T("Some text nobody translated"));

    [Fact]
    public void Every_Russian_template_keeps_its_placeholders()
    {
        using var s = typeof(L).Assembly.GetManifestResourceStream("Framecalm.Core.Data.ru.json");
        Assert.NotNull(s);
        var ru = JsonSerializer.Deserialize<Dictionary<string, string>>(s!)!;
        Assert.True(ru.Count > 900, "the Russian translation should be complete");
        var token = new Regex(@"\{(\d+)(?:\|([^}]*))?(?::[^}]*)?\}");
        foreach (var (en, tr) in ru)
        {
            var a = token.Matches(en).Select(m => m.Groups[1].Value).ToHashSet();
            var b = token.Matches(tr).Select(m => m.Groups[1].Value).ToHashSet();
            Assert.True(a.SetEquals(b), $"placeholders differ for: {en}");
            foreach (Match m in token.Matches(tr))
                if (m.Groups[2].Success) Assert.True(m.Groups[2].Value.Split('|').Length == 3, $"Russian plural needs 3 forms: {tr}");
        }
    }
}
