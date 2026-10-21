using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AutoMod;

/// <summary>
/// Sends AutoMod violation logs to a Discord webhook. Supports two modes:
///   - "text"  : a single configurable message line
///   - "embed" : a rich embed that can show the player's Steam avatar as the thumbnail
///
/// The avatar is resolved from the PUBLIC Steam profile XML (steamcommunity.com/profiles/{id64}?xml=1),
/// so NO Steam Web API key is needed. Avatars are cached per SteamID for the session. All network
/// work is fire-and-forget and never blocks the game thread.
/// </summary>
public sealed class DiscordNotifier
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ConcurrentDictionary<string, string> _avatarCache = new();
    private readonly ILogger _logger;

    public DiscordNotifier(ILogger logger) => _logger = logger;

    /// <summary>
    /// Posts a violation to the webhook (fire-and-forget). Placeholder values come from the caller.
    /// </summary>
    public void Send(DiscordConfig cfg, IReadOnlyDictionary<string, string> fields, string steamId64)
    {
        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.WebhookUrl))
            return;

        // Copy inputs so the async task doesn't touch shared/per-frame state.
        var mode = (cfg.Mode ?? "text").Trim().ToLowerInvariant();
        _ = Task.Run(async () =>
        {
            try
            {
                string? avatar = null;
                if (mode == "embed" && cfg.ShowPlayerAvatar)
                    avatar = await ResolveAvatarAsync(steamId64);

                var payload = mode == "embed"
                    ? BuildEmbed(cfg, fields, avatar)
                    : BuildText(cfg, fields);

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var resp = await Http.PostAsync(cfg.WebhookUrl, content);
                if (!resp.IsSuccessStatusCode)
                    _logger.LogWarning("[AutoMod] Discord webhook returned {Code}", (int)resp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AutoMod] Discord webhook failed: {Err}", ex.Message);
            }
        });
    }

    private static string Apply(string template, IReadOnlyDictionary<string, string> f)
    {
        var sb = new StringBuilder(template);
        foreach (var kv in f)
            sb.Replace("{" + kv.Key + "}", kv.Value);
        return sb.ToString();
    }

    private static string BuildText(DiscordConfig cfg, IReadOnlyDictionary<string, string> f)
    {
        var obj = new Dictionary<string, object?>
        {
            ["content"] = WithMessageLine(cfg.Format, f, "\n> {0}")
        };
        if (!string.IsNullOrWhiteSpace(cfg.Username)) obj["username"] = cfg.Username;
        if (!string.IsNullOrWhiteSpace(cfg.AvatarUrl)) obj["avatar_url"] = cfg.AvatarUrl;
        return JsonSerializer.Serialize(obj);
    }

    /// <summary>
    /// Applies the template, then appends the blocked chat line (using <paramref name="lineFormat"/>)
    /// ONLY when there is a message AND the template did not already include {message}. This keeps
    /// NameCheck (no message) from producing an empty "&gt;" line.
    /// </summary>
    private static string WithMessageLine(string template, IReadOnlyDictionary<string, string> f, string lineFormat)
    {
        var text = Apply(template, f);
        var message = f.TryGetValue("message", out var m) ? m : "";
        if (!string.IsNullOrWhiteSpace(message) && !template.Contains("{message}"))
            text += string.Format(lineFormat, message);
        return text;
    }

    private static string BuildEmbed(DiscordConfig cfg, IReadOnlyDictionary<string, string> f, string? avatar)
    {
        var embed = new Dictionary<string, object?>
        {
            ["title"] = Apply(cfg.EmbedTitle, f),
            ["description"] = WithMessageLine(cfg.EmbedDescription, f, "\n**Message:** {0}"),
            ["color"] = HexToColor(cfg.EmbedColor),
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };
        if (!string.IsNullOrWhiteSpace(avatar))
            embed["thumbnail"] = new Dictionary<string, object?> { ["url"] = avatar };

        var obj = new Dictionary<string, object?>
        {
            ["embeds"] = new[] { embed }
        };
        if (!string.IsNullOrWhiteSpace(cfg.Username)) obj["username"] = cfg.Username;
        if (!string.IsNullOrWhiteSpace(cfg.AvatarUrl)) obj["avatar_url"] = cfg.AvatarUrl;
        return JsonSerializer.Serialize(obj);
    }

    private static int HexToColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0xE23B3B;
        var h = hex.TrimStart('#');
        return int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0xE23B3B;
    }

    private static readonly Regex AvatarFullRegex =
        new("<avatarFull>\\s*<!\\[CDATA\\[(.*?)\\]\\]>\\s*</avatarFull>", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Resolves the player's full avatar URL from the public Steam profile XML. No API key needed.
    /// Cached per SteamID. Returns null on any failure.
    /// </summary>
    private async Task<string?> ResolveAvatarAsync(string steamId64)
    {
        if (string.IsNullOrEmpty(steamId64)) return null;
        if (_avatarCache.TryGetValue(steamId64, out var cached)) return cached;

        try
        {
            var xml = await Http.GetStringAsync($"https://steamcommunity.com/profiles/{steamId64}?xml=1");
            var m = AvatarFullRegex.Match(xml);
            if (m.Success)
            {
                var url = m.Groups[1].Value.Trim();
                _avatarCache[steamId64] = url;
                return url;
            }
        }
        catch
        {
            // ignore - no avatar is fine
        }
        return null;
    }
}
