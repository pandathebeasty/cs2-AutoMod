using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AutoMod;

/// <summary>
/// A compiled list of match rules loaded from a text file. Each non-empty, non-comment line is one
/// rule and is matched case-insensitively against a chat message:
///   - a plain word  -> matched as a WHOLE word (word boundaries), so "ass" does not hit "class"
///   - a regex        -> any line that is wrapped in slashes, e.g.  /discord\.gg\/\w+/
///
/// Lines starting with '#' are comments. Invalid regexes are skipped with a warning.
/// </summary>
public sealed class WordList
{
    private readonly List<Regex> _patterns = new();
    private ILogger? _logger;
    private readonly string _label;

    public WordList(ILogger? logger, string label)
    {
        _logger = logger;
        _label = label;
    }

    /// <summary>Sets/updates the logger (the plugin's Logger isn't ready in the constructor).</summary>
    public void SetLogger(ILogger logger) => _logger = logger;

    public int Count => _patterns.Count;

    /// <summary>Loads (or reloads) the rules from the given file path. Missing file = empty list.</summary>
    public void Load(string path)
    {
        _patterns.Clear();

        if (!File.Exists(path))
        {
            _logger?.LogWarning("[AutoMod] {Label} file not found: {Path} (check disabled until it exists)", _label, path);
            return;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            try
            {
                if (line.Length >= 2 && line.StartsWith('/') && line.EndsWith('/'))
                {
                    // Explicit regex: /pattern/
                    var body = line[1..^1];
                    _patterns.Add(new Regex(body, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant));
                }
                else
                {
                    // Whole-word match, literal text escaped.
                    var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(line)}(?![\p{{L}}\p{{N}}])";
                    _patterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("[AutoMod] {Label}: invalid rule '{Line}' skipped ({Err})", _label, line, ex.Message);
            }
        }

        _logger?.LogInformation("[AutoMod] {Label}: loaded {Count} rule(s) from {Path}", _label, _patterns.Count, path);
    }

    /// <summary>True if the message matches any rule.</summary>
    public bool IsMatch(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        foreach (var p in _patterns)
        {
            try { if (p.IsMatch(message)) return true; }
            catch { /* a pathological pattern must not break chat */ }
        }
        return false;
    }
}
