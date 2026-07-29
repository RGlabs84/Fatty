using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Scanners
{
    [HarmonyPatch]
    public static class FoodScanner
    {
        private static bool _hasScanned = false;

        // Splits PascalCase/camelCase prefab names into whole words (e.g. "PerchSteak" -> "perch",
        // "steak"). Category keywords are matched as WHOLE words against this set, not raw substrings -
        // a plain string.Contains("tea") previously matched inside "Steak" and Contains("mead") matched
        // inside "Meadows", silently miscategorizing solid food as "Drink" (stealing the one drink slot
        // and blocking real food/drink from being eaten).
        private static readonly Regex WordSplitter = new Regex(@"[A-Z]?[a-z0-9]+|[A-Z0-9]+(?![a-z])", RegexOptions.Compiled);

        // Includes a few whole-word compounds ("vineberry", "meatbug", "magmafish", "meadnog",
        // "alenog") that fuse a keyword with no internal capital to split on (e.g. "Meatbug", not
        // "MeatBug") - whole-word matching alone would silently miss those and fall back to the
        // Vegetable default, the same way it correctly rejects unrelated words like "Meadows"/"Steak"
        // that merely happen to contain a keyword as a substring.
        private static readonly HashSet<string> DrinkWords = new HashSet<string> { "tea", "coffee", "mead", "ale", "drink", "meadnog", "alenog", "shake", "smoothie", "shroomshake", "beer", "brew", "booze", "perry", "blaand", "nog", "cider", "juice", "infusion", "wine", "mjod", "grog", "elixir", "tonic", "draught", "wort" };

        // Meat/Fish carry named creatures as well as cuts. Since "Vegetable" is no longer the
        // catch-all, anything these lists miss now lands in Unknown rather than being quietly
        // mislabelled - so it is worth naming the common Valheim/Valheim Cuisine animals directly.
        private static readonly HashSet<string> MeatWords = new HashSet<string> { "meat", "steak", "pork", "sausage", "sausages", "meatbug", "boar", "deer", "wolf", "lox", "neck", "serpent", "troll", "bear", "hare", "chicken", "seeker", "asksvin", "volture", "bonemaw", "drake", "kraken", "ham", "jerky", "salami", "bacon", "entrails", "heart", "morgen", "ulv", "hakarl", "tentacle" };
        private static readonly HashSet<string> SweetWords = new HashSet<string> { "berry", "berries", "cake", "sweet", "honey", "vineberry", "cookies", "cookie", "jam", "pie", "krem", "candy", "fruit" };
        private static readonly HashSet<string> FishWords = new HashSet<string> { "fish", "salmon", "magmafish", "perch", "pike", "trout", "herring", "lutefisk", "rakfisk", "stockfish", "gravlaks", "puffers", "grouper", "tetra", "hafgufa" };
        private static readonly HashSet<string> VegetableWords = new HashSet<string> { "turnip", "carrot", "onion", "mushroom", "shroom", "magecap", "vegetable", "veggies", "greens", "root", "roots", "barley", "flax", "seed", "seeds", "salad", "cabbage", "leek", "herb", "herbal", "bread", "porridge", "dumplings", "omelette", "potato", "pickled", "sap" };

        // Vanilla drinks whose diet can be read from NEITHER a status effect nor a name word.
        // Tasty Mead is the whole list: it grants no status effect at all (which is also why
        // GetBurnTime has to give it a configured default duration) and "mead"/"tasty" hit no diet
        // word list. It was previously only Sweet because GenerateDefaultCategories seeds it that
        // way on a fresh install - a "Rebuild Food Data" or any data-version migration throws those
        // seeds away and re-derives, which silently turned it Unknown and killed Reveler's Draught.
        private static readonly Dictionary<string, string> KnownDrinkDiets = new Dictionary<string, string>
        {
            { "MeadTasty", "Sweet" },
        };

        private static void AddWords(string s, HashSet<string> into)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (Match m in WordSplitter.Matches(s))
                into.Add(m.Value.ToLowerInvariant());
        }

        // Spread the scan across frames so a large modded ObjectDB doesn't stall the load frame.
        private const int ItemsPerFrame = 100;

        // Early attempt. On a vanilla-only setup the ObjectDB is already populated at ZNetScene.Awake
        // and this succeeds. On a modded setup (Jotunn etc.) the custom items are injected slightly
        // LATER, so this often finds an empty/not-ready DB and no-ops - the OnSpawned hook below then
        // covers it once everything is loaded. Both are guarded by _hasScanned so the scan runs once.
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        [HarmonyPostfix]
        public static void ZNetScene_Awake_Postfix(ZNetScene __instance)
        {
            try
            {
                if (_hasScanned || __instance == null) return;

                // Run the scan as a coroutine on the ZNetScene MonoBehaviour so it yields between
                // batches instead of blocking the load frame.
                __instance.StartCoroutine(ScanRoutine());
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Dynamic Food Scanner (start). Reason: {ex}");
            }
        }

        // Reliable trigger: by the time the local player has spawned, the ObjectDB is fully populated
        // - including every modded food (Jotunn injects items during world load, BEFORE spawn). The
        // earlier ZNetScene.Awake attempt runs too soon on modded setups, so this is what actually
        // catches modded food. Guarded by _hasScanned so it still only runs once per session.
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [HarmonyPostfix]
        public static void Player_OnSpawned_Postfix(Player __instance)
        {
            // m_baseHP is a prefab-serialized field, so it resets to vanilla's 25 on every new
            // Player instance - re-apply the earned milestone bonus on each spawn.
            try
            {
                Progression.FoodMilestones.OnSpawned(__instance);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Milestones (spawn). Reason: {ex}");
            }

            try
            {
                if (_hasScanned || __instance == null) return;
                __instance.StartCoroutine(ScanRoutine());
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Dynamic Food Scanner (spawn). Reason: {ex}");
            }
        }

        private static IEnumerator ScanRoutine()
        {
            // Claim the run up front so a second Awake can't start a parallel scan.
            if (_hasScanned) yield break;
            _hasScanned = true;

            // Setup phase. Guard it so an exception here logs a Feature-Lost line instead of
            // silently killing the coroutine (an unhandled throw in a coroutine leaves no Fatty
            // log at all, which is exactly why an earlier scan "vanished" with no save).
            List<GameObject> items = null;
            bool setupOk = false;
            try
            {
                FattyPlugin.Instance.Log.LogInfo("Scanning ObjectDB for custom food items...");

                var objectDB = ObjectDB.instance;
                if (objectDB == null || objectDB.m_items == null)
                {
                    // Expected on modded setups when ZNetScene.Awake fires before the DB is filled.
                    // Not a lost feature - the Player.OnSpawned hook retries once the DB is ready.
                    FattyPlugin.Instance.Log.LogInfo("Fatty: ObjectDB not populated yet; deferring food scan until player spawn.");
                    _hasScanned = false; // allow the OnSpawned attempt to run
                }
                else
                {
                    // Make sure the base category keys always exist before we start adding to them.
                    CategoryManager.EnsureBaseCategories();

                    // Inject hardcoded integrations first.
                    Integrations.MapHardcodedIntegrations(CategoryManager.Categories);

                    // Snapshot the item list so we aren't iterating a collection another mod may
                    // mutate while we yield between frames.
                    items = new List<GameObject>(objectDB.m_items);
                    setupOk = true;
                }
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Dynamic Food Scanner (setup). Reason: {ex}");
                _hasScanned = false;
            }

            // yield break is legal here because it's outside the try/catch above.
            if (!setupOk || items == null) yield break;

            bool categoryUpdated = false;
            int processed = 0;
            int foodFound = 0;

            foreach (var item in items)
            {
                // Per-item try/catch (no yield inside) so one bad prefab can't abort the whole scan.
                try
                {
                    if (ProcessItem(item, ref foodFound)) categoryUpdated = true;
                }
                catch (System.Exception ex)
                {
                    FattyPlugin.Instance.Log.LogError($"Fatty Mod: Skipped a food item during scan. Reason: {ex}");
                }

                // Yield outside the try/catch (C# forbids yield inside a try-with-catch).
                if (++processed % ItemsPerFrame == 0)
                    yield return null;
            }

            // Save when something new appeared - the initial load (empty data set), a later diff
            // when a new mod adds food, or a data migration that re-sorted everything. A migration
            // MUST persist even if nothing else changed, otherwise it re-runs every launch.
            bool migrated = CategoryManager.NeedsReclassify;
            if (categoryUpdated || migrated)
            {
                try
                {
                    CategoryManager.Save();
                }
                catch (System.Exception ex)
                {
                    FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Data Save. Reason: {ex}");
                }
            }

            // Clear only after a successful pass, so an aborted scan retries the migration next launch.
            CategoryManager.NeedsReclassify = false;

            // A rebuild is a one-shot request: turn the setting back off so the next launch is a
            // normal one and hand-edits made after this point survive.
            bool wasRebuild = ConfigManager.rebuildFoodData.Value;
            if (wasRebuild) ConfigManager.rebuildFoodData.Value = false;

            string outcome = categoryUpdated || migrated ? "data saved" : "no change";
            string reason = wasRebuild ? ", rebuilt on request" : migrated ? ", migrated to new categories" : string.Empty;
            FattyPlugin.Instance.Log.LogInfo($"Fatty food scan complete: {foodFound} food item(s) catalogued ({outcome}{reason}).");
        }

        // Processes a single ObjectDB entry. Returns true if it added a new category or new
        // food data (i.e. the JSON should be re-saved).
        private static bool ProcessItem(GameObject item, ref int foodFound)
        {
            if (item == null) return false;

            var itemDrop = item.GetComponent<ItemDrop>();
            if (itemDrop == null || itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null) return false;

            var shared = itemDrop.m_itemData.m_shared;

            // Foods (health/stamina/eitr value) OR any Consumable-typed item - the latter also catches
            // "utility" meads (Frost Resistance, Tasty Mead, etc.) that carry zero food/stamina/eitr and
            // exist purely to grant a status effect. Those have to be catalogued: they are exactly the
            // items that need an IsDrink flag in order to hold the drink slot at all. Other zero-value
            // consumables (potions, scrolls, boss powers) are catalogued too, but IsDrink now requires
            // naming evidence, so they stay plain Unknown food and never claim the slot.
            bool hasFoodValue = shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
            bool isConsumable = shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
            if (!hasFoodValue && !isConsumable) return false;

            string prefabName = item.name;
            if (string.IsNullOrEmpty(prefabName)) return false;

            foodFound++;
            bool updated = false;

            string existingCategory = CategoryManager.GetCategoryForPrefab(prefabName);

            // Classify when we have nothing for this prefab, or when a data migration asked for a
            // full rebuild (pre-v2 files carry categories from the old rules - no Eitr category and
            // everything unidentified filed as Vegetable - which can't be repaired in place).
            if (existingCategory == CategoryManager.UnknownCategory || CategoryManager.NeedsReclassify)
            {
                var words = new HashSet<string>();
                AddWords(shared.m_name, words);
                AddWords(prefabName, words);

                // Hardcoded integrations are hand-authored from the source mod's own recipe list, so
                // they keep their diet even through a migration's full rebuild. The drink slot is a
                // separate axis and is always re-derived.
                // Drink detection now runs FIRST: the diet of a mead is decided by what it does, so
                // ClassifyDiet has to know whether it is looking at a drink before it picks.
                bool isDrink = IsDrink(shared, words) || CategoryManager.IsDrinkPrefab(prefabName);

                string category = CategoryManager.IsHardcoded(prefabName)
                    ? existingCategory
                    : ClassifyDiet(shared, words, isDrink, prefabName);

                CategoryManager.Classify(prefabName, category, isDrink);
                existingCategory = category;

                // Deliberately does NOT set `updated` here. Items that legitimately classify as
                // Unknown re-enter this branch on every single launch, so flagging the data dirty
                // unconditionally rewrote Fatty_FoodData.json every time the game started even
                // when nothing had actually changed. The comparison against existingData below is
                // what decides whether anything is genuinely different.

                if (ConfigManager.debugMode.Value)
                    FattyPlugin.Instance.Log.LogInfo($"Categorized '{prefabName}' as {category}{(isDrink ? " (drink)" : "")}");
            }

            // Always ensure the rich data is captured in AllFoodData so players can override it in
            // JSON. On a migration the entry already exists but its Category/IsDrink are stale, so
            // refresh them (and the stats, which may have changed with a mod update) in place.
            var existingData = CategoryManager.AllFoodData.Find(f => f != null && f.PrefabName == prefabName);
            if (existingData == null)
            {
                existingData = new CategoryManager.FoodData { PrefabName = prefabName };
                CategoryManager.AllFoodData.Add(existingData);
                updated = true;
            }
            else if (existingData.Category != existingCategory
                  || existingData.IsDrink != CategoryManager.IsDrinkPrefab(prefabName))
            {
                updated = true;
            }

            // Lets a prefab name be recovered from an item whose ItemData carries no m_dropPrefab.
            CategoryManager.MapInGameName(shared.m_name, prefabName);

            existingData.InGameName = shared.m_name;
            existingData.Health = shared.m_food;
            existingData.Stamina = shared.m_foodStamina;
            existingData.Eitr = shared.m_foodEitr;
            existingData.Regen = shared.m_foodRegen;
            existingData.BurnTime = shared.m_foodBurnTime;
            existingData.Category = existingCategory;
            existingData.IsDrink = CategoryManager.IsDrinkPrefab(prefabName);

            return updated;
        }

        // Picks the DIET category. Eitr dominance is checked before any keyword because eitr food is
        // functionally mage food regardless of what it's made of - "Mushroom Omelette" is a mage
        // meal, not a vegetable, and filing it by its name is what buried all 118 eitr foods before.
        //
        // Solid food only ever carries a flat +Eitr stat (no separate regen stat - that's drink-only,
        // see ClassifyDrinkByEffect), so "dominant" only ever means beating m_food/m_foodStamina.
        // Relaxed to OR rather than requiring Eitr beat BOTH: a food whose eitr edges out just one of
        // the other two is still meaningfully mage food, and the stricter AND rule was leaving those
        // filed as Meat/Fish/etc. Ties deliberately still fall through to keywords (Vineberry is
        // 30/30/30 - a berry, not mage food - since OR of two false ">" comparisons on equal values
        // is still false). Foods where eitr trails BOTH other stats still don't reclassify here.
        private static string ClassifyDiet(ItemDrop.ItemData.SharedData shared, HashSet<string> words, bool isDrink, string prefabName)
        {
            if (shared.m_foodEitr > 0f && (shared.m_foodEitr > shared.m_food || shared.m_foodEitr > shared.m_foodStamina))
                return "Eitr";

            // Drinks are classified by what they DO before what they are called. Every vanilla mead
            // carries zero food values and a name with no diet word in it ("MeadEitrMinor",
            // "MeadStaminaMedium", "MeadSwimmer"), so the word lists below filed all 21 of them as
            // Unknown - which maps to no draught at all. Reading the status effect they grant fixes
            // the whole lineup at once and covers modded meads without naming any of them.
            if (isDrink)
            {
                string byEffect = ClassifyDrinkByEffect(shared, prefabName);
                if (byEffect != null) return byEffect;
            }

            if (words.Overlaps(MeatWords)) return "Meat";
            if (words.Overlaps(FishWords)) return "Fish";
            if (words.Overlaps(SweetWords)) return "Sweet";
            if (words.Overlaps(VegetableWords)) return "Vegetable";

            // A drink that reached here has no readable effect and no diet word - the resistance and
            // repellent meads, whose status effects are damage-mod lists rather than stat changes.
            // They still get Warrior's rather than nothing, so committing the single drink slot is
            // never a dead choice.
            if (isDrink) return "Meat";

            // Solid food no longer defaults to Vegetable. An honest Unknown keeps the synergy
            // requirements meaningful and, because Unknown can never be a drink, it can't steal the
            // drink slot.
            return CategoryManager.UnknownCategory;
        }

        // Maps a drink onto a diet - and therefore onto a draught - by reading the stats of the
        // status effect it grants. Returns null when nothing recognizable is on offer, so the caller
        // can fall through to the name words.
        //
        // Order is by how distinctive the signature is, not by importance: eitr and swim are only
        // ever set by items specifically about them, whereas health and stamina turn up as
        // incidental extras on all sorts of things and so are checked last.
        //
        // Known tradeoff: this outranks the name words for drinks, so a modded "Salmon Broth" that
        // grants health regen classifies as Meat rather than Fish. That is the right default for the
        // vanilla lineup (where the names say nothing at all) and it is one Category edit away in
        // Fatty_FoodData.json when a mod disagrees.
        private static string ClassifyDrinkByEffect(ItemDrop.ItemData.SharedData shared, string prefabName)
        {
            if (prefabName != null && KnownDrinkDiets.TryGetValue(prefabName, out string known))
                return known;

            var se = shared.m_consumeStatusEffect as SE_Stats;
            if (se == null) return null;

            // Eitr: MeadEitrMinor / MeadEitrLingering and modded equivalents.
            if (se.m_eitrOverTime > 0f || se.m_eitrUpFront > 0f || se.m_eitrRegenMultiplier > 1f)
                return "Eitr";

            // Water work: MeadSwimmer, and anything else making swimming faster or cheaper.
            if (se.m_swimSpeedModifier > 0f || se.m_swimStaminaUseModifier < 0f)
                return "Fish";

            // Stamina and mobility: MeadStamina*, MeadHasty, MeadLightfoot. Mirrors what Reveler's
            // Draught itself grants (stamina regen, running, jumping).
            if (se.m_staminaOverTime > 0f || se.m_staminaUpFront > 0f || se.m_staminaRegenMultiplier > 1f
                || se.m_runStaminaDrainModifier < 0f || se.m_runStaminaUseModifier < 0f
                || se.m_jumpStaminaUseModifier < 0f || se.m_speedModifier > 0f)
                return "Sweet";

            // Health and fighting: MeadHealth*, MeadBzerker, MeadStrength. m_damageModifier and
            // m_armorMultiplier are MULTIPLIERS whose neutral value is 1, so a bonus is > 1, not > 0.
            if (se.m_healthOverTime > 0f || se.m_healthUpFront > 0f || se.m_healthRegenMultiplier > 1f
                || se.m_damageModifier > 1f || se.m_addArmor > 0f || se.m_armorMultiplier > 1f
                || se.m_staggerModifier < 0f || se.m_addMaxCarryWeight > 0f)
                return "Meat";

            // Resistance meads land here: their effect is an m_mods damage-modifier list with none
            // of the stat fields set. The caller's drink fallback picks them up.
            return null;
        }

        // Whether this occupies the dedicated drink slot - independent of the diet above, so an
        // eitr mead is both "Eitr" and a drink.
        //
        // Naming evidence ONLY, deliberately. This used to fall back to "any Consumable with no
        // food value", which swept up every potion, scroll and boss-power item in the game
        // (Pukeberries, HealthUpgrade_Bonemass, ArcaneScroll_*, Potion*...). That was harmless
        // while zero-food items could never enter the stomach at all, but they can now - so the
        // fallback would have handed the single drink slot to a health potion. Every one of the 20
        // vanilla meads carries "Mead" in its prefab name, so the word list covers the real cases,
        // and anything it misses is one IsDrink edit away in Fatty_FoodData.json.
        private static bool IsDrink(ItemDrop.ItemData.SharedData shared, HashSet<string> words)
        {
            return words.Overlaps(DrinkWords);
        }
    }
}
