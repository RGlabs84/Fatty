using System.Collections.Generic;
using System.Text;
using Fatty.Configuration;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // Per-character storage for lifetime food counts.
    //
    // Lives in Player.m_customData, which is a public Dictionary<string,string> that vanilla writes
    // unconditionally in Player.Save and reads back for save version >= 26 (current saves are v29+).
    // That makes it the right home for this: it travels with the CHARACTER rather than the world,
    // it needs no save/load patch of our own, and it is deliberately NOT cleared by
    // Player.ResetCharacter() - which wipes skills, recipes, uniques and known texts but leaves
    // custom data alone - so "lifetime food eaten" genuinely survives.
    //
    // Serialized as one compact key to stay friendly to anything else sharing that dictionary:
    //   Fatty.FoodCounts = "Meat=142,Vegetable=88,Fish=31"
    // Zero counts are omitted. Unparseable entries are skipped rather than throwing, so a
    // hand-edited or truncated save degrades to a lower count instead of breaking the character.
    // ---------------------------------------------------------------------
    public static class MilestoneStore
    {
        private const string CountsKey = "Fatty.FoodCounts";

        public static Dictionary<string, int> Load(Player player)
        {
            var counts = new Dictionary<string, int>();
            if (player == null || player.m_customData == null) return counts;

            if (!player.m_customData.TryGetValue(CountsKey, out string raw) || string.IsNullOrEmpty(raw))
                return counts;

            foreach (string pair in raw.Split(','))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0 || eq == pair.Length - 1) continue;

                string key = pair.Substring(0, eq);
                if (!int.TryParse(pair.Substring(eq + 1), out int value)) continue;
                if (value <= 0) continue;

                counts[key] = value;
            }

            return counts;
        }

        public static void Save(Player player, Dictionary<string, int> counts)
        {
            if (player == null || player.m_customData == null || counts == null) return;

            var sb = new StringBuilder();
            foreach (var kvp in counts)
            {
                if (kvp.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kvp.Key).Append('=').Append(kvp.Value);
            }

            if (sb.Length == 0) player.m_customData.Remove(CountsKey);
            else player.m_customData[CountsKey] = sb.ToString();
        }

        public static int Get(Dictionary<string, int> counts, string category)
        {
            if (counts == null || string.IsNullOrEmpty(category)) return 0;
            return counts.TryGetValue(category, out int v) ? v : 0;
        }

        // Categories that can actually earn milestones. Unknown is tracked but excluded: it is the
        // "we couldn't identify this" bucket, so paying HP for it would reward unidentifiable food -
        // and keeping the count means a later re-categorization credits it retroactively.
        public static bool EarnsMilestones(string category)
        {
            return !string.IsNullOrEmpty(category) && category != CategoryManager.UnknownCategory;
        }
    }
}
