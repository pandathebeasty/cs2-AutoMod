# AutoMod

Automatic chat moderation for CounterStrikeSharp. Every `say` / `say_team` message is checked
against three independent rules — **Language**, **Advertising** and **Spam**. A violation **blocks
the message** and adds one strike for that category; when a category reaches its strike threshold,
its console punishment command runs and that category's strikes reset.

## Checks

- **Language** — rules from a text file (`language.txt` by default)
- **Advertising** — rules from a text file (`advertising.txt` by default)
- **Spam** — more than `MaxMessages` within `IntervalSeconds`
- **NameCheck** — a connecting player whose **name** matches a rule (`names.txt` by default) is
  renamed to a configured placeholder

Each chat category has its **own threshold** and its **own punishment command**.

## FilterBypass (whitelist)

`FilterBypass` is a list of substrings that are **never** blocked, even if a rule would match them.
It is checked against the whole message (and player name for NameCheck) before any rule. Use it to
protect your own server/community names so your plugins/announcements aren't caught by the ad
filter:

```json
"FilterBypass": [ "example.com", "yourserver.net" ]
```

## Word files

One rule per line, `#` starts a comment. Each rule is matched case-insensitively:

- **plain word** → matched as a **whole word** (so `ass` does not hit `class`)
- **`/regex/`** → wrap the line in slashes for a regular expression (best for links, invites, IPs)

```
# language.txt
idiot
/f+u+c+k+/

# advertising.txt
/discord\.gg\/\S+/
/https?:\/\/\S+/
/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/
```

Reload the files at runtime from the server console: `css_automod_reload`.

## Config

`addons/counterstrikesharp/configs/plugins/AutoMod/AutoMod.json`

| Key | Default | Description |
| --- | --- | --- |
| `ExemptFlag` | `@css/ban` | Players with this flag/permission are never checked. Empty = nobody exempt. |
| `FilterBypass` | your server names | Substrings never blocked (whitelist). |
| `StrikeResetSeconds` | `300` | Seconds of quiet after which a category's strikes reset. 0 = never. |
| `NameCheck.Enabled` | `true` | Rename players whose name matches a rule. |
| `NameCheck.WordsFile` | `names.txt` | Rule file tested against player names. |
| `NameCheck.ReplacementName` | `BAD NAME \| CHANGE IT` | What a flagged player is renamed to. |
| `NameCheck.PunishCommand` | `` (empty) | Optional command run before the rename. |
| `Language.Enabled` / `Advertising.Enabled` / `Spam.Enabled` | `true` | Toggle each check. |
| `Language.WordsFile` / `Advertising.WordsFile` | `language.txt` / `advertising.txt` | Rule file (in the plugin folder). |
| `*.Threshold` | 3 / 2 / 3 | Strikes before the category's command runs. |
| `*.PunishCommand` | see below | Console command run at the threshold. |
| `Spam.MaxMessages` | `5` | Max messages per window before it counts as spam. |
| `Spam.IntervalSeconds` | `3` | Sliding window length. |

### Punishment commands

Each category has its own command, run from the server console when its threshold is reached.
Placeholders: `{userid}`, `{steamid}`, `{name}`.

```json
"Language":    { "PunishCommand": "css_gag #{userid} 5 \"AutoMod: bad language\"" },
"Advertising": { "PunishCommand": "css_gag #{userid} 15 \"AutoMod: advertising\"" },
"Spam":        { "PunishCommand": "css_gag #{userid} 10 \"AutoMod: spam\"" }
```

Use whatever admin plugin's commands you run (CS2-SimpleAdmin, Iks_Admin, AdminPlus, ...). The
command is passed verbatim to the server console with the placeholders substituted.

## Requirements

CounterStrikeSharp (.NET 10).
