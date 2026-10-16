using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;

namespace AutoMod;

/// <summary>
/// AutoMod - automatic chat & name moderation for CounterStrikeSharp.
///
/// Checks every say / say_team message for three categories, each independent:
///   - Language    : whole-word / regex rules from a text file
///   - Advertising : whole-word / regex rules from a text file
///   - Spam        : more than MaxMessages within IntervalSeconds
/// Plus NameCheck: a connecting player whose NAME matches a rule is renamed.
///
/// A chat violation BLOCKS the message and adds one strike for that category; at the category's
/// Threshold its console PunishCommand runs and the strikes reset. Players with the exempt flag are
/// never checked. Any text containing a FilterBypass entry is always allowed (protects your own
/// server names, e.g. "go.laleagane.ro").
/// </summary>
public class AutoMod : BasePlugin, IPluginConfig<AutoModConfig>
{
    public override string ModuleName => "AutoMod";
    public override string ModuleAuthor => "panda";
    public override string ModuleDescription => "Automatic chat & name moderation: language, advertising, spam, names.";
    public override string ModuleVersion => "1.1.0";

    public AutoModConfig Config { get; set; } = new();

    private enum Category { Language, Advertising, Spam }

    private readonly WordList _language;
    private readonly WordList _advertising;
    private readonly WordList _names;

    // Per-slot state.
    private readonly List<double>[] _messageTimes = Init(() => new List<double>());
    private readonly int[,] _strikes = new int[64, 3];
    private readonly double[,] _lastStrikeTime = new double[64, 3];

    public AutoMod()
    {
        _language = new WordList(Logger, "Language");
        _advertising = new WordList(Logger, "Advertising");
        _names = new WordList(Logger, "NameCheck");
    }

    private static T[] Init<T>(Func<T> factory)
    {
        var arr = new T[64];
        for (var i = 0; i < 64; i++) arr[i] = factory();
        return arr;
    }

    public void OnConfigParsed(AutoModConfig config)
    {
        Config = config;
        LoadWordFiles();
    }

