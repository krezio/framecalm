using System.Net.Http;
using System.Text.Json;
using Framecalm.Core;

namespace Framecalm.App.Services;

/// <summary>
/// Official cover art, shown on the user's own PC like any game launcher does. Order: the art the launcher
/// already cached locally, then Framecalm's own cache, then one download from the publisher's storefront
/// (Steam's image server or the Microsoft Store catalog). Nothing is bundled with the app; if there's no
/// network or the setting is off, a generated cover is used instead.
/// </summary>
public static class CoverArt
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    public static string CacheDir { get; } = Path.Combine(Paths.State, "covers");

    static string Safe(string s) => new(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

    /// <summary>Path to a cached cover for this game, downloading it once if needed. Null when unavailable.</summary>
    public static async Task<string?> EnsureAsync(string gameId, string? cover)
    {
        if (string.IsNullOrEmpty(cover)) return null;
        var file = Path.Combine(CacheDir, Safe(gameId) + ".img");
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length > 2000) return file;
            var parts = cover.Split(':', 2);
            if (parts.Length != 2) return null;
            if (parts[0] != "search" && !parts[1].All(char.IsLetterOrDigit)) return null;
            var url = parts[0] switch
            {
                "search" => await StoreSearchAsync(parts[1]),
                "steam" => $"https://cdn.cloudflare.steamstatic.com/steam/apps/{parts[1]}/library_600x900.jpg",
                "store" => await StorePosterAsync(parts[1]),
                _ => null,
            };
            if (url is null) return null;
            var bytes = await Http.GetByteArrayAsync(url);
            if (bytes.Length < 2000 || !LooksLikeImage(bytes)) return null;
            Directory.CreateDirectory(CacheDir);
            var tmp = file + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes);
            File.Move(tmp, file, overwrite: true);
            return file;
        }
        catch { return null; } // offline, blocked or moved: the generated cover stays
    }

    static bool LooksLikeImage(byte[] b) =>
        (b[0] == 0xFF && b[1] == 0xD8) || (b[0] == 0x89 && b[1] == 0x50) || (b[0] == 'R' && b[1] == 'I' && b[8] == 'W');

    /// <summary>Finds a game by name in the Microsoft Store catalog (used for Xbox app games, which only give us a name).</summary>
    static async Task<string?> StoreSearchAsync(string name)
    {
        var json = await Http.GetStringAsync($"https://displaycatalog.mp.microsoft.com/v7.0/productFamilies/Games/products?query={Uri.EscapeDataString(name)}&market=US&languages=en-us&$top=10");
        using var doc = JsonDocument.Parse(json);
        // an exact title match wins; a longer title that starts with the name is second best. Anything else is skipped,
        // so a game never gets another game's cover.
        string? prefix = null;
        foreach (var p in doc.RootElement.GetProperty("Products").EnumerateArray())
        {
            var lp = p.GetProperty("LocalizedProperties")[0];
            var title = Clean(lp.GetProperty("ProductTitle").GetString() ?? "");
            var exact = title == Clean(name);
            if (!exact && !title.StartsWith(Clean(name))) continue;
            foreach (var img in lp.GetProperty("Images").EnumerateArray())
                if (img.GetProperty("ImagePurpose").GetString() == "Poster" && img.GetProperty("Uri").GetString() is { } uri)
                {
                    var url = (uri.StartsWith("//") ? "https:" + uri : uri) + "?w=600&h=900&q=90";
                    if (exact) return url;
                    prefix ??= url;
                }
        }
        return prefix;
    }

    static string Clean(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Portrait poster from the Microsoft Store's public catalog.</summary>
    static async Task<string?> StorePosterAsync(string productId)
    {
        var json = await Http.GetStringAsync($"https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds={productId}&market=US&languages=en-us");
        using var doc = JsonDocument.Parse(json);
        var images = doc.RootElement.GetProperty("Products")[0].GetProperty("LocalizedProperties")[0].GetProperty("Images");
        foreach (var purpose in new[] { "Poster", "BoxArt" })
            foreach (var img in images.EnumerateArray())
                if (img.GetProperty("ImagePurpose").GetString() == purpose && img.GetProperty("Uri").GetString() is { } uri)
                    return (uri.StartsWith("//") ? "https:" + uri : uri) + "?w=600&h=900&q=90";
        return null;
    }
}
