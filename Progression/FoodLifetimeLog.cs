using System.Collections.Generic;
using System.Text;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // Per-character storage for "every distinct food ever eaten, and how many times" - a lifetime
    // feast log, separate from MilestoneStore's per-CATEGORY counts (Meat/Vegetable/...). This is
    // per-PREFAB, so it can answer "how many CookedMeat have I eaten" as well as "how much Meat".
    //
    // Same home and same reasoning as MilestoneStore: Player.m_customData, a public
    // Dictionary<string,string> vanilla already saves/loads and never clears on ResetCharacter, so
    // this needs no save/load patch of its own and survives relogs for free.
    //
    // Serialized as one compact key, same format as MilestoneStore:
    //   Fatty.FoodLifetimeCounts = "CookedMeat=142,Sausages=30,Cloudberry=88"
    // Zero counts are omitted; unparseable entries are skipped rather than throwing.
    // ---------------------------------------------------------------------
    public static class FoodLifetimeLog
    {
        private const string CountsKey = "Fatty.FoodLifetimeCounts";

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

        public static void RecordEaten(Player player, string prefabName)
        {
            if (player == null || string.IsNullOrEmpty(prefabName)) return;

            var counts = Load(player);
            counts.TryGetValue(prefabName, out int before);
            counts[prefabName] = before + 1;
            Save(player, counts);
        }
    }
}