    public override void Load(bool hotReload)
    {
        AddCommandListener("say", OnSay, HookMode.Pre);
        AddCommandListener("say_team", OnSay, HookMode.Pre);

        // NameCheck: scan the name shortly after a player is put in server (name is set by then).
        RegisterListener<Listeners.OnClientPutInServer>(slot =>
        {
            if (!Config.NameCheck.Enabled) return;
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is { IsValid: true, IsBot: false })
                AddTimer(0.5f, () => CheckName(player));
        });

        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            if (slot < 0 || slot >= 64) return;
            _messageTimes[slot].Clear();
            for (var c = 0; c < 3; c++) { _strikes[slot, c] = 0; _lastStrikeTime[slot, c] = 0; }
        });
    }

    [ConsoleCommand("css_automod_reload", "Reloads AutoMod word files.")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnReload(CCSPlayerController? _, CommandInfo info)
    {
        LoadWordFiles();
        info.ReplyToCommand("[AutoMod] Word files reloaded.");
    }

    private void LoadWordFiles()
    {
        if (!string.IsNullOrEmpty(Config.Language.WordsFile))
            _language.Load(Path.Combine(ModuleDirectory, Config.Language.WordsFile));
        if (!string.IsNullOrEmpty(Config.Advertising.WordsFile))
            _advertising.Load(Path.Combine(ModuleDirectory, Config.Advertising.WordsFile));
        if (!string.IsNullOrEmpty(Config.NameCheck.WordsFile))
            _names.Load(Path.Combine(ModuleDirectory, Config.NameCheck.WordsFile));
    }

    /// <summary>True if the text contains any FilterBypass entry (case-insensitive) - never blocked.</summary>
    private bool IsBypassed(string text)
    {
        foreach (var allow in Config.FilterBypass)
            if (!string.IsNullOrEmpty(allow) && text.Contains(allow, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // --- Chat moderation ----------------------------------------------------------------------

    private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player is not { IsValid: true } || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var slot = player.Slot;
        if (slot < 0 || slot >= 64)
            return HookResult.Continue;

        var message = info.GetArg(1);
        if (string.IsNullOrWhiteSpace(message))
            return HookResult.Continue;

        // Admins with the bypass flag are never checked.
        if (!string.IsNullOrEmpty(Config.FlagBypass) && AdminManager.PlayerHasPermissions(player, Config.FlagBypass))
            return HookResult.Continue;

        // Whitelisted content (e.g. own server names) is always allowed.
        if (IsBypassed(message))
            return HookResult.Continue;

        // Language.
        if (Config.Language.Enabled && _language.IsMatch(message))
        {
            Handle(player, Category.Language, Config.Language.Threshold, Config.Language.PunishCommand);
            return HookResult.Handled; // block the message
        }

        // Advertising.
        if (Config.Advertising.Enabled && _advertising.IsMatch(message))
        {
            Handle(player, Category.Advertising, Config.Advertising.Threshold, Config.Advertising.PunishCommand);
            return HookResult.Handled;
        }

        // Spam.
        if (Config.Spam.Enabled && IsSpam(slot))
        {
            Handle(player, Category.Spam, Config.Spam.Threshold, Config.Spam.PunishCommand);
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    /// <summary>Records this message's timestamp and returns true if the sliding window is over the limit.</summary>
    private bool IsSpam(int slot)
    {
        var now = Server.CurrentTime;
        var times = _messageTimes[slot];
        var cutoff = now - Config.Spam.IntervalSeconds;

        times.RemoveAll(t => t < cutoff);
        times.Add(now);

        return times.Count > Config.Spam.MaxMessages;
    }

    private void Handle(CCSPlayerController player, Category category, int threshold, string punishCommand)
    {
        var slot = player.Slot;
        var c = (int)category;
        var now = Server.CurrentTime;

        // Reset strikes if the category went quiet long enough.
        if (Config.StrikeResetSeconds > 0 &&
            _lastStrikeTime[slot, c] > 0 &&
            now - _lastStrikeTime[slot, c] > Config.StrikeResetSeconds)
        {
            _strikes[slot, c] = 0;
        }

        _strikes[slot, c]++;
        _lastStrikeTime[slot, c] = now;

        player.PrintToChat($" {Localizer["Prefix"]}{Localizer[$"warn.{category}"]} {Localizer["warn.Strike", _strikes[slot, c], Math.Max(1, threshold)]}");

        if (threshold > 0 && _strikes[slot, c] >= threshold)
        {
            RunPunishment(player, punishCommand);
            _strikes[slot, c] = 0;      // reset after punishing
            if (category == Category.Spam) _messageTimes[slot].Clear();
        }
    }

    // --- Name moderation (integrated NameChecker) ---------------------------------------------

    private void CheckName(CCSPlayerController player)
    {
        if (player is not { IsValid: true } || player.IsBot || player.IsHLTV)
            return;
        if (!string.IsNullOrEmpty(Config.FlagBypass) && AdminManager.PlayerHasPermissions(player, Config.FlagBypass))
            return;

        var name = player.PlayerName ?? string.Empty;
        if (IsBypassed(name))
            return;

        if (!_names.IsMatch(name))
            return;

        // Optional command (e.g. a kick/warn) before renaming.
        RunPunishment(player, Config.NameCheck.PunishCommand);

        var replacement = Config.NameCheck.ReplacementName;
        player.PlayerName = replacement;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iszPlayerName");
        Server.NextFrame(() =>
        {
            if (player.IsValid)
                Utilities.SetStateChanged(player, "CCSPlayerController", "m_iszPlayerName");
        });

        // Tell the player why, and optionally announce it to everyone.
        player.PrintToChat($" {Localizer["Prefix"]}{Localizer["name.Renamed"]}");
        if (Config.NameCheck.AnnounceToAll)
            Server.PrintToChatAll($" {Localizer["Prefix"]}{Localizer["name.Announce", name]}");

        Logger.LogInformation("[AutoMod] Renamed '{Old}' -> '{New}' ({SteamId})",
            name, replacement, player.AuthorizedSteamID?.SteamId64.ToString() ?? player.SteamID.ToString());
    }

    private void RunPunishment(CCSPlayerController player, string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return;

        var cmd = template
            .Replace("{userid}", (player.UserId ?? 0).ToString())
            .Replace("{steamid}", player.AuthorizedSteamID?.SteamId64.ToString() ?? player.SteamID.ToString())
            .Replace("{name}", player.PlayerName);

        try
        {
            Server.ExecuteCommand(cmd);
        }
        catch (Exception ex)
        {
            Logger.LogError("[AutoMod] Punishment command failed: {Cmd} ({Err})", cmd, ex.Message);
        }
    }
}
