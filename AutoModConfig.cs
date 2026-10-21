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
    /// When true, every detected violation (language / advertising / spam / name) is written to a
    /// daily log file in the plugin's "logs" folder: who it was, the strike count, and the punish.
    /// </summary>
    [JsonPropertyName("LogToFile")]
    public bool LogToFile { get; set; } = true;

    /// <summary>
    /// .NET date/time format for the {time} placeholder shown in file logs and Discord messages.
    /// Example: "dd-MM-yyyy HH:mm:ss" (day-month-year) or "yyyy-MM-dd HH:mm:ss".
    /// </summary>
    [JsonPropertyName("DateFormat")]
    public string DateFormat { get; set; } = "dd-MM-yyyy HH:mm:ss";

    /// <summary>
    /// .NET date format used for the daily log FILE NAME (&lt;this&gt;.log). Do NOT use '/' - it is a
    /// path separator and would break the path on Linux. Default: "dd-MM-yyyy".
    /// </summary>
    [JsonPropertyName("LogFileDateFormat")]
    public string LogFileDateFormat { get; set; } = "dd-MM-yyyy";

    /// <summary>
    /// Whitelist of substrings that are NEVER blocked, even if a rule would match them. Checked
    /// case-insensitively against the whole message (and player name for NameCheck) before any rule:
    /// if the text contains one of these, that content is allowed through. Use it to protect your
    /// own server/community names, e.g. "go.laleagane.ro", "gollg".
    /// </summary>
    [JsonPropertyName("FilterBypass")]
    public List<string> FilterBypass { get; set; } = ["example.com", "yourserver.net"];

    /// <summary>
    /// When true, chat/name checks also run against a "normalized" copy of the text that defeats
    /// common bypasses: repeated letters are collapsed (puuulaaa -> pula) and separators between
    /// letters are stripped (p.u.l.a / p u l a / p-u-l-a -> pula). This makes the plain-word rules
    /// catch spaced-out / stretched spellings without needing a regex for each word. Default: true.
    /// </summary>
    [JsonPropertyName("NormalizeText")]
    public bool NormalizeText { get; set; } = true;

    /// <summary>
    /// How many times, and how far apart (seconds), NameCheck re-scans a connecting player's name.
    /// A player's name is not always set 0.5s after connect (reconnects, busy servers), so one check
    /// can miss it. The scan repeats until the name is non-empty or the attempts run out. Default:
    /// 5 attempts, 1s apart (plus an initial 0.5s delay).
    /// </summary>
    [JsonPropertyName("NameCheckAttempts")]
    public int NameCheckAttempts { get; set; } = 5;

    [JsonPropertyName("NameCheckRetrySeconds")]
    public double NameCheckRetrySeconds { get; set; } = 1.0;

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

    [JsonPropertyName("Discord")]
    public DiscordConfig Discord { get; set; } = new();
}

/// <summary>
/// Discord webhook logging. Every violation can also be posted to a Discord channel.
/// Two modes: "text" (a single configurable line) or "embed" (a rich card that also shows the
/// player's Steam avatar - resolved from the public profile XML, no Steam API key needed).
///
/// Placeholders available in Format / EmbedTitle / EmbedDescription:
///   {category} {name} {steamid} {userid} {strikes} {threshold} {punish} {message} {time}
/// {message} is the blocked chat line. It is EMPTY for NameCheck (a rename, not a message); in that
/// case any "{message}" you put in a template collapses to nothing. If a template does not contain
/// {message}, the blocked chat line is appended automatically on its own line (chat checks only).
/// </summary>
public sealed class DiscordConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = false;

    /// <summary>The Discord webhook URL. Leave empty to disable.</summary>
    [JsonPropertyName("WebhookUrl")]
    public string WebhookUrl { get; set; } = "";

    /// <summary>"text" (default) or "embed".</summary>
    [JsonPropertyName("Mode")]
    public string Mode { get; set; } = "text";

    /// <summary>Webhook display name (optional).</summary>
    [JsonPropertyName("Username")]
    public string Username { get; set; } = "AutoMod";

    /// <summary>Avatar for the webhook user itself (optional, not the player).</summary>
    [JsonPropertyName("AvatarUrl")]
    public string AvatarUrl { get; set; } = "";

    // ---- text mode ----
    /// <summary>Message template for "text" mode. Supports the placeholders listed above.</summary>
    [JsonPropertyName("Format")]
    public string Format { get; set; } = "**{category}** | `{name}` ({steamid}) | strikes {strikes}/{threshold} | {punish}";

    // ---- embed mode ----
    /// <summary>Embed title (embed mode).</summary>
    [JsonPropertyName("EmbedTitle")]
    public string EmbedTitle { get; set; } = "AutoMod - {category}";

    /// <summary>Embed description (embed mode).</summary>
    [JsonPropertyName("EmbedDescription")]
    public string EmbedDescription { get; set; } = "**{name}** (`{steamid}`)\nStrikes: **{strikes}/{threshold}**\n{punish}";

    /// <summary>Embed side-colour as a hex string (e.g. "#e23b3b").</summary>
    [JsonPropertyName("EmbedColor")]
    public string EmbedColor { get; set; } = "#e23b3b";

    /// <summary>Show the player's Steam avatar as the embed thumbnail (embed mode).</summary>
    [JsonPropertyName("ShowPlayerAvatar")]
    public bool ShowPlayerAvatar { get; set; } = true;
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
