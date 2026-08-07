using System.Collections.Generic;
using System.Text;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // Shared "key=count,key=count" codec for the two lifetime stores that live in
    // Player.m_customData (MilestoneStore and FoodLifetimeLog).
    //
    // Escaping exists because the keys are not as safe as they look. FoodLifetimeLog's keys are raw
    // PREFAB NAMES straight from another mod's ObjectDB entries, and nothing in Valheim stops one
    // containing a comma or an equals sign. One such food would not just lose its own entry: a stray
    // ',' splits a record in half and a stray '=' moves the delimiter, so the corruption spreads
    // across the rest of the string and silently truncates a character's whole lifetime history on
    // the next parse. MilestoneStore's keys are internal constants today, but since 1.1.9 they are
    // also rebuilt from GetCategoryForPrefab - i.e. from Fatty_FoodData.json, which is a file the
    // player is actively invited to hand-edit - so it is not immune either.
    //
    // Percent-encoding of exactly three characters ('%' first, so it can't double-encode). Chosen
    // over a different delimiter so that OLD, UNESCAPED DATA STILL READS IDENTICALLY: any name
    // containing none of the three - which is every prefab that ever round-tripped correctly - has
    // the same encoded and decoded form, so existing saves need no migration. Names that did contain
    // one were already corrupt, so there is nothing to preserve there.
    // ---------------------------------------------------------------------
    internal static class CountsCodec
    {
        internal static string Encode(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (key.IndexOf('%') < 0 && key.IndexOf(',') < 0 && key.IndexOf('=') < 0) return key;

            var sb = new StringBuilder(key.Length + 6);
            foreach (char c in key)
            {
                switch (c)
                {
                    case '%': sb.Append("%25"); break;
                    case ',': sb.Append("%2C"); break;
                    case '=': sb.Append("%3D"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        internal static string Decode(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOf('%') < 0) return key;

            var sb = new StringBuilder(key.Length);
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == '%' && i + 2 < key.Length)
                {
                    string hex = key.Substring(i + 1, 2).ToUpperInvariant();
                    if (hex == "25") { sb.Append('%'); i += 2; continue; }
                    if (hex == "2C") { sb.Append(','); i += 2; continue; }
                    if (hex == "3D") { sb.Append('='); i += 2; continue; }
                }
                sb.Append(key[i]);
            }
            return sb.ToString();
        }

        // Parses "key=count,key=count". Unparseable pairs and non-positive counts are skipped rather
        // than throwing, so a truncated or hand-edited save degrades to a lower count instead of
        // breaking the character outright.
        internal static Dictionary<string, int> Parse(string raw)
        {
            var counts = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(raw)) return counts;

            foreach (string pair in raw.Split(','))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0 || eq == pair.Length - 1) continue;

                if (!int.TryParse(pair.Substring(eq + 1), out int value)) continue;
                if (value <= 0) continue;

                counts[Decode(pair.Substring(0, eq))] = value;
            }

            return counts;
        }

        // Returns null when nothing is worth storing, so callers remove the key rather than parking
        // an empty string in m_customData.
        internal static string Serialize(Dictionary<string, int> counts)
        {
            if (counts == null) return null;

            var sb = new StringBuilder();
            foreach (var kvp in counts)
            {
                if (kvp.Value <= 0 || string.IsNullOrEmpty(kvp.Key)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(Encode(kvp.Key)).Append('=').Append(kvp.Value);
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
