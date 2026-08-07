using System.Collections.Generic;

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
    //
    // Keys here are raw prefab names from other mods' ObjectDB entries, so they go through
    // CountsCodec's escaping - a single prefab containing ',' or '=' would otherwise corrupt every
    // record after it, not just its own. See CountsCodec for the full reasoning.
    //
    // This is also the authoritative record the per-CATEGORY counts are rebuilt from, since it is
    // written on every eat with no config gate and never needed to know about categories at all -
    // see MilestoneStore.RebuildFromLifetimeLog.
    // ---------------------------------------------------------------------
    public static class FoodLifetimeLog
    {
        private const string CountsKey = "Fatty.FoodLifetimeCounts";

        public static Dictionary<string, int> Load(Player player)
        {
            if (player == null || player.m_customData == null) return new Dictionary<string, int>();

            if (!player.m_customData.TryGetValue(CountsKey, out string raw) || string.IsNullOrEmpty(raw))
                return new Dictionary<string, int>();

            return CountsCodec.Parse(raw);
        }

        public static void Save(Player player, Dictionary<string, int> counts)
        {
            if (player == null || player.m_customData == null || counts == null) return;

            string serialized = CountsCodec.Serialize(counts);

            if (serialized == null) player.m_customData.Remove(CountsKey);
            else player.m_customData[CountsKey] = serialized;
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
