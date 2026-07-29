using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace Fatty.Configuration
{
    public static class ConfigManager
    {
        // Note: Set up ServerSync for strict server authority
        private static readonly ConfigSync configSync = new ConfigSync("com.wubarrk.fatty")
        {
            DisplayName = "Fatty",
            CurrentVersion = FattyPlugin.PluginVersion,
            MinimumRequiredVersion = FattyPlugin.PluginVersion
        };

        // Server-side lock config
        public static ConfigEntry<bool> serverConfigLocked;

        // Note: Core features config
        public static ConfigEntry<bool> noFoodDecay;
        public static ConfigEntry<int> maxFoodStacks;
        public static ConfigEntry<bool> rebuildFoodData;
        public static ConfigEntry<bool> debugMode;

        // Note: Stacking diminishing returns (multiplier applied to each additional stack of the SAME food)
        public static ConfigEntry<float> stackDiminishingReturnStep1; // 2nd stack
        public static ConfigEntry<float> stackDiminishingReturnStep2; // 3rd stack
        public static ConfigEntry<float> stackDiminishingReturnStep3; // 4th+ stack

        // Note: Regen scaling (keeps stamina/eitr refill time at vanilla parity)
        public static ConfigEntry<bool> scaleRegenWithPool;
        public static ConfigEntry<float> maxRegenScale;

        // Note: Food duration - Fatty's own multiplier, so the external mod is optional
        public static ConfigEntry<float> foodDurationMultiplier;

        // Note: Milestones - lifetime food counts grant permanent base HP
        public static ConfigEntry<bool> enableMilestones;
        public static ConfigEntry<int> milestoneTier1Count;
        public static ConfigEntry<int> milestoneTier2Count;
        public static ConfigEntry<int> milestoneTier3Count;
        public static ConfigEntry<float> milestoneTier1Hp;
        public static ConfigEntry<float> milestoneTier2Hp;
        public static ConfigEntry<float> milestoneTier3Hp;
        public static ConfigEntry<float> milestoneTotalCap;

        // Note: Drink buffs
        public static ConfigEntry<bool> enableDrinkBuffs;
        public static ConfigEntry<float> drinkBuffSkillLevels;
        public static ConfigEntry<float> drinkBuffEitrRegen;
        public static ConfigEntry<float> drinkBuffStaminaRegen;
        public static ConfigEntry<float> drinkBuffMaxHealth;
        public static ConfigEntry<float> drinkSlotDefaultDuration;
        public static ConfigEntry<bool> enableDrunk;
        public static ConfigEntry<int> drunkStacksRequired;
        public static ConfigEntry<float> drunkDamageBonus;
        public static ConfigEntry<float> drunkStaggerResist;
        public static ConfigEntry<float> drunkBlockingPenalty;
        public static ConfigEntry<float> drunkBlockStaminaPenalty;

        // Note: Synergy configs
        public static ConfigEntry<bool> enableSynergies;
        public static ConfigEntry<float> balancedDietRegen;
        public static ConfigEntry<float> sugarRushSpeedBonus;
        public static ConfigEntry<float> fishermanSwimStaminaReduction;
        public static ConfigEntry<float> lumberjackCarryWeight;
        public static ConfigEntry<float> lumberjackWoodcuttingDamage;

        // Note: HUD panel placement - client-side UI preferences, not gameplay, so these are
        // intentionally unsynced (see the block near rebuildFoodData/debugMode below).
        public static ConfigEntry<float> foodBreakdownPanelX;
        public static ConfigEntry<float> foodBreakdownPanelY;
        public static ConfigEntry<float> foodBreakdownPanelWidth;
        public static ConfigEntry<float> foodBreakdownPanelHeight;
        public static ConfigEntry<bool> useSegmentedStackBar;
        public static ConfigEntry<KeyboardShortcut> breakdownToggleKey;

        public static void Init(ConfigFile config)
        {
            serverConfigLocked = config.Bind("1 - General", "Lock Configuration", true, "If on, the configuration is locked and can be changed by server admins only.");
            configSync.AddLockingConfigEntry(serverConfigLocked);

            noFoodDecay = BindSynced(config, "2 - Core", "No Food Decay", true,
                "Vanilla fades a food's stats to nothing as its timer runs down. ON = you keep 100% of a food's stats until its timer expires, then lose them all at once. OFF = vanilla's gradual decay curve.");
            maxFoodStacks = BindSynced(config, "2 - Core", "Max Food Stacks", 5,
                "How many times the same food can be eaten at once. Vanilla = 1 (no stacking). Each extra stack is worth less - see section 3.", 1, 20);

            stackDiminishingReturnStep1 = BindSynced(config, "3 - Stacking", "Stack 2 Multiplier", 0.5f,
                "Fraction of a food's stats granted by its 2nd stack. 1.0 = full value, 0.5 = 50% (default), 0.0 = nothing. The 1st stack is always 100%.", 0f, 1f);
            stackDiminishingReturnStep2 = BindSynced(config, "3 - Stacking", "Stack 3 Multiplier", 0.25f,
                "Fraction of a food's stats granted by its 3rd stack. 0.25 = 25% (default).", 0f, 1f);
            stackDiminishingReturnStep3 = BindSynced(config, "3 - Stacking", "Stack 4+ Multiplier", 0.05f,
                "Fraction of a food's stats granted by the 4th and every further stack. 0.05 = 5% (default).", 0f, 1f);

            enableSynergies = BindSynced(config, "4 - Synergies", "Enable Synergies", true,
                "Enables the dietary synergy status effects (Balanced Diet, Sugar Rush, Fisherman's Friend, Lumberjack's Feast).");
            balancedDietRegen = BindSynced(config, "4 - Synergies", "Balanced Diet Regen Multiplier", 3f,
                "Balanced Diet (1 Meat + 1 Veggie): MULTIPLIER on vanilla health & stamina regen. 1.0 = vanilla, 3.0 = 3x vanilla / +200% (default).", 1f, 10f);
            sugarRushSpeedBonus = BindSynced(config, "4 - Synergies", "Sugar Rush Speed Multiplier", 2.5f,
                "Sugar Rush (3 Sweets + 1 Drink): MULTIPLIER on vanilla movement speed. 1.0 = vanilla, 2.5 = 2.5x / +150% (default).", 1f, 5f);
            fishermanSwimStaminaReduction = BindSynced(config, "4 - Synergies", "Fisherman Swim Stamina Reduction", 0.50f,
                "Fisherman's Friend (2 Fish + 1 Veggie): FRACTION of vanilla swim stamina drain removed. 0.0 = vanilla drain, 0.50 = half the drain (default), 1.0 = free swimming.", 0f, 1f);
            lumberjackCarryWeight = BindSynced(config, "4 - Synergies", "Lumberjack Carry Weight Bonus", 100f,
                "Lumberjack's Feast (3 Meat + 1 Drink): FLAT carry weight added on top of vanilla. 0 = vanilla, 100 = +100 units (default).", 0f, 1000f);
            lumberjackWoodcuttingDamage = BindSynced(config, "4 - Synergies", "Lumberjack Woodcutting Damage", 0.45f,
                "Lumberjack's Feast (3 Meat + 1 Drink): FRACTION added to vanilla woodcutting damage. 0.0 = vanilla, 0.45 = +45% (default).", 0f, 5f);

            // Vanilla stamina/eitr regen are flat rates that ignore how big the bar is, so Fatty's
            // larger pools made them refill far slower than vanilla. On by default: without it the
            // mod is a straight regen downgrade, worst of all for eitr/mage builds.
            scaleRegenWithPool = BindSynced(config, "6 - Regen", "Scale Regen With Pool", true,
                "Vanilla regenerates stamina & eitr at a FLAT rate that ignores bar size, so any mod that enlarges your pools makes them refill slower. ON = Fatty scales the rate with the pool, holding refill TIME at vanilla parity. OFF = vanilla's flat rate.");
            maxRegenScale = BindSynced(config, "6 - Regen", "Max Regen Scale", 4f,
                "Safety cap on the above, as a MULTIPLIER of vanilla's regen rate. 1.0 = never speed up at all, 4.0 = at most 4x vanilla (default). Fatty never scales BELOW 1.0, so regen can never end up worse than vanilla.", 1f, 20f);

            // Fatty's own food timer extension, so blacks7ar's FoodDurationMultiplier is optional.
            // If that mod IS installed Fatty defers to its value entirely rather than multiplying
            // the two, so nobody running 2x in both silently ends up at 4x.
            foodDurationMultiplier = BindSynced(config, "7 - Duration", "Food Duration Multiplier", 1f,
                "MULTIPLIER on how long food lasts. 1.0 = vanilla duration (default), 2.0 = food lasts twice as long. IGNORED while blacks7ar's FoodDurationMultiplier is installed - that mod's value is used instead, so the two can never multiply together.", 0.1f, 10f);

            enableMilestones = BindSynced(config, "8 - Milestones", "Enable Milestones", true,
                "Eating a lot of a food category over the life of your character permanently raises your BASE max health. Vanilla base is 25. Small, infrequent awards that reward a broad diet.");
            milestoneTier1Count = BindSynced(config, "8 - Milestones", "Tier 1 Count", 50,
                "Items of a SINGLE category that must be eaten to earn that category's 1st milestone. Default 50.", 1, 100000);
            milestoneTier2Count = BindSynced(config, "8 - Milestones", "Tier 2 Count", 200,
                "Items of a single category needed for its 2nd milestone. Default 200. Should be higher than Tier 1 Count.", 1, 100000);
            milestoneTier3Count = BindSynced(config, "8 - Milestones", "Tier 3 Count", 500,
                "Items of a single category needed for its 3rd milestone. Default 500. Should be higher than Tier 2 Count.", 1, 100000);
            milestoneTier1Hp = BindSynced(config, "8 - Milestones", "Tier 1 Health", 5f,
                "FLAT base max health added by a category's 1st milestone. 5 = +5 on top of vanilla's 25 base (default).", 0f, 500f);
            milestoneTier2Hp = BindSynced(config, "8 - Milestones", "Tier 2 Health", 5f,
                "Flat base max health added by a category's 2nd milestone. Default +5.", 0f, 500f);
            milestoneTier3Hp = BindSynced(config, "8 - Milestones", "Tier 3 Health", 10f,
                "Flat base max health added by a category's 3rd milestone. Default +10.", 0f, 500f);
            milestoneTotalCap = BindSynced(config, "8 - Milestones", "Total Health Cap", 50f,
                "Hard cap on total base health from ALL milestones combined. Each of the 5 real categories can grant 20 at the defaults, so a broad diet is what reaches the 50 cap.", 0f, 1000f);

            enableDrinkBuffs = BindSynced(config, "9 - Drink Buffs", "Enable Drink Buffs", true,
                "Whatever holds your drink slot grants a buff chosen by its food category, lasting exactly as long as the drink does.");
            drinkBuffSkillLevels = BindSynced(config, "9 - Drink Buffs", "Skill Level Bonus", 15f,
                "FLAT temporary skill levels granted by a drink buff (each buff boosts two related skills). 0 = vanilla, 15 = +15 levels (default).", 0f, 100f);
            drinkBuffEitrRegen = BindSynced(config, "9 - Drink Buffs", "Arcane Eitr Regen Multiplier", 1.5f,
                "Arcane Draught (eitr drinks): MULTIPLIER on vanilla eitr regen. 1.0 = vanilla, 1.5 = +50% (default).", 1f, 10f);
            drinkBuffStaminaRegen = BindSynced(config, "9 - Drink Buffs", "Reveler Stamina Regen Multiplier", 1.5f,
                "Reveler's Draught (sweet drinks): MULTIPLIER on vanilla stamina regen. 1.0 = vanilla, 1.5 = +50% (default).", 1f, 10f);
            drinkBuffMaxHealth = BindSynced(config, "9 - Drink Buffs", "Max Health Per Drink", 10f,
                "FLAT bonus max health while a buffing drink is active. 0 = none, 10 = +10 (default). Follows the same stacking diminishing returns as food, so extra stacks add less.", 0f, 200f);
            drinkSlotDefaultDuration = BindSynced(config, "9 - Drink Buffs", "Default Drink Duration", 600f,
                "MINIMUM seconds a drink holds the drink slot. A health or stamina mead's own effect is over in seconds, which would leave its draught blinking on and off, so drinks are held for at least this long. Drinks that already last longer (the resistance meads) keep their own timer, and food with a real burn time is unaffected. This does NOT let you drink more often - vanilla's cooldown between meads is untouched. 600 = 10 minutes (default). Scaled by the Food Duration Multiplier like everything else.", 10f, 7200f);

            enableDrunk = BindSynced(config, "9 - Drink Buffs", "Enable Drunk", true,
                "Drinking several Tasty Meads at once makes you Drunk: harder hitting and harder to stagger, but your guard suffers. See 'Drunk Stacks Required' for how many.");
            drunkStacksRequired = BindSynced(config, "9 - Drink Buffs", "Drunk Stacks Required", 3,
                "How many Tasty Meads must be in your drink slot at once to get Drunk. Capped by Max Food Stacks, so it can never ask for more meads than your stomach can hold. 3 = default.", 1, 20);
            drunkDamageBonus = BindSynced(config, "9 - Drink Buffs", "Drunk Damage Bonus", 0.15f,
                "Drunk: FRACTION added to vanilla damage on all attacks. 0.0 = vanilla, 0.15 = +15% (default).", 0f, 2f);
            drunkStaggerResist = BindSynced(config, "9 - Drink Buffs", "Drunk Stagger Resistance", 0.25f,
                "Drunk: FRACTION of incoming stagger ignored. 0.0 = vanilla, 0.25 = 25% less stagger (default), 1.0 = unstaggerable.", 0f, 1f);
            drunkBlockingPenalty = BindSynced(config, "9 - Drink Buffs", "Drunk Blocking Penalty", 20f,
                "Drunk: FLAT blocking skill levels LOST. 0 = no penalty, 20 = -20 levels (default).", 0f, 100f);
            drunkBlockStaminaPenalty = BindSynced(config, "9 - Drink Buffs", "Drunk Block Stamina Penalty", 0.5f,
                "Drunk: FRACTION added to the stamina cost of blocking. 0.0 = vanilla, 0.5 = +50% (default).", 0f, 5f);

            // Note: these two are intentionally NOT server-synced - they are per-client maintenance
            // switches, not gameplay values, and a server has no business forcing either.
            rebuildFoodData = config.Bind("2 - Core", "Rebuild Food Data", false,
                "One-shot repair switch. ON = re-derive every food's category and drink flag from scratch on the next launch, then turn itself back off. Your existing Fatty_FoodData.json is backed up to Fatty_FoodData.json.bak first, and ANY hand-edited categories in it are overwritten. Use this when a food is filed under the wrong category.");
            debugMode = config.Bind("5 - Debug", "Enable Debug Mode", false,
                "Verbose logging: what each food was categorized as, what you ate, and milestone awards. Slot-block reasons are always logged regardless of this setting.");

            // Note: panel placement/size, saved automatically when you drag or resize a panel.
            // Local/unsynced - where YOU like a panel is not something a server should override.
            foodBreakdownPanelX = BindLocal(config, "10 - HUD Panels", "Food Breakdown Panel X", 200f,
                "Saved screen X position (pixels from the left edge) of the Feast Ledger panel. Updated automatically when you drag it.");
            foodBreakdownPanelY = BindLocal(config, "10 - HUD Panels", "Food Breakdown Panel Y", 100f,
                "Saved screen Y position (pixels from the top edge) of the Feast Ledger panel. Updated automatically when you drag it.");
            foodBreakdownPanelWidth = BindLocalRanged(config, "10 - HUD Panels", "Food Breakdown Panel Width", 420f,
                "Saved width of the Feast Ledger panel. Updated automatically when you resize it.", 200f, 800f);
            foodBreakdownPanelHeight = BindLocalRanged(config, "10 - HUD Panels", "Food Breakdown Panel Height", 420f,
                "Saved height of the Feast Ledger panel. Updated automatically when you resize it.", 150f, 600f);
            useSegmentedStackBar = BindLocal(config, "10 - HUD Panels", "Use Segmented Stack Bar", true,
                "How a stacked food slot shows its individual items. ON = a thin segmented bar under the icon, one segment per stacked item, dimming as each expires. OFF = a simple 'xN' count badge instead. Only one shows at a time.");
            breakdownToggleKey = config.Bind("10 - HUD Panels", "Breakdown Panel Hotkey", new KeyboardShortcut(KeyCode.F8),
                "Opens/closes the Feast Ledger panel from the keyboard, alongside its own on-screen toggle button. Default F8.");
        }

        // Binds a config entry and registers it with ServerSync so servers have authority over the value.
        private static ConfigEntry<T> BindSynced<T>(ConfigFile config, string section, string key, T defaultValue, string description)
        {
            ConfigEntry<T> entry = config.Bind(section, key, defaultValue, description);
            configSync.AddConfigEntry(entry);
            return entry;
        }

        // Local/unsynced entries - deliberately never registered with configSync. Used for
        // per-client preferences (panel placement, debug logging) that a server has no business
        // overriding, as opposed to every other entry above which IS gameplay and IS synced.
        private static ConfigEntry<float> BindLocal(ConfigFile config, string section, string key, float defaultValue, string description)
        {
            return config.Bind(section, key, defaultValue, description);
        }

        private static ConfigEntry<bool> BindLocal(ConfigFile config, string section, string key, bool defaultValue, string description)
        {
            return config.Bind(section, key, defaultValue, description);
        }

        private static ConfigEntry<float> BindLocalRanged(ConfigFile config, string section, string key, float defaultValue, string description, float min, float max)
        {
            return config.Bind(section, key, defaultValue, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
        }

        // Range-checked overload. Every numeric setting goes through one of these: without an
        // AcceptableValueRange nothing stops a hand-edited .cfg - or a value pushed by a server -
        // from putting a negative, NaN or Infinity straight into max-health and regen arithmetic,
        // where it silently corrupts the health bar for the rest of the session.
        //
        // This also covers NaN and +/-Infinity for free: AcceptableValueRange clamps via
        // IComparable.CompareTo, and float.CompareTo orders NaN below -Infinity, so both ends land
        // on a real number. BepInEx re-clamps in the Value setter, so ServerSync pushes are checked
        // on arrival too, not just at load.
        private static ConfigEntry<float> BindSynced(ConfigFile config, string section, string key, float defaultValue, string description, float min, float max)
        {
            return BindRanged(config, section, key, defaultValue, description, new AcceptableValueRange<float>(min, max));
        }

        private static ConfigEntry<int> BindSynced(ConfigFile config, string section, string key, int defaultValue, string description, int min, int max)
        {
            return BindRanged(config, section, key, defaultValue, description, new AcceptableValueRange<int>(min, max));
        }

        private static ConfigEntry<T> BindRanged<T>(ConfigFile config, string section, string key, T defaultValue, string description, AcceptableValueBase range)
        {
            ConfigEntry<T> entry = config.Bind(section, key, defaultValue, new ConfigDescription(description, range));
            configSync.AddConfigEntry(entry);
            return entry;
        }
    }
}
