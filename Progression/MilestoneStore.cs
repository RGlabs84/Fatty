using System.Collections.Generic;
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
    //   Fatty.FoodCounts = "Meat=142,Produce=88,Fish=31"
    // Zero counts are omitted. Unparseable entries are skipped rather than throwing, so a
    // hand-edited or truncated save degrades to a lower count instead of breaking the character.
    // Keys are escaped through CountsCodec - see there for why that matters even here, where they
    // look like fixed internal constants.
    // ---------------------------------------------------------------------
    public static class MilestoneStore
    {
        private const string CountsKey = "Fatty.FoodCounts";

        // Stamp recording which classification generation these counts were built against. Bumped
        // whenever a category is renamed or the classifier starts filing a food differently, which
        // makes the stored per-category counts stale - see RebuildFromLifetimeLog.
        //   1 = implicit (no stamp): counts as written by 1.1.8 and earlier.
        //   2 = "Vegetable" is now "Produce", and berries classify at all rather than as Unknown.
        //   3 = plural/compound keyword matching moved ~85 more foods out of Unknown into a real
        //       category, so anything eaten of those was still filed under Unknown and had to move.
        private const string SchemaKey = "Fatty.FoodCountsSchema";
        private const int CurrentSchema = 3;

        // Parse cache. Load() is not only called on eats: FoodMilestones.GetEitrBonus goes through it
        // from GetTotalFoodValue's postfix, which UpdateFood drives once per second for as long as
        // the game is running - so the raw string was being split, scanned and int-parsed every
        // second to answer a question whose inputs had not changed. Keyed on the exact string that
        // produced it, which makes the cache self-invalidating: anything that edits the counts goes
        // through Save, Save rewrites m_customData, and the next Load sees a string it has not
        // parsed. No manual invalidation to forget, and an external writer to m_customData is picked
        // up for free.
        //
        // A COPY is handed out rather than the cached dictionary itself - callers legitimately mutate
        // what Load returns (RecordEaten increments before saving), and that must not reach back into
        // the cache. Copying ~7 entries is still far cheaper than re-parsing the string.
        private static string _cachedRaw;
        private static Dictionary<string, int> _cachedCounts;

        public static Dictionary<string, int> Load(Player player)
        {
            if (player == null || player.m_customData == null) return new Dictionary<string, int>();

            if (!player.m_customData.TryGetValue(CountsKey, out string raw) || string.IsNullOrEmpty(raw))
                return new Dictionary<string, int>();

            if (!ReferenceEquals(raw, _cachedRaw) && raw != _cachedRaw)
            {
                _cachedCounts = CountsCodec.Parse(raw);
                _cachedRaw = raw;
            }

            return new Dictionary<string, int>(_cachedCounts);
        }

        public static void Save(Player player, Dictionary<string, int> counts)
        {
            if (player == null || player.m_customData == null || counts == null) return;

            string serialized = CountsCodec.Serialize(counts);

            if (serialized == null) player.m_customData.Remove(CountsKey);
            else player.m_customData[CountsKey] = serialized;
        }

        public static int Get(Dictionary<string, int> counts, string category)
        {
            if (counts == null || string.IsNullOrEmpty(category)) return 0;
            return counts.TryGetValue(category, out int v) ? v : 0;
        }

        // ------------------------------------------------------------------
        // Rebuilding the per-category counts from the per-PREFAB lifetime log.
        //
        // Why this has to exist at all: the counts above are keyed by the category a food had AT THE
        // MOMENT IT WAS EATEN, and that key is then frozen forever. Every one of these moves a food
        // between categories without moving the history with it -
        //
        //   * a "Rebuild Food Data" run or a Fatty_FoodData data-version migration re-deriving it,
        //   * a food mod updating its stats so eitr dominance now claims the dish,
        //   * Fatty's own classifier getting better (v6 finally seeing berries),
        //   * the Vegetable -> Produce rename, which strands the whole old key,
        //
        // - so the Feast Ledger would show tiers that no longer match what you have actually eaten,
        // and progress toward a tier could be stranded under a category name nothing reads.
        //
        // FoodLifetimeLog is the authority to rebuild from: it is per-prefab, it is written on every
        // eat with no config gate on it, and it never needed to know about categories at all. Re-
        // filing each prefab through the CURRENT classification reproduces the counts exactly as
        // they would be if the present rules had always applied.
        //
        // Deliberately one-shot per schema bump, not every launch. A food whose mod has been
        // uninstalled resolves to Unknown, so an unconditional rebuild would quietly demote those
        // eats out of their real category every time the game started; stamping it means that can
        // happen at most once, on a launch the player was told about.
        // ------------------------------------------------------------------
        public static bool NeedsRebuild(Player player)
        {
            if (player == null || player.m_customData == null) return false;

            if (!player.m_customData.TryGetValue(SchemaKey, out string raw) || !int.TryParse(raw, out int schema))
                schema = 1;

            return schema < CurrentSchema;
        }

        public static void RebuildFromLifetimeLog(Player player)
        {
            if (player == null || player.m_customData == null) return;

            var lifetime = FoodLifetimeLog.Load(player);
            var rebuilt = new Dictionary<string, int>();

            foreach (var kvp in lifetime)
            {
                string category = CategoryManager.CanonicalCategory(
                    CategoryManager.GetCategoryForPrefab(kvp.Key));

                rebuilt.TryGetValue(category, out int running);
                rebuilt[category] = running + kvp.Value;
            }

            // Never let a rebuild LOSE total food eaten. The lifetime log only started being written
            // in 1.1.0, and it is skipped for anything eaten before that, so a long-lived character
            // can hold more in its category counts than the log can account for. Fatness is the
            // total-eaten axis, so silently dropping the difference would visibly take max health
            // away from exactly the players who had eaten the most. The shortfall is parked in
            // Unknown, which counts toward Fatness but earns no category tier - the honest place for
            // "we know you ate these, we no longer know what they were".
            var previous = Load(player);
            int previousTotal = 0, rebuiltTotal = 0;
            foreach (var kvp in previous) previousTotal += kvp.Value;
            foreach (var kvp in rebuilt) rebuiltTotal += kvp.Value;

            if (previousTotal > rebuiltTotal)
            {
                rebuilt.TryGetValue(CategoryManager.UnknownCategory, out int unaccounted);
                rebuilt[CategoryManager.UnknownCategory] = unaccounted + (previousTotal - rebuiltTotal);
            }

            Save(player, rebuilt);
            player.m_customData[SchemaKey] = CurrentSchema.ToString();

            FattyPlugin.Instance.Log.LogInfo(
                $"[Fatty] Rebuilt lifetime food categories from the feast log: {lifetime.Count} distinct food(s), " +
                $"{rebuiltTotal} eaten re-filed under the current categories" +
                (previousTotal > rebuiltTotal ? $" (+{previousTotal - rebuiltTotal} pre-log carried as Unknown)" : "") + ".");
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
