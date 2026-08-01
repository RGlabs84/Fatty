using System.Collections.Generic;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // "Getting fat": eating a lot of food, over a long time, permanently raises a stat - small
    // awards, well spaced out, so a broad diet is what pays rather than spamming whichever food is
    // cheapest. Six independent axes, all driven by the same per-category lifetime counts.
    //
    // Five of the six ALTERNATE between two reward types across their 3 tiers - tier 1 and tier 3
    // grant a flat pool bonus, tier 2 grants a %-flavoured alternate instead of a 3rd flat step:
    //
    //   Meat      -> Carry Weight (t1/t3, player.m_maxCarryWeight) <-> Sword+Axe skill (t2)
    //   Sweet     -> Stamina      (t1/t3, player.m_baseStamina)    <-> Stamina regen%  (t2)
    //   Eitr      -> Eitr         (t1/t3, GetTotalFoodValue delta) <-> Eitr regen%     (t2)
    //   Fish      -> Swim Speed   (t1/t3, MilestonePerks SE_Stats) <-> Swim stamina use reduction (t2)
    //   Fatness   -> Max Health   (t1/t3, player.m_baseHP)         <-> Health regen%   (t2)
    //   Vegetable -> All-round regen on ALL 3 tiers (MilestonePerks SE_Stats) - the one exception,
    //                since it never owned a single flat pool to alternate a "flat" reward against.
    //
    // Fatness is NOT per-category: it's total food eaten across EVERY category, including food
    // Fatty couldn't identify, since it rewards eating a lot in general rather than any one diet.
    //
    // m_baseHP/m_baseStamina/m_maxCarryWeight are all "vanilla only ever reads this" fields (see
    // GetTotalFoodValue/GetBaseFoodHP and GetMaxCarryWeight in the decompile), which gives the same
    // two things for free on all three that the original HP-only version already relied on:
    //   * They flow through Fatty's own GetTotalFoodValue postfix / GetMaxCarryWeight untouched -
    //     both the "fatty" and "vanilla" totals seed from the same base field, so it cancels out of
    //     the delta and vanilla's own math carries the bonus forward.
    //   * They're prefab-serialized and reset on every new Player instance, hence OnSpawned re-applies
    //     every axis (and re-syncs MilestonePerks' status effects, which live on a fresh SEMan too).
    // ---------------------------------------------------------------------
    public static class FoodMilestones
    {
        private const string CategoryMeat = "Meat";
        private const string CategorySweet = "Sweet";
        private const string CategoryEitr = "Eitr";
        private const string CategoryFish = "Fish";
        private const string CategoryVegetable = "Vegetable";

        // Vanilla's starting values, and the floor everything builds from. Captured per-player on
        // spawn rather than hardcoded, so another mod raising a base value is respected.
        private static float _pristineBaseHp = 25f;
        private static float _pristineBaseStamina = 75f;
        private static float _pristineMaxCarryWeight = 300f;
        private static bool _capturedBase;

        public static void OnSpawned(Player player)
        {
            if (player == null) return;
            CaptureBaseIfNeeded(player);
            Apply(player);
        }

        // Called once per successful eat. Records the bite, then recomputes and writes/syncs every
        // axis, then announces whichever axis (if any) just crossed a new tier.
        public static void RecordEaten(Player player, string category)
        {
            if (player == null || !ConfigManager.enableMilestones.Value) return;
            if (string.IsNullOrEmpty(category)) return;

            var counts = MilestoneStore.Load(player);
            int before = MilestoneStore.Get(counts, category);
            int totalBefore = SumAll(counts);

            counts[category] = before + 1;
            MilestoneStore.Save(player, counts);

            Apply(player);

            if (MilestoneStore.EarnsMilestones(category))
            {
                int tiersBefore = TiersEarned(before);
                int tiersAfter = TiersEarned(before + 1);
                if (tiersAfter > tiersBefore)
                    AnnounceCategoryTier(player, category, tiersAfter);
            }

            int fatnessBefore = FatnessTiersEarned(totalBefore);
            int fatnessAfter = FatnessTiersEarned(totalBefore + 1);
            if (fatnessAfter > fatnessBefore)
                AnnounceFatnessTier(player, fatnessAfter);
        }

        // Recomputes every milestone-driven stat from the stored counts and (re)writes/syncs it.
        // Idempotent - safe to call on every eat AND on every respawn, which it is: flat fields reset
        // on every new Player instance, and MilestonePerks' status effects live on a fresh SEMan each
        // time too, so both need a full re-apply here rather than an incremental update.
        public static void Apply(Player player)
        {
            if (player == null) return;
            CaptureBaseIfNeeded(player);

            bool enabled = ConfigManager.enableMilestones.Value;
            var counts = MilestoneStore.Load(player);

            player.m_baseHP = _pristineBaseHp + (enabled ? FatnessBonus(counts) : 0f);
            player.m_baseStamina = _pristineBaseStamina + (enabled ? StaminaBonus(counts) : 0f);
            player.m_maxCarryWeight = _pristineMaxCarryWeight + (enabled ? CarryWeightBonus(counts) : 0f);

            int meatTiers = enabled ? TiersEarned(MilestoneStore.Get(counts, CategoryMeat)) : 0;
            int sweetTiers = enabled ? TiersEarned(MilestoneStore.Get(counts, CategorySweet)) : 0;
            int eitrTiers = enabled ? TiersEarned(MilestoneStore.Get(counts, CategoryEitr)) : 0;
            int fishTiers = enabled ? TiersEarned(MilestoneStore.Get(counts, CategoryFish)) : 0;
            int vegTiers = enabled ? TiersEarned(MilestoneStore.Get(counts, CategoryVegetable)) : 0;
            int fatnessTiers = enabled ? FatnessTiersEarned(SumAll(counts)) : 0;

            // Tier 2 is the "alternate" reward slot for 5 of the 6 axes - gated purely on
            // tier >= 2, not summed like the flat tiers, since only one status effect per axis
            // grants it (see MilestonePerks' header comment for why tiers use separate effects).
            MilestonePerks.Sync(player, fishTiers, vegTiers,
                meatSkillActive: meatTiers >= 2,
                staminaRegenActive: sweetTiers >= 2,
                eitrRegenActive: eitrTiers >= 2,
                hpRegenActive: fatnessTiers >= 2);
        }

        // Permanent max-eitr bonus - no base-eitr field exists in vanilla to write, so this is read
        // by PlayerPatches.GetTotalFoodValue_Postfix and added directly into the eitr delta, the same
        // way DrinkBuffs.GetMaxHealthBonus injects its (temporary) max-HP bonus.
        public static float GetEitrBonus(Player player)
        {
            if (player == null || !ConfigManager.enableMilestones.Value) return 0f;
            return EitrBonus(MilestoneStore.Load(player));
        }

        private static void CaptureBaseIfNeeded(Player player)
        {
            if (_capturedBase) return;
            _pristineBaseHp = player.m_baseHP;
            _pristineBaseStamina = player.m_baseStamina;
            _pristineMaxCarryWeight = player.m_maxCarryWeight;
            _capturedBase = true;
        }

        private static void AnnounceCategoryTier(Player player, string category, int tier)
        {
            string text = DescribeTierAward(category, tier);

            player.Message(MessageHud.MessageType.Center, $"You're getting fat! {category} {tier} - {text}");

            if (ConfigManager.debugMode.Value)
                FattyPlugin.Instance.Log.LogInfo($"[Fatty] Milestone: {category} tier {tier} -> {text}.");
        }

        private static void AnnounceFatnessTier(Player player, int tier)
        {
            string text = DescribeTierAward("Fatness", tier);

            player.Message(MessageHud.MessageType.Center, $"You're getting fat! Fatness {tier} - {text}");

            if (ConfigManager.debugMode.Value)
                FattyPlugin.Instance.Log.LogInfo($"[Fatty] Milestone: Fatness tier {tier} -> {text}.");
        }

        // Tier 2 is every category's "alternate" reward slot except Vegetable (which grants
        // all-round regen on every tier - no flat counterpart to alternate against). Internal (not
        // private) so FeastLedgerGui can build a per-tier breakdown for the Getting Fat panel.
        internal static string DescribeTierAward(string category, int tier)
        {
            switch (category)
            {
                case CategoryMeat:
                    return tier == 2
                        ? $"+{ConfigManager.milestoneTier2MeatSkill.Value:0.#} Sword & Axe skill"
                        : $"+{CarryWeightForTier(tier):0.#} carry weight";
                case CategorySweet:
                    return tier == 2
                        ? $"+{ConfigManager.milestoneTier2StaminaRegen.Value * 100f:0.#}% stamina regen"
                        : $"+{StaminaForTier(tier):0.#} stamina";
                case CategoryEitr:
                    return tier == 2
                        ? $"+{ConfigManager.milestoneTier2EitrRegen.Value * 100f:0.#}% eitr regen"
                        : $"+{EitrForTier(tier):0.#} eitr";
                case CategoryFish:
                    return tier == 2
                        ? $"-{ConfigManager.milestoneTier2FishSwimStamina.Value * 100f:0.#}% swim stamina cost"
                        : $"+{SwimSpeedForTier(tier) * 100f:0.#}% swim speed";
                case CategoryVegetable:
                    return $"+{(RegenForTier(tier) - 1f) * 100f:0.#}% regen";
                case "Fatness": // not a real diet category - the total-food-eaten axis, see SumAll
                    return tier == 2
                        ? $"+{ConfigManager.milestoneTier2HpRegen.Value * 100f:0.#}% health regen"
                        : $"+{HpForTier(tier):0.#} max health";
                default:
                    return "";
            }
        }

        // ---- per-axis totals -------------------------------------------------
        //
        // Each of these sums only tiers 1 and 3 - tier 2 is the alternate (regen%/skill) reward for
        // that axis, applied separately via MilestonePerks.Sync, not folded into this flat total.

        public static float CarryWeightBonus(Dictionary<string, int> counts)
        {
            int tiers = TiersEarned(MilestoneStore.Get(counts, CategoryMeat));
            float total = (tiers >= 1 ? CarryWeightForTier(1) : 0f) + (tiers >= 3 ? CarryWeightForTier(3) : 0f);
            return Mathf.Min(total, Mathf.Max(0f, ConfigManager.milestoneCarryWeightCap.Value));
        }

        public static float StaminaBonus(Dictionary<string, int> counts)
        {
            int tiers = TiersEarned(MilestoneStore.Get(counts, CategorySweet));
            float total = (tiers >= 1 ? StaminaForTier(1) : 0f) + (tiers >= 3 ? StaminaForTier(3) : 0f);
            return Mathf.Min(total, Mathf.Max(0f, ConfigManager.milestoneStaminaCap.Value));
        }

        public static float EitrBonus(Dictionary<string, int> counts)
        {
            int tiers = TiersEarned(MilestoneStore.Get(counts, CategoryEitr));
            float total = (tiers >= 1 ? EitrForTier(1) : 0f) + (tiers >= 3 ? EitrForTier(3) : 0f);
            return Mathf.Min(total, Mathf.Max(0f, ConfigManager.milestoneEitrCap.Value));
        }

        public static float FatnessBonus(Dictionary<string, int> counts)
        {
            int tiers = FatnessTiersEarned(SumAll(counts));
            float total = (tiers >= 1 ? HpForTier(1) : 0f) + (tiers >= 3 ? HpForTier(3) : 0f);
            return Mathf.Min(total, Mathf.Max(0f, ConfigManager.milestoneTotalCap.Value));
        }

        // Total food eaten across EVERY tracked category, including "Unknown" - Fatness is about raw
        // consumption, not diet quality, unlike the per-category axes above.
        private static int SumAll(Dictionary<string, int> counts)
        {
            int total = 0;
            foreach (var kvp in counts) total += kvp.Value;
            return total;
        }

        // ---- tier thresholds ---------------------------------------------------

        // How many thresholds a given eaten-count has passed. Shared by all 5 real categories - the
        // three counts are independent config entries, so nothing stops them being configured out of
        // order (or all equal). Sorting here means a bad ordering just makes the tiers land in a
        // different place instead of awarding tier 3 before tier 1, and combined with the config's
        // minimum of 1, no tier can ever be earned at zero food eaten.
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

        // Same shape as TiersEarned but against Fatness's own, separate (and larger) thresholds.
        internal static int FatnessTiersEarned(int totalEaten)
        {
            int t1 = ConfigManager.fatnessTier1Count.Value;
            int t2 = ConfigManager.fatnessTier2Count.Value;
            int t3 = ConfigManager.fatnessTier3Count.Value;

            SortAscending(ref t1, ref t2);
            SortAscending(ref t2, ref t3);
            SortAscending(ref t1, ref t2);

            if (totalEaten >= t3) return 3;
            if (totalEaten >= t2) return 2;
            if (totalEaten >= t1) return 1;
            return 0;
        }

        private static void SortAscending(ref int a, ref int b)
        {
            if (a <= b) return;
            int tmp = a;
            a = b;
            b = tmp;
        }

        // ---- per-tier award amounts ---------------------------------------------
        //
        // Tier 2 deliberately has no case in any of these five - it's the alternate (regen%/skill)
        // reward slot, read directly off its own config entry in DescribeTierAward/MilestonePerks
        // rather than summed here as a flat amount.

        internal static float HpForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1Hp.Value;
                case 3: return ConfigManager.milestoneTier3Hp.Value;
                default: return 0f;
            }
        }

        internal static float CarryWeightForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1CarryWeight.Value;
                case 3: return ConfigManager.milestoneTier3CarryWeight.Value;
                default: return 0f;
            }
        }

        internal static float StaminaForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1Stamina.Value;
                case 3: return ConfigManager.milestoneTier3Stamina.Value;
                default: return 0f;
            }
        }

        internal static float EitrForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1Eitr.Value;
                case 3: return ConfigManager.milestoneTier3Eitr.Value;
                default: return 0f;
            }
        }

        internal static float SwimSpeedForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1SwimSpeed.Value;
                case 3: return ConfigManager.milestoneTier3SwimSpeed.Value;
                default: return 0f;
            }
        }

        // Multiplier (1.0 = unchanged), NOT additive - matches MilestonePerks/SE_Stats' own field.
        internal static float RegenForTier(int tier)
        {
            switch (tier)
            {
                case 1: return ConfigManager.milestoneTier1Regen.Value;
                case 2: return ConfigManager.milestoneTier2Regen.Value;
                case 3: return ConfigManager.milestoneTier3Regen.Value;
                default: return 1f;
            }
        }

        // ---- diagnostics ---------------------------------------------------------

        // Diagnostics for the debug config / support requests.
        public static string Describe(Player player)
        {
            if (player == null) return "no player";

            var counts = MilestoneStore.Load(player);
            var parts = new List<string>();

            AppendIfEaten(parts, counts, CategoryMeat, "carry weight", CarryWeightBonus(counts));
            AppendIfEaten(parts, counts, CategorySweet, "stamina", StaminaBonus(counts));
            AppendIfEaten(parts, counts, CategoryEitr, "eitr", EitrBonus(counts));

            int fishEaten = MilestoneStore.Get(counts, CategoryFish);
            if (fishEaten > 0)
            {
                int fishTiers = TiersEarned(fishEaten);
                string alt = fishTiers >= 2 ? ", swim stamina reduced" : "";
                parts.Add($"{CategoryFish}={fishEaten}({fishTiers} tier(s)) -> swim speed{alt}");
            }

            int vegEaten = MilestoneStore.Get(counts, CategoryVegetable);
            if (vegEaten > 0)
                parts.Add($"{CategoryVegetable}={vegEaten}({TiersEarned(vegEaten)} tier(s)) -> regen");

            int total = SumAll(counts);
            if (total > 0)
            {
                int fatnessTiers = FatnessTiersEarned(total);
                string alt = fatnessTiers >= 2 ? ", health regen boosted" : "";
                parts.Add($"Fatness={total}({fatnessTiers} tier(s)) -> +{FatnessBonus(counts):0.#} HP{alt}");
            }

            return parts.Count == 0 ? "nothing eaten yet" : string.Join(", ", parts);
        }

        private static void AppendIfEaten(List<string> parts, Dictionary<string, int> counts, string category, string statName, float bonus)
        {
            int eaten = MilestoneStore.Get(counts, category);
            if (eaten <= 0) return;
            int tiers = TiersEarned(eaten);
            string alt = tiers >= 2 ? $", tier2 alt: {DescribeTierAward(category, 2)}" : "";
            parts.Add($"{category}={eaten}({tiers} tier(s)) -> +{bonus:0.#} {statName}{alt}");
        }
    }
}
