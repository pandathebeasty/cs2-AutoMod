using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace AutoMod;

/// <summary>
/// AutoMod configuration (configs/plugins/AutoMod/AutoMod.json).
/// Three independent checks - Language, Advertising, Spam - each with its own strike threshold and
/// its own console punishment command. A violation blocks the message and adds one strike for that
/// category; when the category's strike count reaches its threshold, its command is run and the
/// strikes for that category reset.
/// </summary>
public sealed class AutoModConfig : BasePluginConfig
{
    /// <summary>
    /// Players holding this admin flag/permission are completely exempt from AutoMod. Empty = nobody
    /// is exempt. Example: "@css/ban" or "@css/chat".
    /// </summary>
    [JsonPropertyName("FlagBypass")]
    public string FlagBypass { get; set; } = "@css/ban";

    /// <summary>
    /// Seconds after which a category's accumulated strikes are forgotten if no new violation occurs.
    /// 0 = never reset (strikes persist for the whole session).
    /// </summary>
    [JsonPropertyName("StrikeResetSeconds")]
    public int StrikeResetSeconds { get; set; } = 300;

    /// <summary>
    /// Whitelist of substrings that are NEVER blocked, even if a rule would match them. Checked
    /// case-insensitively against the whole message (and player name for NameCheck) before any rule:
    /// if the text contains one of these, that content is allowed through. Use it to protect your
    /// own server/community names, e.g. "go.laleagane.ro", "gollg".
    /// </summary>
    [JsonPropertyName("FilterBypass")]
    public List<string> FilterBypass { get; set; } = ["example.com", "yourserver.net"];

    [JsonPropertyName("Language")]
    public CheckConfig Language { get; set; } = new()
    {
        Enabled = true,
        WordsFile = "language.txt",
        Threshold = 3,
        PunishCommand = "css_gag #{userid} 5 \"AutoMod: bad language\""
    };

    [JsonPropertyName("Advertising")]
    public CheckConfig Advertising { get; set; } = new()
    {
        Enabled = true,
        WordsFile = "advertising.txt",
        Threshold = 2,
        PunishCommand = "css_gag #{userid} 15 \"AutoMod: advertising\""
    };

    [JsonPropertyName("Spam")]
    public SpamConfig Spam { get; set; } = new();

    [JsonPropertyName("NameCheck")]
    public NameCheckConfig NameCheck { get; set; } = new();
}

/// <summary>
/// Name moderation (integrated NameChecker). When a connecting player's name matches a rule in
/// WordsFile (and is not covered by FilterBypass), the player is renamed to ReplacementName.
/// </summary>
public sealed class NameCheckConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// File (in the plugin folder) with one entry per line - a whole word or a /regex/ - tested
    /// against the player name. '#' = comment. Can be the same file as a chat check if you want.
    /// </summary>
    [JsonPropertyName("WordsFile")]
    public string WordsFile { get; set; } = "names.txt";

    /// <summary>The name a flagged player is renamed to.</summary>
    [JsonPropertyName("ReplacementName")]
    public string ReplacementName { get; set; } = "BAD NAME | CHANGE IT";

    /// <summary>Announce the rename to all players in chat (uses the "name.Announce" translation).</summary>
    [JsonPropertyName("AnnounceToAll")]
    public bool AnnounceToAll { get; set; } = false;

    /// <summary>
    /// Optional console command run when a name is flagged, BEFORE the rename. Placeholders:
    /// {userid} {steamid} {name}. Empty = just rename, no command.
    /// </summary>
    [JsonPropertyName("PunishCommand")]
    public string PunishCommand { get; set; } = "";
}

/// <summary>A file-backed check (Language / Advertising).</summary>
public sealed class CheckConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>File (in the plugin folder) with one entry per line: a whole word or a regex. '#' = comment.</summary>
    [JsonPropertyName("WordsFile")]
    public string WordsFile { get; set; } = "";

    /// <summary>Strikes needed before PunishCommand runs for this category.</summary>
    [JsonPropertyName("Threshold")]
    public int Threshold { get; set; } = 3;

    /// <summary>
    /// Console command run when the threshold is reached. Placeholders: {userid} {steamid} {name}.
    /// </summary>
    [JsonPropertyName("PunishCommand")]
    public string PunishCommand { get; set; } = "";
}

/// <summary>The spam check: too many messages in a short window.</summary>
public sealed class SpamConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Max messages allowed within IntervalSeconds before it counts as spam.</summary>
    [JsonPropertyName("MaxMessages")]
    public int MaxMessages { get; set; } = 5;

    /// <summary>Sliding window length in seconds.</summary>
    [JsonPropertyName("IntervalSeconds")]
    public double IntervalSeconds { get; set; } = 3.0;

    /// <summary>Strikes needed before PunishCommand runs.</summary>
    [JsonPropertyName("Threshold")]
    public int Threshold { get; set; } = 3;

    /// <summary>Console command run when the threshold is reached. Placeholders: {userid} {steamid} {name}.</summary>
    [JsonPropertyName("PunishCommand")]
    public string PunishCommand { get; set; } = "css_gag #{userid} 10 \"AutoMod: spam\"";
}
