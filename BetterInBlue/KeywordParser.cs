using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lumina.Text.ReadOnly;
using static BetterInBlue.NotebookFilterFlags;

namespace BetterInBlue;

public record KeywordGroup(NotebookCategory Category, NotebookFilterFlags[] Flags, ReadOnlySeString[] FlagNames);

public record KeywordMatch(
    NotebookCategory Category,
    NotebookFilterFlags Flag,
    uint Color,
    int Index,
    int Length,
    bool Partial = false
) {
    public int End => Index + Length;
};

public static class KeywordParser {
    private static readonly Regex PairRegex = new(@"(?<key>\w+):(?<value>\w*)", RegexOptions.Compiled);
    public static readonly uint DefaultColor = 28;
    public static readonly uint DefaultErrorColor = 17;

    public static readonly Dictionary<NotebookCategory, KeywordGroup> Groups = new();

    static KeywordParser() {
        var groups = typeof(NotebookFilterFlags)
                     .GetFields()
                     .Select(field => new {
                         Flag = (NotebookFilterFlags) field.GetValue(None)!,
                         Attribute = field.GetCustomAttributes(false)
                                          .OfType<NotebookFlagAttribute>()
                                          .FirstOrDefault()
                     })
                     .Where(item => item.Attribute != null)
                     .GroupBy(item => item.Attribute?.Category);

        foreach (var group in groups) {
            if (group.Key is { } k) {
                var g = group.ToList();
                var flags = g.Select(x => x.Flag).ToArray();
                var strings = g.Select(x => x.Attribute?.Name ?? x.Flag.GetDisplay()).ToArray();
                Groups[k] = new KeywordGroup(k, flags, strings);
            }
        }
    }

    private static KeywordMatch? FindMatch(string category, string value, int index, int length) {
        foreach (var (_, g) in Groups) {
            var v = g.Category.GetAddonLogId().ToString();
            if (!string.Equals(g.Category.ToString(), category, StringComparison.InvariantCultureIgnoreCase) &&
                !string.Equals(v, category, StringComparison.InvariantCultureIgnoreCase)) continue;
            // Services.Log.Verbose($" - found {v} || {g.Category}");
            foreach (var flag in g.Flags) {
                var rowText = flag.GetDisplay().ToString();
                if (string.Equals(rowText, value, StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(flag.GetName(), value, StringComparison.InvariantCultureIgnoreCase) ||
                    string.Equals(flag.ToString(), value, StringComparison.InvariantCultureIgnoreCase)) {
                    var color = flag.GetColor();
                    return new KeywordMatch(g.Category, flag, color == 0 ? DefaultColor : color, index, length);
                }
            }

            return new KeywordMatch(g.Category, 0, DefaultErrorColor, index, length, true);
        }

        return null;
    }

    public static List<KeywordMatch> TryParse(ReadOnlySeString rStr, out bool success) {
        var str = rStr.ToString();
        var pairs = PairRegex.Matches(str);
        List<KeywordMatch> matches = [];
        success = true;

        if (str.Count(c => c == ':') != pairs.Count) {
            success = false;
            Services.Log.Warning("Invalid search string");
        }

        foreach (Match match in pairs) {
            var found = FindMatch(match.Groups["key"].Value,
                                  match.Groups["value"].Value,
                                  match.Index,
                                  match.Length);
            if (found is null) continue;
            matches.Add(found);
        }

        return matches;
    }
}
