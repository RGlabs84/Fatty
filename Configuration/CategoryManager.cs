using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace Fatty.Configuration
{
    public static class CategoryManager
    {
        private static string ConfigPath => Path.Combine(Paths.ConfigPath, "Fatty_FoodData.json");
        private static string BackupPath => ConfigPath + ".bak";

        // Bump when the on-disk shape or the classification rules change in a way that should
        // retroactively re-sort existing entries. Load() migrates anything older.
        //   1 = original: single Category string, "Drink" doubled as a slot marker, no Eitr,
        //       everything unrecognized dumped into Vegetable.
        //   2 = Category is the DIET only (+ Eitr, + real Unknown); IsDrink is its own flag.
        //   3 = drink detection now decides the slot for zero-food-value meads, which previously
        //       never reached the food system at all, so every entry is re-derived once more.
        //   4 = a drink's diet is now read from the status effect it grants rather than its name,
        //       and any drink still unmatched falls back to Meat instead of Unknown. v3 files filed
        //       21 of 25 drinks as Unknown (every eitr, health and stamina mead among them), which
        //       meant no draught at all, and a few under a stray word match like BarleyWine ->
        //       Vegetable. Neither can be repaired in place.
        //   5 = solid-food Eitr dominance relaxed from beating BOTH health and stamina to beating
        //       EITHER one, so foods that only edge out one of the two are correctly filed as Eitr
        //       instead of Meat/Fish/Vegetable/Sweet. v4 files under-classified those.
        public const int CurrentDataVersion = 5;

        // The diet categories. These drive synergies, milestones and drink buffs - NOT slot
        // allocation. "Drink" is deliberately absent: whether something takes the dedicated drink
        // slot is the separate IsDrink flag, so an eitr mead can be Eitr AND a drink at once.
        public static readonly string[] BaseCategories = { "Meat", "Vegetable", "Sweet", "Fish", "Eitr", "Unknown" };

        // Fallback diet for anything the scanner can't identify. Previously everything unknown was
        // called "Vegetable", which quietly made "1 Veggie" satisfiable by 293 of 491 items and
        // rendered the Balanced Diet / Fisherman's Friend requirements meaningless.
        public const string UnknownCategory = "Unknown";

        public class FoodData
        {
            public string PrefabName { get; set; }
            public string InGameName { get; set; }
            public float Health { get; set; }
            public float Stamina { get; set; }
            public float Eitr { get; set; }
            public float Regen { get; set; }
            public float BurnTime { get; set; }
            public string Category { get; set; }
            public bool IsDrink { get; set; }
        }

        // Wrapper so the file can carry a version alongside the list. Old files are a bare JSON
        // array, which is what Load() detects to trigger the migration.
        private class FoodDataFile
        {
            public int DataVersion { get; set; }
            public List<FoodData> Foods { get; set; }
        }

        // Internal quick lookup mapping: diet category -> prefab names.
        public static Dictionary<string, List<string>> Categories { get; private set; } = new Dictionary<string, List<string>>();

        // Reverse index of the above. GetCategoryForPrefab used to walk every category list on
        // every call, and it is called per food per tick from the synergy counter, the drink buffs
        // and the max-health bonus - so a 491-entry data file meant thousands of string compares a
        // second. Kept in step with Categories by AddToCategory/Clear.
        private static readonly Dictionary<string, string> _categoryByPrefab = new Dictionary<string, string>();

        // m_shared.m_name ("$item_carrotsoup") -> prefab name ("CarrotSoup"). Needed to recover a
        // real prefab name for modded items whose ItemData has no m_dropPrefab; see
        // PlayerPatches.ResolvePrefabName.
        private static readonly Dictionary<string, string> _prefabByInGameName = new Dictionary<string, string>();

        // Prefabs that occupy the dedicated drink slot, independent of their diet category.
        public static HashSet<string> Drinks { get; private set; } = new HashSet<string>();

        // Full data store
        public static List<FoodData> AllFoodData { get; private set; } = new List<FoodData>();

        // Set when Load() read a pre-v2 file. FoodScanner checks this to force a full
        // reclassification pass instead of only touching entries it sees as Unknown.
        public static bool NeedsReclassify { get; internal set; }

        public static void Init()
        {
            if (File.Exists(ConfigPath))
            {
                Load();
            }
            else
            {
                GenerateDefaultCategories();
                // Save happens dynamically during the ObjectDB scan so we can capture real stats.
            }

            EnsureBaseCategories();
        }

        // Guarantees every base category key exists so lookups/adds never hit KeyNotFoundException.
        public static void EnsureBaseCategories()
        {
            foreach (string cat in BaseCategories)
            {
                if (!Categories.ContainsKey(cat))
                    Categories[cat] = new List<string>();
            }
        }

        // Safely adds a prefab to a diet category, creating the list if needed and avoiding
        // duplicates. A prefab only ever belongs to one diet, so re-classifying it moves it rather
        // than leaving a stale entry behind in its old list.
        public static void AddToCategory(string category, string prefabName)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(prefabName)) return;

            if (_categoryByPrefab.TryGetValue(prefabName, out string previous))
            {
                if (previous == category) return;
                if (Categories.TryGetValue(previous, out List<string> old))
                    old.Remove(prefabName);
            }

            if (!Categories.TryGetValue(category, out List<string> list))
            {
                list = new List<string>();
                Categories[category] = list;
            }

            if (!list.Contains(prefabName))
                list.Add(prefabName);

            _categoryByPrefab[prefabName] = category;
        }

        // Records the item's in-game (localisation) name so a prefab name can be recovered from it.
        public static void MapInGameName(string inGameName, string prefabName)
        {
            if (string.IsNullOrEmpty(inGameName) || string.IsNullOrEmpty(prefabName)) return;
            _prefabByInGameName[inGameName] = prefabName;
        }

        public static string GetPrefabNameForInGameName(string inGameName)
        {
            if (string.IsNullOrEmpty(inGameName)) return null;
            return _prefabByInGameName.TryGetValue(inGameName, out string prefab) ? prefab : null;
        }

        // Marks a prefab as occupying the drink slot.
        //
        // Note on "Unknown is strictly food": that rule is enforced at the CLASSIFIER, not here.
        // FoodScanner.IsDrink() only returns true on positive evidence (a drink word, or a
        // zero-food-value Consumable), so anything that merely falls through to the Unknown diet
        // can never claim the single drink slot. Coupling the two here instead would make the
        // result depend on whether AddToCategory happened to run first, and would also wrongly
        // reject a drink we know IS a drink but whose diet we couldn't identify.
        public static void SetDrink(string prefabName, bool isDrink)
        {
            if (string.IsNullOrEmpty(prefabName)) return;

            if (isDrink) Drinks.Add(prefabName);
            else Drinks.Remove(prefabName);
        }

        public static bool IsDrinkPrefab(string prefabName)
        {
            return !string.IsNullOrEmpty(prefabName) && Drinks.Contains(prefabName);
        }

        // Convenience for callers that want both facts at once.
        public static void Classify(string prefabName, string category, bool isDrink)
        {
            AddToCategory(category, prefabName);
            SetDrink(prefabName, isDrink);
        }

        // Prefabs claimed by a hardcoded integration (Valheim Cuisine and friends). Those mappings
        // are hand-authored from the mod's own recipe list, so they outrank the name heuristic and
        // must survive a data migration's full reclassification pass.
        private static readonly HashSet<string> _hardcoded = new HashSet<string>();

        public static bool IsHardcoded(string prefabName)
        {
            return !string.IsNullOrEmpty(prefabName) && _hardcoded.Contains(prefabName);
        }

        // Hand-authored diet. The drink slot is a separate axis, so this deliberately does NOT
        // touch IsDrink - the scanner still decides that from the item's own words and stats.
        public static void AddHardcodedCategory(string category, string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return;
            AddToCategory(category, prefabName);
            _hardcoded.Add(prefabName);
        }

        // Hand-authored "this takes the drink slot", leaving the diet to the scanner - so
        // VC_ShroomBeer can be a Vegetable drink and VC_ElivagarTea an Eitr drink without either
        // being spelled out by hand.
        public static void MarkHardcodedDrink(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return;
            SetDrink(prefabName, true);
        }

        // Drops every derived lookup at once. Anything that empties Categories MUST come through
        // here, or GetCategoryForPrefab keeps answering from a stale reverse index.
        private static void ClearAll()
        {
            Categories.Clear();
            Drinks.Clear();
            _categoryByPrefab.Clear();
            _prefabByInGameName.Clear();
        }

        private static void GenerateDefaultCategories()
        {
            ClearAll();

            // Goes through AddToCategory rather than assigning the lists directly, so the reverse
            // index is built too.
            AddAll("Meat", "CookedMeat", "NeckTailGrilled", "DeerMeatCooked", "SerpentMeatCooked", "LoxMeatCooked", "WolfMeatCooked", "BugMeatCooked");
            AddAll("Vegetable", "Turnip", "Carrot", "Onion", "Mushroom", "MushroomYellow", "MushroomBlue");
            AddAll("Sweet", "Raspberry", "Blueberry", "Cloudberry", "Honey", "MeadTasty");
            AddAll("Fish", "FishRaw", "FishCooked");
            AddAll("Eitr", "MushroomMagecap", "SeekerAspic", "MushroomOmelette", "MagicallyStuffedShroom", "YggdrasilPorridge", "MisthareSupreme");

            EnsureBaseCategories();

            foreach (string drink in new[] { "MeadTasty", "MeadHealthMinor", "MeadStaminaMinor" })
                Drinks.Add(drink);
        }

        private static void AddAll(string category, params string[] prefabNames)
        {
            foreach (string prefabName in prefabNames)
                AddToCategory(category, prefabName);
        }

        public static void Load()
        {
            try
            {
                ClearAll();
                AllFoodData.Clear();
                NeedsReclassify = false;

                string json = File.ReadAllText(ConfigPath);
                int fileVersion = ReadFile(json);

                // The in-game-name index is rebuilt either way: a migration throws the categories
                // away but the prefab <-> localisation-name pairs are still valid.
                foreach (var data in AllFoodData)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.PrefabName)) continue;
                    MapInGameName(data.InGameName, data.PrefabName);
                }

                if (fileVersion < CurrentDataVersion)
                {
                    Migrate(fileVersion);
                    return;
                }

                foreach (var data in AllFoodData)
                {
                    if (data == null || string.IsNullOrWhiteSpace(data.PrefabName)) continue;

                    string category = string.IsNullOrWhiteSpace(data.Category) ? UnknownCategory : data.Category;
                    AddToCategory(category, data.PrefabName);
                    SetDrink(data.PrefabName, data.IsDrink);
                }

                FattyPlugin.Instance.Log.LogInfo($"Successfully loaded Fatty_FoodData.json with {AllFoodData.Count} items ({Drinks.Count} drinks).");
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Error loading FoodData JSON: {ex.Message}. Falling back to defaults.");
                AllFoodData.Clear();
                GenerateDefaultCategories();
            }
        }

        // Reads either shape: a bare JSON array (pre-v2) or the versioned wrapper. Returns the
        // file's data version. Populates AllFoodData either way.
        private static int ReadFile(string json)
        {
            string trimmed = json.TrimStart();

            if (trimmed.StartsWith("["))
            {
                AllFoodData = JsonConvert.DeserializeObject<List<FoodData>>(json) ?? new List<FoodData>();
                return 1;
            }

            var file = JsonConvert.DeserializeObject<FoodDataFile>(json);
            AllFoodData = file?.Foods ?? new List<FoodData>();
            return file?.DataVersion ?? 1;
        }

        // Pre-v2 files carry categories from the old rules: no Eitr category at all, and every
        // unidentified item filed as Vegetable. Those assignments can't be repaired in place, so we
        // back the file up and let FoodScanner rebuild every entry from scratch on this run.
        private static void Migrate(int fromVersion)
        {
            try
            {
                File.Copy(ConfigPath, BackupPath, overwrite: true);
                FattyPlugin.Instance.Log.LogWarning(
                    $"Fatty: migrating Fatty_FoodData.json from data version {fromVersion} to {CurrentDataVersion}. " +
                    $"Every food will be re-categorized, so any hand-edited categories WILL be overwritten - " +
                    $"your previous file has been backed up to Fatty_FoodData.json.bak.");
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty: could not back up Fatty_FoodData.json before migrating ({ex.Message}). Migrating anyway.");
            }

            // Drop the old assignments entirely but keep the entry list, so the scanner re-files
            // each prefab under the new rules and the save at the end of the scan persists it.
            // The in-game-name index survives - it is derived from the item, not from our rules.
            Categories.Clear();
            Drinks.Clear();
            _categoryByPrefab.Clear();
            EnsureBaseCategories();
            NeedsReclassify = true;
        }

        // User-triggered full re-derive, for when the classifier gets something wrong and there is
        // no version bump to hang a migration off. Same backup + rebuild path as a migration.
        public static void RequestRebuild()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    File.Copy(ConfigPath, BackupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty: could not back up Fatty_FoodData.json before rebuilding ({ex.Message}). Rebuilding anyway.");
            }

            FattyPlugin.Instance.Log.LogWarning(
                "Fatty: 'Rebuild Food Data' is on - every food will be re-categorized from scratch this launch and " +
                "any hand-edited categories WILL be overwritten. Your previous file has been backed up to " +
                "Fatty_FoodData.json.bak. The setting turns itself back off once the rebuild completes.");

            Categories.Clear();
            Drinks.Clear();
            _categoryByPrefab.Clear();
            EnsureBaseCategories();
            NeedsReclassify = true;
        }

        public static void Save()
        {
            try
            {
                var file = new FoodDataFile { DataVersion = CurrentDataVersion, Foods = AllFoodData };
                string jsonOutput = JsonConvert.SerializeObject(file, Formatting.Indented);
                File.WriteAllText(ConfigPath, jsonOutput);
                FattyPlugin.Instance.Log.LogInfo("Successfully updated Fatty_FoodData.json");
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Error saving FoodData JSON: {ex.Message}");
            }
        }

        public static string GetCategoryForPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return UnknownCategory;
            return _categoryByPrefab.TryGetValue(prefabName, out string category) ? category : UnknownCategory;
        }
    }
}
