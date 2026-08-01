using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Synergies
{
    [HarmonyPatch]
    public static class SynergyManager
    {
        // Note: Re-evaluate synergies whenever food is updated.
        [HarmonyPatch(typeof(Player), "UpdateFood")]
        [HarmonyPostfix]
        public static void Player_UpdateFood_Postfix(Player __instance, ref List<Player.Food> ___m_foods)
        {
            try
            {
                if (__instance == null) return;

                SEMan seman = __instance.GetSEMan();
                if (seman == null) return;

                bool synergiesEnabled = ConfigManager.enableSynergies.Value;

                Dictionary<string, int> catCounts = CountCategories(___m_foods);

                bool balanced = synergiesEnabled && catCounts["Meat"] >= 1 && catCounts["Vegetable"] >= 1;
                bool sugarRush = synergiesEnabled && catCounts["Sweet"] >= 3 && catCounts["Drink"] >= 1;
                bool fisherman = synergiesEnabled && catCounts["Fish"] >= 2 && catCounts["Vegetable"] >= 1;
                bool lumberjack = synergiesEnabled && catCounts["Meat"] >= 3 && catCounts["Drink"] >= 1;

                // Each synergy ends the moment ANY of its requirements stops being met, so its
                // remaining time is the soonest of them - see NthLargestRemaining.
                ApplySynergy(seman, SynergyEffects.BalancedDietHash, balanced,
                    Mathf.Min(CategoryRemaining(___m_foods, "Meat", 1), CategoryRemaining(___m_foods, "Vegetable", 1)));
                ApplySynergy(seman, SynergyEffects.SugarRushHash, sugarRush,
                    Mathf.Min(CategoryRemaining(___m_foods, "Sweet", 3), DrinkRemaining(___m_foods, 1)));
                ApplySynergy(seman, SynergyEffects.FishermanHash, fisherman,
                    Mathf.Min(CategoryRemaining(___m_foods, "Fish", 2), CategoryRemaining(___m_foods, "Vegetable", 1)));
                ApplySynergy(seman, SynergyEffects.LumberjackHash, lumberjack,
                    Mathf.Min(CategoryRemaining(___m_foods, "Meat", 3), DrinkRemaining(___m_foods, 1)));

                // Mead & drink buffs ride the same add/remove model - active exactly while a
                // qualifying drink holds the drink slot.
                DrinkBuffs.Evaluate(__instance, seman, ___m_foods);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Dietary Synergies. Reason: {ex}");
            }
        }

        // Synthetic key for "occupies the drink slot". Drink is no longer a diet category, but the
        // synergy recipes are still written in terms of "N Sweets + 1 Drink", so a drink contributes
        // to BOTH its diet category and this pseudo-key. That keeps every existing recipe working
        // while letting an eitr mead also count as Eitr.
        public const string DrinkKey = "Drink";

        public static Dictionary<string, int> CountCategories(List<Player.Food> foods)
        {
            var counts = new Dictionary<string, int>();
            foreach (string cat in CategoryManager.BaseCategories) counts[cat] = 0;
            counts[DrinkKey] = 0;

            if (foods == null) return counts;

            foreach (var food in foods)
            {
                if (food == null || food.m_item == null) continue;

                string prefabName = Patches.PlayerPatches.ResolvePrefabName(food);

                string cat = CategoryManager.GetCategoryForPrefab(prefabName);
                if (counts.ContainsKey(cat)) counts[cat]++;

                if (CategoryManager.IsDrinkPrefab(prefabName)) counts[DrinkKey]++;
            }

            return counts;
        }

        // Adds or removes a synergy effect so it exactly tracks whether the diet qualifies, and
        // publishes how long it has left so the HUD can draw a countdown.
        private static void ApplySynergy(SEMan seman, int nameHash, bool qualified, float remaining)
        {
            if (nameHash == 0) return; // effect not registered yet (ObjectDB not ready)

            bool active = seman.HaveStatusEffect(nameHash);

            if (qualified && !active)
                seman.AddStatusEffect(nameHash, true);
            else if (!qualified && active)
                seman.RemoveStatusEffect(nameHash, false);

            if (qualified) PublishRemaining(seman, nameHash, remaining);
        }

        // ------------------------------------------------------------------
        // Countdown support.
        //
        // These effects are diet-driven, not duration-driven, so they are built with m_ttl = 0
        // ("infinite") and vanilla's StatusEffect.GetIconText() returns "" for anything with no ttl -
        // which is why they showed no timer at all. Their end IS predictable though, so the remaining
        // time is computed each tick and written to the LIVE effect's m_ttl.
        //
        // Written as `remaining + GetDuration()` rather than plain `remaining` on purpose:
        // GetIconText renders `m_ttl - GetDuration()`, so this displays exactly `remaining`, while
        // IsDone() (`m_time > m_ttl`) stays false the whole time. Setting m_ttl = remaining directly
        // would let vanilla expire the effect out from under Fatty's own add/remove lifecycle.
        //
        // Must be the LIVE instance from SEMan, not the ObjectDB template: AddStatusEffect stores a
        // Clone(), so writing the template would change nothing on screen.
        // ------------------------------------------------------------------
        internal static void PublishRemaining(SEMan seman, int nameHash, float remaining)
        {
            if (remaining <= 0f) return;

            StatusEffect live = seman.GetStatusEffect(nameHash);
            if (live == null) return;

            live.m_ttl = remaining + live.GetDuration();
        }

        // Time until this category's count drops below `need`.
        //
        // PlayerPatches.FoodDecay_Prefix/Postfix now makes only the last-eaten entry of each
        // same-prefab group actually decay (older stacks of that food are frozen/parked until it
        // expires), so the need-th largest CURRENT m_time is an approximation, not an exact
        // prediction, whenever a matching category holds multiple stacks of the same food: a parked
        // entry's m_time doesn't start counting down until the stack ahead of it actually expires, so
        // the category empties later than this estimate suggests. Good enough for a cosmetic status-
        // effect countdown (never used for the add/remove decision itself, only PublishRemaining's
        // displayed ttl) - not worth carrying real queue state through here for.
        internal static float CategoryRemaining(List<Player.Food> foods, string category, int need)
        {
            return NthLargestRemaining(foods, prefab => CategoryManager.GetCategoryForPrefab(prefab) == category, need);
        }

        internal static float DrinkRemaining(List<Player.Food> foods, int need)
        {
            return NthLargestRemaining(foods, CategoryManager.IsDrinkPrefab, need);
        }

        internal static float NthLargestRemaining(List<Player.Food> foods, System.Func<string, bool> match, int need)
        {
            if (foods == null || need < 1) return 0f;

            var times = new List<float>();
            foreach (var food in foods)
            {
                if (food == null || food.m_item == null) continue;
                if (!match(Patches.PlayerPatches.ResolvePrefabName(food))) continue;
                times.Add(food.m_time);
            }

            if (times.Count < need) return 0f;

            times.Sort();
            times.Reverse();
            return times[need - 1];
        }
    }
}
