using System.Collections.Generic;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // "Getting fat": eating a lot of a food category, over a long time, permanently raises the
    // player's BASE health - small awards, well spaced out, per category, so a broad diet is what
    // pays rather than spamming whichever food is cheapest.
    //
    // Applied by writing Player.m_baseHP. That field is `public float m_baseHP = 25f` and is only
    // ever READ in the vanilla assembly (by GetTotalFoodValue and GetBaseFoodHP) - nothing writes
    // it - which gives two things for free:
    //   * It flows through Fatty's own GetTotalFoodValue postfix untouched: both the "fatty" and
    //     "vanilla" totals seed from m_baseHP, so it cancels out of the delta and vanilla's own
    //     `hp = m_baseHP` carries the bonus into max health.
    //   * Hud.UpdateFood sizes the base food bar from GetBaseFoodHP(), so the white base segment
    //     visibly grows as base health increases. No HUD work needed.
    //
    // m_baseHP is a prefab-serialized field, so it resets to 25 on every new Player instance -
    // hence the re-apply on Player.OnSpawned.
    // ---------------------------------------------------------------------
    public static class FoodMilestones
    {
        // Vanilla's starting base HP, and the floor we always build from. Captured per-player on
        // spawn rather than hardcoded, so another mod raising the base is respected.
        private static float _pristineBaseHp = 25f;
        private static bool _capturedBase;

        public static void OnSpawned(Player player)
        {
            if (player == null) return;

            // Capture before we ever write, so we can never compound our own bonus across respawns.
            if (!_capturedBase)
            {
                _pristineBaseHp = player.m_baseHP;
                _capturedBase = true;
            }

            Apply(player, announce: false);
        }

        // Called once per successful eat. Returns nothing - any award is announced inline.
        public static void RecordEaten(Player player, string category)
        {
            if (player == null || !ConfigManager.enableMilestones.Value) return;
            if (string.IsNullOrEmpty(category)) return;

            var counts = MilestoneStore.Load(player);
            int before = MilestoneStore.Get(counts, category);
            counts[category] = before + 1;
            MilestoneStore.Save(player, counts);

            if (!MilestoneStore.EarnsMilestones(category)) return;

            // Only bother re-applying when this bite actually crossed a threshold.
            int tiersBefore = TiersEarned(before);
            int tiersAfter = TiersEarned(before + 1);
            if (tiersAfter <= tiersBefore) return;

            Apply(player, announce: true, justEarnedCategory: category, tier: tiersAfter);
        }

        // Recomputes total bonus HP from the stored counts and writes m_baseHP.
        public static void Apply(Player player, bool announce, string justEarnedCategory = null, int tier = 0)
        {
            if (player == null) return;
            if (!_capturedBase)
            {
                _pristineBaseHp = player.m_baseHP;
                _capturedBase = true;
            }

            float bonus = ConfigManager.enableMilestones.Value ? TotalBonus(player) : 0f;
            player.m_baseHP = _pristineBaseHp + bonus;

            if (announce && !string.IsNullOrEmpty(justEarnedCategory))
            {
                float awarded = HpForTier(tier);
                player.Message(MessageHud.MessageType.Center,
                    $"You're getting fat! {justEarnedCategory} {tier} - +{awarded:0.#} max health");

                if (ConfigManager.debugMode.Value)
                    FattyPlugin.Instance.Log.LogInfo(
                        $"[Fatty] Milestone: {justEarnedCategory} tier {tier}, +{awarded} HP (total bonus {bonus}, base HP now {player.m_baseHP}).");
            }
        }

        public static float TotalBonus(Player player)
        {
            var counts = MilestoneStore.Load(player);
            float total = 0f;

            foreach (string category in CategoryManager.BaseCategories)
            {
                if (!MilestoneStore.EarnsMilestones(category)) continue;

                int tiers = TiersEarned(MilestoneStore.Get(counts, category));
                for (int t = 1; t <= tiers; t++) total += HpForTier(t);
            }

            return Mathf.Min(total, Mathf.Max(0f, ConfigManager.milestoneTotalCap.Value));
        }

        // How many thresholds a given eaten-count has passed.
        //
        // The three counts are independent config entries, so nothing stops them being configured
        // out of order (or all equal). Sorting them here means a bad ordering just makes the tiers
        // land in a different place instead of, say, awarding tier 3 before tier 1 - and combined
        // with the config's minimum of 1, no tier can ever be earned at zero food eaten.
        internal static int TiersEarned(int eaten)
        {
            int t1 = ConfigManager.milestoneTier1Count.Value;
            int t2 = ConfigManager.milestoneTier2Count.Value;
            int t3 = ConfigManager.milestoneTier3Count.Value;

            SortAscending(ref t1, ref t2);
            SortAscending(ref t2, ref t3);
            SortAscending(ref t1, ref t2);

            if (eaten >= t3) return 3;
            if (eaten >= t2) return 2;
            if (eaten >= t1) return 1;
            return 0;
        }

        private static void SortAscending(ref int a, ref int b)
        {
            if (a <= b) return;
            int tmp = a;
            a = b;
            b = tmp;
        }

        internal static float HpForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1Hp.Value;
                case 2: return ConfigManager.milestoneTier2Hp.Value;
                case 3: return ConfigManager.milestoneTier3Hp.Value;
                default: return 0f;
            }
        }

        // Diagnostics for the debug config / support requests.
        public static string Describe(Player player)
        {
            if (player == null) return "no player";

            var counts = MilestoneStore.Load(player);
            var parts = new List<string>();
            foreach (string category in CategoryManager.BaseCategories)
            {
                int eaten = MilestoneStore.Get(counts, category);
                if (eaten <= 0) continue;
                parts.Add($"{category}={eaten}({TiersEarned(eaten)} tier(s))");
            }

            return parts.Count == 0
                ? "nothing eaten yet"
                : $"{string.Join(", ", parts)} -> +{TotalBonus(player):0.#} HP";
        }
    }
}
