using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Patches
{
    [HarmonyPatch]
    public static class PlayerPatches
    {
        private const int MaxFoodSlots = 4; // solid-food slots (drinks get a dedicated 5th slot)
        private const int MaxDrinkSlots = 1;

        // Vanilla's own stomach size (Player.m_maxFoods). Used as the yardstick for how much
        // bigger Fatty made the stat pools, which is what the regen scaling has to undo.
        private const int VanillaFoodSlots = 3;

        // ------------------------------------------------------------------
        // Shared slot/stack rules used by both CanEat and EatFood so they never disagree.
        // ------------------------------------------------------------------
        public static bool CanAddFood(List<Player.Food> foods, ItemDrop.ItemData item, out string blockMessage)
        {
            blockMessage = null;
            if (item == null || item.m_shared == null) return false;

            string prefabName = ResolvePrefabName(item);
            // Slot allocation is its own axis now: "Drink" is no longer a diet category, so an eitr
            // mead can count as Eitr for synergies while still taking the dedicated drink slot.
            bool isDrink = CategoryManager.IsDrinkPrefab(prefabName);

            // A food TYPE occupies one slot; eating the same food again just stacks inside that slot.
            int sameFood = 0;
            var foodTypes = new HashSet<string>();
            var drinkTypes = new HashSet<string>();
            if (foods != null)
            {
                foreach (Player.Food f in foods)
                {
                    if (f == null || f.m_item == null || f.m_item.m_shared == null) continue;

                    // MUST go through the Food overload, not f.m_item. Player.Load restores each
                    // food by assigning the ObjectDB PREFAB ASSET's m_itemData, and a prefab asset
                    // never runs ItemDrop.Awake - so m_dropPrefab is null on every entry after a
                    // relog, while the item in your inventory always has it set. Reading f.m_item
                    // here compared "$item_carrotsoup" against "CarrotSoup", which made a restored
                    // drink count as a solid food (4 food slots full with 3 foods held) and reset
                    // stacking across every relog.
                    string n = ResolvePrefabName(f);
                    if (n == prefabName) sameFood++;

                    if (CategoryManager.IsDrinkPrefab(n)) drinkTypes.Add(n);
                    else foodTypes.Add(n);
                }
            }

            int maxStacks = Mathf.Max(1, ConfigManager.maxFoodStacks.Value);
            if (sameFood >= maxStacks)
            {
                blockMessage = "You cannot eat any more of this right now!";
                LogBlock(prefabName, isDrink, sameFood, maxStacks, foodTypes.Count, drinkTypes.Count, blockMessage);
                return false;
            }

            // Only a brand-new food/drink type needs a free slot; further stacks of an
            // existing type are always allowed (up to the stack limit checked above).
            bool isNewType = sameFood == 0;
            if (isNewType)
            {
                if (isDrink && drinkTypes.Count >= MaxDrinkSlots)
                {
                    blockMessage = "Drink slot is full!";
                    LogBlock(prefabName, isDrink, sameFood, maxStacks, foodTypes.Count, drinkTypes.Count, blockMessage);
                    return false;
                }

                if (!isDrink && foodTypes.Count >= MaxFoodSlots)
                {
                    blockMessage = "Food slots are full!";
                    LogBlock(prefabName, isDrink, sameFood, maxStacks, foodTypes.Count, drinkTypes.Count, blockMessage);
                    return false;
                }
            }

            return true;
        }

        // Block reasons were previously silent (no log at all - only the in-game HUD message showed
        // it), which made a stacking/slot cap impossible to diagnose remotely. Logging every block
        // with the full counts that produced it turns "won't let me eat/drink X" into a one-line answer.
        //
        // Throttled per (item + reason): a single eat attempt runs these rules several times over,
        // and the UI re-asks whenever it decides whether to grey the item out, so an unthrottled
        // line buries the rest of the log in ten identical copies of the same answer.
        private static readonly Dictionary<string, float> _lastBlockLog = new Dictionary<string, float>();
        private const float BlockLogCooldown = 5f;

        private static void LogBlock(string prefabName, bool isDrink, int sameFood, int maxStacks, int foodTypeCount, int drinkTypeCount, string reason)
        {
            string key = prefabName + "|" + reason;
            float now = Time.unscaledTime;

            if (_lastBlockLog.TryGetValue(key, out float last) && now - last < BlockLogCooldown) return;
            _lastBlockLog[key] = now;

            FattyPlugin.Instance.Log.LogInfo($"[Fatty] Blocked eating '{prefabName}' (category={(isDrink ? "Drink" : "Food")}, sameFood={sameFood}/{maxStacks}, foodTypes={foodTypeCount}/{MaxFoodSlots}, drinkTypes={drinkTypeCount}/{MaxDrinkSlots}): {reason}");
        }

        // ------------------------------------------------------------------
        // The ONE place a prefab name is derived. Everything keyed by prefab name - slot rules,
        // stacking, categories, drink detection, synergies, milestones - must agree on this string,
        // and four separate inlined copies of "m_dropPrefab ?? something else" is exactly how they
        // stopped agreeing.
        //
        // m_dropPrefab is only ever assigned by ItemDrop.Awake and Inventory.AddItem, so it is
        // reliably set for anything in your inventory and reliably NULL for anything Player.Load
        // restored into the stomach. The fallbacks below cover that, and deliberately never return
        // a "$item_..." localisation token if a real prefab name can be recovered - that token is
        // not a valid ObjectDB key, so persisting one makes vanilla drop the food on next load.
        // ------------------------------------------------------------------
        public static string ResolvePrefabName(ItemDrop.ItemData item, string savedName = null)
        {
            if (item != null && item.m_dropPrefab != null && !string.IsNullOrEmpty(item.m_dropPrefab.name))
                return item.m_dropPrefab.name;

            // Player.Food.m_name is written as the prefab name by both vanilla and Fatty, so this
            // is the normal path for a stomach restored from a save.
            if (!string.IsNullOrEmpty(savedName) && !IsLocalizationToken(savedName))
                return savedName;

            string sharedName = item?.m_shared?.m_name;
            if (!string.IsNullOrEmpty(sharedName))
            {
                // Last resort: the scanner already recorded m_shared.m_name alongside the prefab
                // name for every food in the game, so use that index rather than a token nothing
                // else can look up.
                string resolved = CategoryManager.GetPrefabNameForInGameName(sharedName);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            return sharedName ?? savedName ?? string.Empty;
        }

        public static string ResolvePrefabName(Player.Food food)
        {
            if (food == null) return string.Empty;
            return ResolvePrefabName(food.m_item, food.m_name);
        }

        private static bool IsLocalizationToken(string s)
        {
            return s.Length > 0 && s[0] == '$';
        }

        private static float GetStackMultiplier(int stackIndex)
        {
            // stackIndex is 0-based: 0 = first (full value), 1 = 2nd stack, etc.
            if (stackIndex <= 0) return 1f;
            if (stackIndex == 1) return ConfigManager.stackDiminishingReturnStep1.Value;
            if (stackIndex == 2) return ConfigManager.stackDiminishingReturnStep2.Value;
            return ConfigManager.stackDiminishingReturnStep3.Value;
        }

        // Records one more entry of `prefabName` in `seen` and returns that entry's position within
        // its own stack. Both the max-stat totals and the food-regen tick weight stacks with the
        // same ladder, so they share this bookkeeping to guarantee they can never drift apart.
        private static int NextStackIndex(Dictionary<string, int> seen, string prefabName)
        {
            int stackIndex = seen.TryGetValue(prefabName, out int c) ? c : 0;
            seen[prefabName] = stackIndex + 1;
            return stackIndex;
        }

        // One stack entry's full math: stack index, diminishing multiplier, decay factor and the
        // HP/Stamina/Eitr it currently contributes. GetTotalFoodValue_Postfix sums these to build
        // the player's totals; the HUD breakdown panel reads the same struct per-member instead of
        // re-deriving it, so the two can never disagree about what a single food is worth.
        internal struct FoodContribution
        {
            public Player.Food Food;
            public string Name;
            public int StackIndex;
            public float Multiplier;
            public float DecayFactor;
            public float BurnTime;
            public float Hp;
            public float Stamina;
            public float Eitr;
        }

        // Caller must already know food/food.m_item/food.m_item.m_shared are non-null - every
        // existing call site filters that before touching the stack math, so this doesn't re-check.
        internal static FoodContribution ComputeContribution(Player.Food food, Dictionary<string, int> seen,
            bool noDecay, float durationMultiplier, float hpBenefit, float staminaBenefit, float eitrBenefit)
        {
            var shared = food.m_item.m_shared;
            string name = ResolvePrefabName(food);
            int stackIndex = NextStackIndex(seen, name);
            float multiplier = GetStackMultiplier(stackIndex);
            float burnTime = GetBurnTime(food.m_item, durationMultiplier);

            float decayFactor = 1f;
            if (!noDecay)
                decayFactor = burnTime > 0f ? Mathf.Pow(Mathf.Clamp01(food.m_time / burnTime), 0.3f) : 0f;

            return new FoodContribution
            {
                Food = food,
                Name = name,
                StackIndex = stackIndex,
                Multiplier = multiplier,
                DecayFactor = decayFactor,
                BurnTime = burnTime,
                Hp = shared.m_food * decayFactor * multiplier * hpBenefit,
                Stamina = shared.m_foodStamina * decayFactor * multiplier * staminaBenefit,
                Eitr = shared.m_foodEitr * decayFactor * multiplier * eitrBenefit
            };
        }

        // ------------------------------------------------------------------
        // CanEat: replace vanilla's 3-slot / no-restack gate with Fatty's own rules.
        // This runs inside CanConsumeItem BEFORE EatFood, so without it the extra
        // slots and immediate stacking would be blocked before EatFood ever runs.
        // ------------------------------------------------------------------
        [HarmonyPatch(typeof(Player), nameof(Player.CanEat))]
        [HarmonyPrefix]
        public static bool CanEat_Prefix(Player __instance, ItemDrop.ItemData item, bool showMessages, ref bool __result)
        {
            try
            {
                if (item == null || item.m_shared == null) return true; // let vanilla handle oddities

                bool ok = CanAddFood(__instance.GetFoods(), item, out string msg);
                if (!ok && showMessages && !string.IsNullOrEmpty(msg))
                    __instance.Message(MessageHud.MessageType.Center, msg);

                __result = ok;
                return false; // skip vanilla CanEat
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Eat Rules (CanEat). Reason: {ex}");
                return true; // fall back to vanilla
            }
        }

        // ------------------------------------------------------------------
        // EatFood: fully replaced. Adds the new food as its own stack without ever
        // clearing m_foods (the old approach caused max-stat clamping / health loss).
        // ------------------------------------------------------------------
        [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
        [HarmonyPrefix]
        public static bool EatFood_Prefix(Player __instance, ItemDrop.ItemData item, ref List<Player.Food> ___m_foods, ref bool __result)
        {
            try
            {
                if (item == null || item.m_shared == null) return true; // let vanilla handle it

                if (!CanAddFood(___m_foods, item, out string blockMessage))
                {
                    if (!string.IsNullOrEmpty(blockMessage))
                        __instance.Message(MessageHud.MessageType.Center, blockMessage);
                    __result = false;
                    return false;
                }

                // Add a fresh stack. UpdateFood (and our postfix) will compute the real max-stat values.
                float durationMultiplier = Compat.FoodDuration.GetMultiplier();
                float burnTime = GetBurnTime(item, durationMultiplier);

                // Nothing to sit in the stomach for. Let the item's own consume effect happen (that
                // already ran) but don't add a zero-length entry vanilla would delete next tick.
                if (burnTime <= 0f)
                {
                    if (ConfigManager.debugMode.Value)
                        FattyPlugin.Instance.Log.LogInfo(
                            $"[Fatty Debug] '{ResolvePrefabName(item)}' has no food burn time and no status-effect duration; not taking a slot.");

                    __result = true;
                    return false;
                }

                Player.Food newFood = new Player.Food
                {
                    // Always the real prefab name: vanilla SAVES this field and Player.Load looks it
                    // up in ObjectDB, so writing a "$item_..." token here would make the food vanish
                    // on next login.
                    m_name = ResolvePrefabName(item),
                    m_item = item,
                    m_time = burnTime,
                    m_health = item.m_shared.m_food,
                    m_stamina = item.m_shared.m_foodStamina,
                    m_eitr = item.m_shared.m_foodEitr
                };
                ___m_foods.Add(newFood);

                // Vanilla-style "what you gained" message.
                string text = string.Empty;
                if (item.m_shared.m_food > 0f) text += " +" + item.m_shared.m_food + " $item_food_health ";
                if (item.m_shared.m_foodStamina > 0f) text += " +" + item.m_shared.m_foodStamina + " $item_food_stamina ";
                if (item.m_shared.m_foodEitr > 0f) text += " +" + item.m_shared.m_foodEitr + " $item_food_eitr ";
                if (!string.IsNullOrWhiteSpace(text))
                    __instance.Message(MessageHud.MessageType.Center, text);

                // Lifetime food counters -> permanent base HP. Recorded here because Fatty fully
                // owns EatFood, so this is the single point where an eat is known to have succeeded.
                Progression.FoodMilestones.RecordEaten(__instance, CategoryManager.GetCategoryForPrefab(newFood.m_name));

                // Per-item lifetime tally for the feast log HUD panel - separate from the
                // per-category counts above.
                Progression.FoodLifetimeLog.RecordEaten(__instance, newFood.m_name);

                // Force an immediate refresh so stats apply this frame.
                __instance.UpdateFood(0f, forceUpdate: true);

                if (ConfigManager.debugMode.Value)
                    FattyPlugin.Instance.Log.LogInfo($"[Fatty Debug] Ate '{newFood.m_name}'. Total slots used: {___m_foods.Count}");

                __result = true;
                return false; // skip vanilla EatFood
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Eat Handling (EatFood). Reason: {ex}");
                return true; // fall back to vanilla
            }
        }

        // ------------------------------------------------------------------
        // GetTotalFoodValue: where vanilla sums each food into the player's max HP / Stamina / Eitr.
        // UpdateFood calls it every second and then SetMax*(..., flashBar: true), and each of those
        // flashes the bar whenever the new max is strictly GREATER than the old. So anything that
        // makes the total creep up tick after tick reads to the player as constant flashing.
        //
        // Fatty cannot simply assign the out-params: it is not the only mod feeding this method.
        // Vanilla has m_baseHP/m_baseStamina but NO m_baseEitr - it hardcodes `eitr = 0f` - so the
        // only way any mod can grant base Eitr is through these out-params, and several do
        // (AllTheBases transpiles that `0f` into its "Base Eitr" config and postfixes hp).
        // Assigning wiped them, which is what users hit as "Fatty isn't working for Eitr".
        //
        // 1.0.3 fixed that by adding a DELTA instead, reconstructing what vanilla's own loop
        // contributed as `sum of food.m_health`. That reconstruction is an ASSUMPTION about the
        // method body, and blacks7ar's FoodDurationMultiplier breaks it: with its "Food Degrade"
        // Off (its shipped default) it TRANSPILES this method so it never reads food.m_health at
        // all, summing `m_shared.m_food * benefit` instead. The incoming total then held steady
        // while our assumed baseline decayed, so the delta grew every single second - a flash per
        // tick, and max stats drifting to roughly double. That is the reported "back to flashing".
        //
        // So: stop guessing the baseline, remove it. The prefix empties the stomach for the
        // duration of the original call, so whatever the original returns - through every other
        // mod's transpiler, untouched - is purely the BASE contribution with no food term to
        // disagree about. The postfix puts the stomach back and adds Fatty's own food totals.
        // Fatty becomes the single, deterministic author of per-food stat math while every other
        // mod keeps its base intact.
        //
        // The one thing that costs us is FoodDurationMultiplier's benefit percentages, which lived
        // in the loop we just bypassed - so Compat.FoodDuration reproduces them directly.
        // ------------------------------------------------------------------

        // Contents of m_foods while the original method runs. A plain static is safe here: this
        // is Unity's main thread, and the prefix/original/postfix sequence is synchronous.
        private static readonly List<Player.Food> _stashedFoods = new List<Player.Food>();
        private static bool _foodsStashed;

        // Deliberately Priority.Last so every OTHER prefix has already seen the real stomach; the
        // window where m_foods is empty is then just the original method body.
        //
        // Note this clears the list in place rather than swapping the field: Player.m_foods is
        // declared readonly, and other mods may be holding a reference to that exact list.
        [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static void GetTotalFoodValue_Prefix(List<Player.Food> ___m_foods)
        {
            try
            {
                if (_foodsStashed || ___m_foods == null || ___m_foods.Count == 0) return;

                _stashedFoods.Clear();
                _stashedFoods.AddRange(___m_foods);
                ___m_foods.Clear();
                _foodsStashed = true;
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Totals (stash). Reason: {ex}");
            }
        }

        // Priority.First so the stomach is restored immediately after the original returns, before
        // any other mod's postfix can observe it empty.
        [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void GetTotalFoodValue_Postfix(Player __instance, ref float hp, ref float stamina, ref float eitr,
            List<Player.Food> ___m_foods)
        {
            RestoreFoods(___m_foods);

            try
            {
                if (___m_foods == null) return;

                // Everything the original produced with an empty stomach IS the base: vanilla's
                // m_baseHP/m_baseStamina plus any other mod's base contribution. That is also
                // exactly the right starting point for the regen reference pool.
                float refStamina = stamina, refEitr = eitr;

                bool noDecay = ConfigManager.noFoodDecay.Value;
                // Reflection-backed lookups - hoisted out of the loop instead of once per food.
                float durationMultiplier = Compat.FoodDuration.GetMultiplier();
                float hpBenefit = Compat.FoodDuration.GetHealthBenefit();
                float staminaBenefit = Compat.FoodDuration.GetStaminaBenefit();
                float eitrBenefit = Compat.FoodDuration.GetEitrBenefit();

                // Two totals over a single walk:
                //   fatty* - Fatty's rules (extra slots, stacking diminishing returns, no-decay)
                //   ref*   - what VANILLA's slot rules would have granted (its 3 slots, first stack
                //            only), used purely to scale regen back to parity. Health has no
                //            flat-rate regen to compensate, so it needs no reference total.
                float fattyHp = 0f, fattyStamina = 0f, fattyEitr = 0f;

                Dictionary<string, int> seen = new Dictionary<string, int>();
                int distinctTypes = 0;

                foreach (Player.Food food in ___m_foods)
                {
                    if (food == null || food.m_item == null || food.m_item.m_shared == null) continue;

                    var shared = food.m_item.m_shared;
                    FoodContribution c = ComputeContribution(food, seen, noDecay, durationMultiplier,
                        hpBenefit, staminaBenefit, eitrBenefit);
                    if (c.StackIndex == 0) distinctTypes++;

                    fattyHp += c.Hp;
                    fattyStamina += c.Stamina;
                    fattyEitr += c.Eitr;

                    // Drink buffs grant max HP that lasts as long as the drink. SE_Stats has no
                    // m_addMaxHealth field, so max stats can only be reached through this method -
                    // and because it is recomputed every tick, the bonus expires with the drink on
                    // its own. Uses the same stack ladder as everything else.
                    fattyHp += Synergies.DrinkBuffs.GetMaxHealthBonus(c.Name, c.Multiplier);

                    // Vanilla-equivalent pool: its 3 slots, one stack each, no diminishing returns.
                    // Uses the SAME decay factor as above on purpose, so the no-decay setting cancels
                    // out of the ratio and it isolates only the extra size Fatty adds.
                    if (c.StackIndex == 0 && distinctTypes <= VanillaFoodSlots)
                    {
                        refStamina += shared.m_foodStamina * c.DecayFactor;
                        refEitr += shared.m_foodEitr * c.DecayFactor;
                    }
                }

                hp += fattyHp;
                stamina += fattyStamina;
                eitr += fattyEitr;

                // Eitr milestone bonus: permanent, not stomach-content-dependent, so it's added once
                // here rather than per-stack inside the loop above. Vanilla has no base-eitr field to
                // write like m_baseHP/m_baseStamina (GetTotalFoodValue hardcodes eitr = 0f before
                // summing food), so this is the only place a permanent eitr bonus can land.
                eitr += Progression.FoodMilestones.GetEitrBonus(__instance);

                // A bigger bar with vanilla's flat regen rate is just a slower-filling bar.
                Regen.RegenScaling.Apply(__instance, stamina, refStamina, eitr, refEitr);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Totals (GetTotalFoodValue). Reason: {ex}");
            }
        }

        // Postfixes are skipped when the original throws, so the stomach would stay empty for the
        // rest of the session. Finalizers always run.
        [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        [HarmonyFinalizer]
        public static void GetTotalFoodValue_Finalizer(List<Player.Food> ___m_foods)
        {
            RestoreFoods(___m_foods);
        }

        private static void RestoreFoods(List<Player.Food> foods)
        {
            if (!_foodsStashed) return;
            _foodsStashed = false;

            try
            {
                if (foods == null) return;
                foods.Clear();
                foods.AddRange(_stashedFoods);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Totals (restore). Reason: {ex}");
            }
            finally
            {
                _stashedFoods.Clear();
            }
        }

        // How long this item should sit in the stomach.
        //
        // Meads and other utility drinks carry no m_foodBurnTime at all, because vanilla never
        // intended them to enter the stomach, so their duration has to be invented. It used to be
        // taken from the status effect they grant - but that chained two timers together that have
        // nothing to do with each other, and it broke the whole potion tier:
        //
        //   * A health/stamina mead's effect is over in seconds. Tying the slot to it meant the
        //     drink held the slot just long enough for its draught to blink on and vanish.
        //   * Vanilla's consume gate (see CanConsumeItem_Prefix) refuses a second mead while the
        //     first effect is up, so a slot that expired with the effect could never reach a second
        //     stack either. Unstackable AND unusable.
        //
        // So the two are separated. The consume gate is untouched - it still governs how OFTEN you
        // may drink, which is the balance that stops potion spam. This governs only how long the
        // entry lingers afterwards, which is Fatty's own layer. Drink one potion per cooldown and
        // the stacks now build over minutes: slow accumulation is exactly what the cooldown is for,
        // so this obeys it rather than evading it.
        //
        // A floor, not an override: a drink whose effect already outlasts it (the resistance meads,
        // ~10 minutes) keeps its own longer timer, and anything with a REAL m_foodBurnTime is left
        // alone entirely - that is an authored food duration and none of this applies to it.
        // Solid food is never given an invented duration: no burn time means no stomach entry.
        private static float GetBurnTime(ItemDrop.ItemData item, float durationMultiplier)
        {
            var shared = item?.m_shared;
            if (shared == null) return 0f;

            float baseTime = shared.m_foodBurnTime;

            if (baseTime <= 0f)
            {
                if (shared.m_consumeStatusEffect != null)
                    baseTime = shared.m_consumeStatusEffect.m_ttl;

                // Covers both cases at once: a short potion ttl is raised to the floor, and a drink
                // with no status effect at all (vanilla's Tasty Mead) still lands on it from zero.
                if (CategoryManager.IsDrinkPrefab(ResolvePrefabName(item)))
                    baseTime = Mathf.Max(baseTime, ConfigManager.drinkSlotDefaultDuration.Value);
            }

            return baseTime > 0f ? baseTime * durationMultiplier : 0f;
        }

        // ------------------------------------------------------------------
        // UpdateFood: vanilla's 10-second food-regen tick sums m_foodRegen RAW over every entry in
        // the stomach (Player.UpdateFood), with no diminishing returns - so 5 stacks of one food
        // healed 5x as much even though its max-HP contribution was correctly diminished to 1.85x.
        // Claim the tick before vanilla reaches it and re-run it through the same stack ladder every
        // other stat already uses.
        //
        // Zeroing m_foodRegenTimer here means vanilla's own `m_foodRegenTimer += dt; if (>= 10f)`
        // check fails immediately afterwards, so it cannot heal as well - no double-tick possible.
        // ------------------------------------------------------------------
        [HarmonyPatch(typeof(Player), "UpdateFood")]
        [HarmonyPrefix]
        public static void UpdateFood_Prefix(Player __instance, float dt, bool forceUpdate,
            ref float ___m_foodRegenTimer, List<Player.Food> ___m_foods)
        {
            try
            {
                // Vanilla skips its regen block entirely on forced updates; mirror that.
                if (forceUpdate || __instance == null) return;
                if (___m_foodRegenTimer + dt < 10f) return;

                ___m_foodRegenTimer = 0f;
                if (___m_foods == null || ___m_foods.Count == 0) return;

                float total = 0f;
                Dictionary<string, int> seen = new Dictionary<string, int>();
                foreach (Player.Food food in ___m_foods)
                {
                    if (food == null || food.m_item == null || food.m_item.m_shared == null) continue;
                    int stackIndex = NextStackIndex(seen, ResolvePrefabName(food));
                    total += food.m_item.m_shared.m_foodRegen * GetStackMultiplier(stackIndex);
                }

                if (total <= 0f) return;

                float regenMultiplier = 1f;
                __instance.GetSEMan().ModifyHealthRegen(ref regenMultiplier);
                __instance.Heal(total * regenMultiplier);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Regen Stacking. Reason: {ex}");
            }
        }

        // ------------------------------------------------------------------
        // Sequential stack decay: vanilla's UpdateFood decays EVERY entry in m_foods by 1s every
        // tick, unconditionally (confirmed by decompile - no per-entry gating exists in vanilla at
        // all). For a single vanilla slot that's fine, but Fatty stacks up to maxFoodStacks copies
        // of the SAME prefab in m_foods, so all of them counted down - and expired - in lockstep,
        // which is what made "banked" reserves a lie: nothing was actually being held in reserve.
        //
        // Desired behaviour (per design): only the LAST-EATEN stack of a given prefab ticks down.
        // Every older stack of that same prefab is frozen until the active one expires, at which
        // point the next-most-recent survivor becomes active on its own - no promotion code needed.
        //
        // This works with zero new state because EatFood_Prefix only ever Adds (never Inserts/Sorts)
        // and vanilla's own expiry removal is an ordinary List.Remove that preserves relative order
        // (confirmed - the only mutations against m_foods anywhere in Fatty are Add and Clear). That
        // means, within any same-prefab group, the highest list index is ALWAYS the most-recently-
        // eaten survivor. So every tick: snapshot every non-last member of each group before vanilla
        // runs, let vanilla's original decrement/removal logic execute completely untouched, then
        // restore the snapshot - undoing the decrement for everything except the one active entry.
        //
        // A prefix/postfix undo rather than a transpiler on purpose: vanilla's nested internal
        // GetTotalFoodValue() call (inside UpdateFood, before any Harmony postfix can run) will see a
        // frozen entry's just-decremented (not yet restored) value for that one nested call, meaning
        // a frozen stack's contribution to actual max HP/Stamina/Eitr is understated by a fixed,
        // non-growing 1-second offset (never compounding - each tick decrements then restores back to
        // the same value). Against typical burn times (100s-1800s) that's under 1%, and it never
        // reaches the HUD/Feast Ledger at all - those read GetFoods() from a separate call after
        // UpdateFood has fully returned (prefix->original->postfix already complete by then). Not
        // worth a transpiler to close.
        //
        // Accepted edge case, not worth extra code: if a stack is displaced from active to frozen at
        // a moment its own m_time is already under 1s, it can still expire on that same transition
        // tick before this freezes it. It was about to expire anyway - letting it go is fine.
        // ------------------------------------------------------------------
        private static readonly List<Player.Food> _frozenFoodEntries = new List<Player.Food>();
        private static readonly List<float> _frozenFoodTimes = new List<float>();
        private static readonly List<float> _frozenFoodHealth = new List<float>();
        private static readonly List<float> _frozenFoodStamina = new List<float>();
        private static readonly List<float> _frozenFoodEitr = new List<float>();

        [HarmonyPatch(typeof(Player), "UpdateFood")]
        [HarmonyPrefix]
        public static void FoodDecay_Prefix(List<Player.Food> ___m_foods)
        {
            _frozenFoodEntries.Clear();
            _frozenFoodTimes.Clear();
            _frozenFoodHealth.Clear();
            _frozenFoodStamina.Clear();
            _frozenFoodEitr.Clear();

            try
            {
                if (___m_foods == null || ___m_foods.Count < 2) return;

                // Highest list index per prefab wins - that's the most-recently-eaten survivor.
                Dictionary<string, int> lastIndexByPrefab = new Dictionary<string, int>();
                for (int i = 0; i < ___m_foods.Count; i++)
                {
                    Player.Food f = ___m_foods[i];
                    if (f == null || f.m_item == null || f.m_item.m_shared == null) continue;
                    lastIndexByPrefab[ResolvePrefabName(f)] = i;
                }

                for (int i = 0; i < ___m_foods.Count; i++)
                {
                    Player.Food f = ___m_foods[i];
                    if (f == null || f.m_item == null || f.m_item.m_shared == null) continue;
                    if (lastIndexByPrefab.TryGetValue(ResolvePrefabName(f), out int lastIdx) && lastIdx == i)
                        continue; // active entry - let vanilla decay it normally

                    _frozenFoodEntries.Add(f);
                    _frozenFoodTimes.Add(f.m_time);
                    _frozenFoodHealth.Add(f.m_health);
                    _frozenFoodStamina.Add(f.m_stamina);
                    _frozenFoodEitr.Add(f.m_eitr);
                }
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Sequential Food Decay (prefix). Reason: {ex}");
                _frozenFoodEntries.Clear();
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateFood")]
        [HarmonyPostfix]
        public static void FoodDecay_Postfix()
        {
            try
            {
                for (int i = 0; i < _frozenFoodEntries.Count; i++)
                {
                    Player.Food f = _frozenFoodEntries[i];
                    if (f == null) continue;
                    f.m_time = _frozenFoodTimes[i];
                    f.m_health = _frozenFoodHealth[i];
                    f.m_stamina = _frozenFoodStamina[i];
                    f.m_eitr = _frozenFoodEitr[i];
                }
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Sequential Food Decay (postfix). Reason: {ex}");
            }
            finally
            {
                _frozenFoodEntries.Clear();
                _frozenFoodTimes.Clear();
                _frozenFoodHealth.Clear();
                _frozenFoodStamina.Clear();
                _frozenFoodEitr.Clear();
            }
        }

        // ------------------------------------------------------------------
        // Meads and other utility drinks: vanilla gates the ENTIRE food system on m_shared.m_food.
        // Player.CanConsumeItem only consults CanEat `if (item.m_shared.m_food > 0f)`, and
        // Player.ConsumeItem only calls EatFood under the same condition. Every one of the 20
        // vanilla meads has m_food == 0 (they carry no food stats at all - their whole effect is
        // the status effect they grant), so none of them ever reached EatFood and none of them ever
        // entered m_foods. That silently made the dedicated drink slot unreachable for real meads,
        // made Drunk impossible to trigger, and left the "+ 1 Drink" synergies completable only by
        // the two food-valued drinks in the game.
        //
        // This patch routes exactly that class of item - no food value, but flagged as a drink by
        // Fatty's own data - into the stomach so it can hold the drink slot. Anything with real
        // food value is untouched: vanilla already handles it.
        //
        // Deliberately ADDITIVE ONLY. There is no matching CanConsumeItem veto, and a drink that
        // doesn't fit is skipped in silence rather than refused: vanilla lets you drink any mead at
        // any time, and taking that away - so that a Frost Resistance mead could lock you out of a
        // health mead mid-fight because "the drink slot is full" - would be a far worse bug than
        // the one being fixed. Worst case the second drink simply doesn't get a Fatty buff.
        // ------------------------------------------------------------------
        private static bool IsStatOnlyDrink(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return false;
            if (item.m_shared.m_food > 0f) return false; // vanilla already routes this one
            return CategoryManager.IsDrinkPrefab(ResolvePrefabName(item));
        }

        // ------------------------------------------------------------------
        // Tasty Mead stacking. Vanilla's CanConsumeItem refuses an item when you already have its
        // consume status effect OR any effect sharing its category - and every vanilla mead's effect
        // carries the same "Mead" category. That single check is the game's potion cooldown: it is
        // what stops you chugging five health meads in a fight, and it stays.
        //
        // It also made Drunk impossible. Tasty Mead does carry a consume status effect, so the
        // second one was always refused, ConsumeItem returned false, and ConsumeItem_Postfix below
        // never got to add a stack - no matter how low Drunk's threshold was set.
        //
        // Exempted for MeadTasty and NOTHING else, deliberately. A blanket exemption for every Fatty
        // drink IS the cooldown override, and it would hand the player unlimited healing. Tasty Mead
        // is the one safe case because its effect has no mechanical payload: re-consuming just runs
        // vanilla's AddStatusEffect(se, resetTime: true), which refreshes an effect that does
        // nothing. Every other drink keeps the vanilla gate exactly as written - see the floor in
        // GetBurnTime for how potions stack instead, which is by obeying the cooldown rather than
        // evading it.
        //
        // A PREFIX rather than a postfix on purpose: vanilla prints "$msg_cantconsume" to the centre
        // of the screen BEFORE returning false, so flipping the result afterwards would still flash
        // a refusal at the player on every single drink.
        // ------------------------------------------------------------------
        [HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem))]
        [HarmonyPrefix]
        public static bool CanConsumeItem_Prefix(Player __instance, ItemDrop.ItemData item, bool checkWorldLevel, ref bool __result)
        {
            try
            {
                if (item == null || item.m_shared == null) return true; // let vanilla handle oddities
                if (ResolvePrefabName(item) != Synergies.DrinkBuffs.TastyMeadPrefab) return true;

                // Humanoid.CanConsumeItem's own checks, replicated. This exemption may only ever
                // bypass the status-effect refusal - it must never smuggle an item past these.
                if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable) return true;
                if (checkWorldLevel && Game.m_worldLevel > 0 && item.m_worldLevel < Game.m_worldLevel) return true;

                // No room left in the slot: fall through to vanilla, which refuses with its own
                // message. Only ever loosens, never adds a veto of its own.
                if (!CanAddFood(__instance.GetFoods(), item, out _)) return true;

                __result = true;
                return false; // skip vanilla's same-effect/same-category refusal
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Tasty Mead Stacking (CanConsumeItem). Reason: {ex}");
                return true; // fall back to vanilla
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
        [HarmonyPostfix]
        public static void ConsumeItem_Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            try
            {
                if (!__result || !IsStatOnlyDrink(item)) return;

                // Checked here rather than letting EatFood do it, because EatFood reports a refusal
                // to the player on screen - and this consumption already succeeded.
                if (!CanAddFood(__instance.GetFoods(), item, out _)) return;

                __instance.EatFood(item);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Drink Slot (ConsumeItem). Reason: {ex}");
            }
        }

        // ------------------------------------------------------------------
        // RemoveOneFood: vanilla's puke effect (SE_Puke) calls this once per second to make you
        // vomit food one item at a time - tuned for the 3-slot vanilla stomach. Fatty expands the
        // stomach to many stacked slots, so a single puke would only clear a couple of items before
        // it expired, leaving most food behind while max HP had already dropped with each removal
        // ("damaged max hp but didn't remove the foods"). Empty the whole stomach in one call so a
        // puke fully resets it, the way it effectively does in vanilla.
        // ------------------------------------------------------------------
        [HarmonyPatch(typeof(Player), nameof(Player.RemoveOneFood))]
        [HarmonyPrefix]
        public static bool RemoveOneFood_Prefix(Player __instance, ref List<Player.Food> ___m_foods, ref bool __result)
        {
            try
            {
                if (___m_foods == null || ___m_foods.Count == 0)
                {
                    __result = false;
                    return false;
                }

                ___m_foods.Clear();
                __instance.UpdateFood(0f, forceUpdate: true); // drop max stats back to base immediately
                __result = true;
                return false; // skip vanilla's single-item removal
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Puke Clears Stomach. Reason: {ex}");
                return true; // fall back to vanilla single-remove
            }
        }
    }
}
