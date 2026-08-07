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
        //
        // Priority.Last deliberately: PlayerPatches.FoodDecay_Postfix (Priority.First) undoes
        // vanilla's decrement on every frozen stack, and reading m_foods BEFORE that restore lands
        // sees values that are one second low for exactly one frame per second. That is what made
        // every countdown here blip down by a second and snap straight back.
        [HarmonyPatch(typeof(Player), "UpdateFood")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Player_UpdateFood_Postfix(Player __instance, ref List<Player.Food> ___m_foods)
        {
            try
            {
                if (__instance == null) return;

                SEMan seman = __instance.GetSEMan();
                if (seman == null) return;

                RefreshProjection(___m_foods);

                bool synergiesEnabled = ConfigManager.enableSynergies.Value;

                Dictionary<string, int> catCounts = CountCategories(___m_foods);

                bool balanced = synergiesEnabled && catCounts["Meat"] >= 1 && catCounts["Produce"] >= 1;
                bool sugarRush = synergiesEnabled && catCounts["Sweet"] >= 3 && catCounts["Drink"] >= 1;
                bool fisherman = synergiesEnabled && catCounts["Fish"] >= 2 && catCounts["Produce"] >= 1;
                bool lumberjack = synergiesEnabled && catCounts["Meat"] >= 3 && catCounts["Drink"] >= 1;

                // Each synergy ends the moment ANY of its requirements stops being met, so its
                // remaining time is the soonest of them - see NthLargestRemaining.
                ApplyTimedEffect(seman, SynergyEffects.BalancedDietHash, balanced,
                    Mathf.Min(CategoryRemaining(___m_foods, "Meat", 1), CategoryRemaining(___m_foods, "Produce", 1)));
                ApplyTimedEffect(seman, SynergyEffects.SugarRushHash, sugarRush,
                    Mathf.Min(CategoryRemaining(___m_foods, "Sweet", 3), DrinkRemaining(___m_foods, 1)));
                ApplyTimedEffect(seman, SynergyEffects.FishermanHash, fisherman,
                    Mathf.Min(CategoryRemaining(___m_foods, "Fish", 2), CategoryRemaining(___m_foods, "Produce", 1)));
                ApplyTimedEffect(seman, SynergyEffects.LumberjackHash, lumberjack,
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

        // ------------------------------------------------------------------
        // Shared lifecycle for every food-driven Fatty effect - the four synergies here plus the
        // drink draughts and Drunk in DrinkBuffs. Adds/removes the effect so it exactly tracks
        // whether the diet qualifies, publishes how long it has left so the HUD can draw a
        // countdown, and enforces the configured duration ceiling.
        //
        // The ceiling ("Max Effect Duration"): Fatty's own duration multiplier and banked stacks
        // make a single activation last far longer than anything vanilla grants, so an activation is
        // cut off once it has been up for that long even though the diet still qualifies. Re-adding
        // it on the very next frame would make the ceiling meaningless, so a spent effect is locked
        // out until its recipe actually stops being met - that lapse is what re-arms it.
        //
        // A plain static set is enough for the lockout: Player.UpdateStats, UpdateFood's only
        // per-frame caller, runs solely for m_localPlayer (confirmed by decompile).
        // ------------------------------------------------------------------
        private static readonly HashSet<int> _spent = new HashSet<int>();

        internal static void ApplyTimedEffect(SEMan seman, int nameHash, bool qualified, float remaining)
        {
            if (nameHash == 0) return; // effect not registered yet (ObjectDB not ready)

            if (!qualified)
            {
                if (seman.HaveStatusEffect(nameHash)) seman.RemoveStatusEffect(nameHash, false);
                _spent.Remove(nameHash); // recipe lapsed - the ceiling re-arms
                return;
            }

            // 0 = no ceiling. GetDuration() is the live effect's own m_time, i.e. seconds since it
            // was granted, so no separate start-time bookkeeping is needed.
            float cap = Mathf.Max(0f, ConfigManager.maxEffectDuration.Value) * 60f;
            StatusEffect live = seman.GetStatusEffect(nameHash);

            if (live != null && cap > 0f && live.GetDuration() >= cap)
            {
                seman.RemoveStatusEffect(nameHash, false);
                _spent.Add(nameHash);
                return;
            }

            if (live == null)
            {
                if (_spent.Contains(nameHash)) return; // ceiling already spent this qualification
                seman.AddStatusEffect(nameHash, true);
                live = seman.GetStatusEffect(nameHash);
                if (live == null) return;
            }

            if (cap > 0f) remaining = Mathf.Min(remaining, cap - live.GetDuration());
            PublishRemaining(live, remaining);
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
        // Floored rather than skipped when it reaches zero. Leaving m_ttl unwritten would strand the
        // last value published, and once m_time overtook it IsDone() (`m_time > m_ttl`) would expire
        // the effect out from under Fatty's own add/remove lifecycle - which at the duration ceiling
        // is worse than cosmetic: the effect would come back on the very next frame with a fresh
        // GetDuration() of 0, handing it a brand new full-length window instead of the lockout.
        private const float MinPublishedTtl = 0.5f;

        private static void PublishRemaining(StatusEffect live, float remaining)
        {
            if (live == null) return;

            live.m_ttl = Mathf.Max(remaining, MinPublishedTtl) + live.GetDuration();
        }

        // ------------------------------------------------------------------
        // Projected expiry, one entry per stomach slot.
        //
        // Sequential stack decay (PlayerPatches.FoodDecay_Prefix/Postfix) means only the LAST entry
        // of a same-prefab group actually ticks down - every older stack of that prefab is frozen
        // until the one ahead of it expires. So a frozen entry's raw m_time is not "time left" at
        // all: it is a constant that never moves, which is exactly why a countdown built straight on
        // it sat still instead of counting down.
        //
        // An entry's REAL expiry is the sum of m_time from itself through the end of its own prefab
        // group, since every older stack has to wait out everything newer than it. Walking the list
        // backwards accumulates precisely that in one pass. Reused static buffers because this runs
        // every frame from UpdateFood, on the hot path.
        // ------------------------------------------------------------------
        private static readonly List<float> _projected = new List<float>();
        private static readonly Dictionary<string, float> _projectionAcc = new Dictionary<string, float>();
        private static readonly List<float> _sortBuffer = new List<float>();

        internal static void RefreshProjection(List<Player.Food> foods)
        {
            _projected.Clear();
            _projectionAcc.Clear();
            if (foods == null) return;

            for (int i = 0; i < foods.Count; i++) _projected.Add(0f);

            for (int i = foods.Count - 1; i >= 0; i--)
            {
                Player.Food food = foods[i];
                if (food == null || food.m_item == null) continue;

                string prefab = Patches.PlayerPatches.ResolvePrefabName(food);
                _projectionAcc.TryGetValue(prefab, out float queued);
                queued += food.m_time;
                _projectionAcc[prefab] = queued;
                _projected[i] = queued;
            }
        }

        // Time until this category's count drops below `need`.
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
            if (_projected.Count != foods.Count) RefreshProjection(foods);

            _sortBuffer.Clear();
            for (int i = 0; i < foods.Count; i++)
            {
                Player.Food food = foods[i];
                if (food == null || food.m_item == null) continue;
                if (!match(Patches.PlayerPatches.ResolvePrefabName(food))) continue;
                _sortBuffer.Add(_projected[i]);
            }

            if (_sortBuffer.Count < need) return 0f;

            _sortBuffer.Sort();
            _sortBuffer.Reverse();
            return _sortBuffer[need - 1];
        }
    }
}
