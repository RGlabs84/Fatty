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

        // Note: Milestones - lifetime food counts grant permanent, category-flavoured perks. Each of
        // Meat/Sweet/Eitr/Fish/Fatness ALTERNATES between two reward types across its 3 tiers (tier
        // 1 and 3 grant one type, tier 2 grants the other) - e.g. Eitr: +eitr, then +eitr regen%,
        // then +eitr again. Vegetable is the one exception and stays a flat regen% on all 3 tiers,
        // since it never owned a single pool to alternate a "flat" reward against in the first place.
        public static ConfigEntry<bool> enableMilestones;
        public static ConfigEntry<int> milestoneTier1Count;
        public static ConfigEntry<int> milestoneTier2Count;
        public static ConfigEntry<int> milestoneTier3Count;
        public static ConfigEntry<float> milestoneTier1Hp;
        public static ConfigEntry<float> milestoneTier2HpRegen;
        public static ConfigEntry<float> milestoneTier3Hp;
        public static ConfigEntry<float> milestoneTotalCap;

        public static ConfigEntry<float> milestoneTier1CarryWeight;
        public static ConfigEntry<float> milestoneTier2MeatSkill;
        public static ConfigEntry<float> milestoneTier3CarryWeight;
        public static ConfigEntry<float> milestoneCarryWeightCap;

        public static ConfigEntry<float> milestoneTier1Stamina;
        public static ConfigEntry<float> milestoneTier2StaminaRegen;
        public static ConfigEntry<float> milestoneTier3Stamina;
        public static ConfigEntry<float> milestoneStaminaCap;

        public static ConfigEntry<float> milestoneTier1Eitr;
        public static ConfigEntry<float> milestoneTier2EitrRegen;
        public static ConfigEntry<float> milestoneTier3Eitr;
        public static ConfigEntry<float> milestoneEitrCap;

        public static ConfigEntry<float> milestoneTier1SwimSpeed;
        public static ConfigEntry<float> milestoneTier2FishSwimStamina;
        public static ConfigEntry<float> milestoneTier3SwimSpeed;

        public static ConfigEntry<float> milestoneTier1Regen;
        public static ConfigEntry<float> milestoneTier2Regen;
        public static ConfigEntry<float> milestoneTier3Regen;

        public static ConfigEntry<int> fatnessTier1Count;
        public static ConfigEntry<int> fatnessTier2Count;
        public static ConfigEntry<int> fatnessTier3Count;

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

        // Note: the Feast Ledger's Puke button - gameplay (it empties the stomach), so synced.
        public static ConfigEntry<bool> allowPukeButton;

        // Note: screen effects - how drunkenness LOOKS on your own monitor. Local on purpose: motion
        // blur is a comfort/accessibility choice, and a server forcing it on would be forcing nausea.
        public static ConfigEntry<bool> drunkMotionBlur;
        public static ConfigEntry<float> drunkBlurStrength;

        // Note: Synergy configs
        public static ConfigEntry<bool> enableSynergies;
        public static ConfigEntry<float> maxEffectDuration;
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
        public static ConfigEntry<int> feastLedgerFontSizeDelta;

        // Note: Feast export - server-side per-player food history, written for BarrkBOT to read.
        // Synced, and server-authoritative on purpose: the SERVER is the only machine that reads
        // these, because it is the one that pulls snapshots and writes the file. See
        // Patches\FeastExportPatches.cs for why the pull direction matters.
        public static ConfigEntry<bool> enableFeastExport;
        public static ConfigEntry<int> feastExportIntervalSeconds;
        public static ConfigEntry<int> feastSyncIntervalSeconds;

        public static void Init(ConfigFile config)
        {
            serverConfigLocked = config.Bind("1 - General", "Lock Configuration", true, "If on, the configuration is locked and can be changed by server admins only.");
            configSync.AddLockingConfigEntry(serverConfigLocked);

            noFoodDecay = BindSynced(config, "2 - Core", "No Food Decay", true,
                "Vanilla fades a food's stats to nothing as its timer runs down. ON = you keep 100% of a food's stats until its timer expires, then lose them all at once. OFF = vanilla's gradual decay curve.");
            maxFoodStacks = BindSynced(config, "2 - Core", "Max Food Stacks", 4,
                "How many times the same food can be eaten at once. Vanilla = 1 (no stacking), 4 = default. Each extra stack is worth less - see section 3.", 1, 20);
            allowPukeButton = BindSynced(config, "2 - Core", "Allow Puke Button", true,
                "ON = the Feast Ledger shows a Puke button that empties every food slot and the drink slot at once (click twice to confirm). Lifetime counts are never refunded - what you ate stays eaten. OFF = the button is hidden; Pukeberries still work as in vanilla.");

            stackDiminishingReturnStep1 = BindSynced(config, "3 - Stacking", "Stack 2 Multiplier", 0.3f,
                "Fraction of a food's stats granted by its 2nd stack. 1.0 = full value, 0.3 = 30% (default), 0.0 = nothing. The 1st stack is always 100%.", 0f, 1f);
            stackDiminishingReturnStep2 = BindSynced(config, "3 - Stacking", "Stack 3 Multiplier", 0.2f,
                "Fraction of a food's stats granted by its 3rd stack. 0.2 = 20% (default).", 0f, 1f);
            stackDiminishingReturnStep3 = BindSynced(config, "3 - Stacking", "Stack 4+ Multiplier", 0.1f,
                "Fraction of a food's stats granted by the 4th and every further stack. 0.1 = 10% (default).", 0f, 1f);

            enableSynergies = BindSynced(config, "4 - Synergies", "Enable Synergies", true,
                "Enables the dietary synergy status effects (Balanced Diet, Sugar Rush, Fisherman's Friend, Lumberjack's Feast).");
            maxEffectDuration = BindSynced(config, "4 - Synergies", "Max Effect Duration", 30f,
                "Hard ceiling, in MINUTES, on how long ONE activation of any food-driven Fatty status effect can last - the four synergies above, the four drink draughts and Drunk. Fatty's own duration multiplier and banked food stacks otherwise push a single activation well past an hour. On reaching the ceiling the effect ends even if your diet still qualifies, and it stays gone until its recipe stops being met and is met again. 30 = 30 minutes (default), 0 = no ceiling. Milestone perks are permanent progression and are never touched by this.", 0f, 240f);
            balancedDietRegen = BindSynced(config, "4 - Synergies", "Balanced Diet Regen Multiplier", 2f,
                "Balanced Diet (1 Meat + 1 Veggie): MULTIPLIER on vanilla health & stamina regen. 1.0 = vanilla, 2.0 = 2x vanilla / +100% (default).", 1f, 10f);
            sugarRushSpeedBonus = BindSynced(config, "4 - Synergies", "Sugar Rush Speed Multiplier", 2f,
                "Sugar Rush (3 Sweets + 1 Drink): MULTIPLIER on vanilla movement speed. 1.0 = vanilla, 2.0 = 2x / +100% (default).", 1f, 5f);
            fishermanSwimStaminaReduction = BindSynced(config, "4 - Synergies", "Fisherman Swim Stamina Reduction", 0.80f,
                "Fisherman's Friend (2 Fish + 1 Veggie): FRACTION of vanilla swim stamina drain removed. 0.0 = vanilla drain, 0.80 = 80% of the drain gone (default), 1.0 = free swimming.", 0f, 1f);
            lumberjackCarryWeight = BindSynced(config, "4 - Synergies", "Lumberjack Carry Weight Bonus", 175f,
                "Lumberjack's Feast (3 Meat + 1 Drink): FLAT carry weight added on top of vanilla. 0 = vanilla, 175 = +175 units (default).", 0f, 1000f);
            lumberjackWoodcuttingDamage = BindSynced(config, "4 - Synergies", "Lumberjack Woodcutting Damage", 0.45f,
                "Lumberjack's Feast (3 Meat + 1 Drink): FRACTION added to vanilla woodcutting damage. 0.0 = vanilla, 0.45 = +45% (default).", 0f, 5f);

            // Vanilla stamina/eitr regen are flat rates that ignore how big the bar is, so Fatty's
            // larger pools made them refill far slower than vanilla. On by default: without it the
            // mod is a straight regen downgrade, worst of all for eitr/mage builds.
            scaleRegenWithPool = BindSynced(config, "6 - Regen", "Scale Regen With Pool", true,
                "Vanilla regenerates stamina & eitr at a FLAT rate that ignores bar size, so any mod that enlarges your pools makes them refill slower. ON = Fatty scales the rate with the pool, holding refill TIME at vanilla parity. OFF = vanilla's flat rate.");
            maxRegenScale = BindSynced(config, "6 - Regen", "Max Regen Scale", 2f,
                "Safety cap on the above, as a MULTIPLIER of vanilla's regen rate. 1.0 = never speed up at all, 2.0 = at most 2x vanilla (default). Fatty never scales BELOW 1.0, so regen can never end up worse than vanilla.", 1f, 20f);

            // Fatty's own food timer extension, so blacks7ar's FoodDurationMultiplier is optional.
            // If that mod IS installed Fatty defers to its value entirely rather than multiplying
            // the two, so nobody running 2x in both silently ends up at 4x.
            foodDurationMultiplier = BindSynced(config, "7 - Duration", "Food Duration Multiplier", 2f,
                "MULTIPLIER on how long food lasts. 1.0 = vanilla duration, 2.0 = food lasts twice as long (default). IGNORED while blacks7ar's FoodDurationMultiplier is installed - that mod's value is used instead, so the two can never multiply together.", 0.1f, 10f);

            enableMilestones = BindSynced(config, "8 - Milestones", "Enable Milestones", true,
                "Eating a lot of a food category over the life of your character permanently raises a stat matched to that category's flavour: Meat->carry weight, Sweet->stamina, Eitr->eitr, Fish->swim speed, Produce (vegetables and fruit)->all-round regen. Total food eaten across EVERY category (a separate 'Fatness' tally) raises base max health. Small, infrequent awards that reward a broad diet. Turning this OFF removes the bonuses but does NOT stop the tally - what you eat still counts, so switching it back on picks up where you left off.");
            milestoneTier1Count = BindSynced(config, "8 - Milestones", "Tier 1 Count", 50,
                "Items of a SINGLE category that must be eaten to earn that category's 1st milestone. Default 50.", 1, 100000);
            milestoneTier2Count = BindSynced(config, "8 - Milestones", "Tier 2 Count", 200,
                "Items of a single category needed for its 2nd milestone. Default 200. Should be higher than Tier 1 Count.", 1, 100000);
            milestoneTier3Count = BindSynced(config, "8 - Milestones", "Tier 3 Count", 500,
                "Items of a single category needed for its 3rd milestone. Default 500. Should be higher than Tier 2 Count.", 1, 100000);

            fatnessTier1Count = BindSynced(config, "8 - Milestones", "Fatness Tier 1 Count", 150,
                "TOTAL items eaten across EVERY category (including unrecognized food) for Fatness tier 1. Separate from the per-category thresholds above since this accumulates ~5x faster. Default 150.", 1, 500000);
            fatnessTier2Count = BindSynced(config, "8 - Milestones", "Fatness Tier 2 Count", 600,
                "Total items eaten across every category for Fatness tier 2. Default 600.", 1, 500000);
            fatnessTier3Count = BindSynced(config, "8 - Milestones", "Fatness Tier 3 Count", 1500,
                "Total items eaten across every category for Fatness tier 3. Default 1500.", 1, 500000);

            milestoneTier1Hp = BindSynced(config, "8 - Milestones", "Fatness Tier 1 Health", 5f,
                "FLAT base max health added by Fatness tier 1 (total food eaten, all categories). 5 = +5 on top of vanilla's 25 base (default).", 0f, 500f);
            milestoneTier2HpRegen = BindSynced(config, "8 - Milestones", "Fatness Tier 2 Health Regen", 0.05f,
                "Fatness tier 2 ALTERNATES to a permanent, always-on health regen MULTIPLIER instead of flat health. 1.0 = vanilla, so this value is added to 1.0 - 0.05 = +5% (default).", 0f, 1f);
            milestoneTier3Hp = BindSynced(config, "8 - Milestones", "Fatness Tier 3 Health", 10f,
                "Flat base max health added by Fatness tier 3 (back to the flat reward). Default +10.", 0f, 500f);
            milestoneTotalCap = BindSynced(config, "8 - Milestones", "Fatness Health Cap", 50f,
                "Hard cap on total base health from the Fatness axis' flat tiers (1 and 3 only - tier 2 is a regen multiplier, uncapped separately). 50 at the defaults (5+10=15 short of it).", 0f, 1000f);

            milestoneTier1CarryWeight = BindSynced(config, "8 - Milestones", "Meat Tier 1 Carry Weight", 10f,
                "FLAT carry weight added by Meat's 1st milestone. Default +10.", 0f, 500f);
            milestoneTier2MeatSkill = BindSynced(config, "8 - Milestones", "Meat Tier 2 Skill Bonus", 10f,
                "Meat tier 2 ALTERNATES to a permanent, always-on Swords + Axes skill level bonus instead of carry weight, matching Warrior's Draught's own flavour. Default +10 levels.", 0f, 100f);
            milestoneTier3CarryWeight = BindSynced(config, "8 - Milestones", "Meat Tier 3 Carry Weight", 20f,
                "Flat carry weight added by Meat's 3rd milestone (back to the flat reward). Default +20.", 0f, 500f);
            milestoneCarryWeightCap = BindSynced(config, "8 - Milestones", "Meat Carry Weight Cap", 40f,
                "Hard cap on total carry weight from Meat's flat tiers (1 and 3 only). Default 40, which leaves 10 of headroom over the 30 those tiers actually grant (10+20) - raise a tier's value without also raising this and the extra is silently clipped.", 0f, 1000f);

            milestoneTier1Stamina = BindSynced(config, "8 - Milestones", "Sweet Tier 1 Stamina", 5f,
                "FLAT base stamina added by Sweet's 1st milestone. Default +5.", 0f, 200f);
            milestoneTier2StaminaRegen = BindSynced(config, "8 - Milestones", "Sweet Tier 2 Stamina Regen", 0.05f,
                "Sweet tier 2 ALTERNATES to a permanent, always-on stamina regen MULTIPLIER instead of flat stamina. Added to 1.0, so 0.05 = +5% (default).", 0f, 1f);
            milestoneTier3Stamina = BindSynced(config, "8 - Milestones", "Sweet Tier 3 Stamina", 10f,
                "Flat base stamina added by Sweet's 3rd milestone (back to the flat reward). Default +10.", 0f, 200f);
            milestoneStaminaCap = BindSynced(config, "8 - Milestones", "Sweet Stamina Cap", 20f,
                "Hard cap on total base stamina from Sweet's flat tiers (1 and 3 only). Default 20, which leaves 5 of headroom over the 15 those tiers actually grant (5+10).", 0f, 500f);

            milestoneTier1Eitr = BindSynced(config, "8 - Milestones", "Eitr Tier 1 Eitr", 5f,
                "FLAT max eitr added by Eitr's 1st milestone. Vanilla has no base eitr at all outside food, so this is meaningful even at low values. Default +5.", 0f, 200f);
            milestoneTier2EitrRegen = BindSynced(config, "8 - Milestones", "Eitr Tier 2 Eitr Regen", 0.05f,
                "Eitr tier 2 ALTERNATES to a permanent, always-on eitr regen MULTIPLIER instead of flat eitr. Added to 1.0, so 0.05 = +5% (default).", 0f, 1f);
            milestoneTier3Eitr = BindSynced(config, "8 - Milestones", "Eitr Tier 3 Eitr", 10f,
                "Flat max eitr added by Eitr's 3rd milestone (back to the flat reward). Default +10.", 0f, 200f);
            milestoneEitrCap = BindSynced(config, "8 - Milestones", "Eitr Eitr Cap", 20f,
                "Hard cap on total max eitr from Eitr's flat tiers (1 and 3 only). Default 20, which leaves 5 of headroom over the 15 those tiers actually grant (5+10).", 0f, 500f);

            milestoneTier1SwimSpeed = BindSynced(config, "8 - Milestones", "Fish Tier 1 Swim Speed", 0.02f,
                "Fish's 1st milestone: permanent, always-on swim speed bonus (additive, like Seafarer's Draught's own modifier). 0.02 = +2% (default).", 0f, 1f);
            milestoneTier2FishSwimStamina = BindSynced(config, "8 - Milestones", "Fish Tier 2 Swim Stamina Reduction", 0.10f,
                "Fish tier 2 ALTERNATES to a permanent, always-on FRACTION of swim stamina drain removed instead of more swim speed, matching Seafarer's/Fisherman's Friend's own flavour. 0.10 = 10% less drain (default).", 0f, 1f);
            milestoneTier3SwimSpeed = BindSynced(config, "8 - Milestones", "Fish Tier 3 Swim Speed", 0.06f,
                "Fish's 3rd milestone swim speed bonus (back to the flat reward, adds to tier 1's). Default +6%.", 0f, 1f);

            // Renamed from "Vegetable Tier N ..." in 1.1.9 when the category widened to cover fruit.
            // BepInEx keys by name, so a renamed entry starts at its default and the old line is
            // left orphaned in the .cfg - anyone who had tuned these three has to re-enter them once.
            milestoneTier1Regen = BindSynced(config, "8 - Milestones", "Produce Tier 1 Regen Multiplier", 1.02f,
                "Produce (vegetables and fruit) 1st milestone: permanent, always-on MULTIPLIER on health, stamina AND eitr regen together. 1.0 = vanilla, 1.02 = +2% (default). Each tier is its own always-on effect, and SE_Stats adds regen bonuses above 1 rather than multiplying them, so earning tiers 1-3 at the defaults gives +12% exactly.", 1f, 2f);
            milestoneTier2Regen = BindSynced(config, "8 - Milestones", "Produce Tier 2 Regen Multiplier", 1.04f,
                "Produce's 2nd milestone regen multiplier. Default 1.04 (+4%).", 1f, 2f);
            milestoneTier3Regen = BindSynced(config, "8 - Milestones", "Produce Tier 3 Regen Multiplier", 1.06f,
                "Produce's 3rd milestone regen multiplier. Default 1.06 (+6%).", 1f, 2f);

            enableDrinkBuffs = BindSynced(config, "9 - Drink Buffs", "Enable Drink Buffs", true,
                "Whatever holds your drink slot grants a buff chosen by its food category, lasting exactly as long as the drink does.");
            drinkBuffSkillLevels = BindSynced(config, "9 - Drink Buffs", "Skill Level Bonus", 15f,
                "FLAT temporary skill levels granted by a drink buff (each buff boosts two related skills). 0 = vanilla, 15 = +15 levels (default).", 0f, 100f);
            drinkBuffEitrRegen = BindSynced(config, "9 - Drink Buffs", "Arcane Eitr Regen Multiplier", 1.5f,
                "Arcane Draught (eitr drinks): MULTIPLIER on vanilla eitr regen. 1.0 = vanilla, 1.5 = +50% (default).", 1f, 10f);
            drinkBuffStaminaRegen = BindSynced(config, "9 - Drink Buffs", "Reveler Stamina Regen Multiplier", 1.5f,
                "Reveler's Draught (sweet drinks): MULTIPLIER on vanilla stamina regen. 1.0 = vanilla, 1.5 = +50% (default).", 1f, 10f);
            drinkBuffMaxHealth = BindSynced(config, "9 - Drink Buffs", "Max Health Per Drink", 5f,
                "FLAT bonus max health while a buffing drink is active. 0 = none, 5 = +5 (default). Follows the same stacking diminishing returns as food, so extra stacks add less.", 0f, 200f);
            drinkSlotDefaultDuration = BindSynced(config, "9 - Drink Buffs", "Default Drink Duration", 600f,
                "MINIMUM seconds a drink holds the drink slot. A health or stamina mead's own effect is over in seconds, which would leave its draught blinking on and off, so drinks are held for at least this long. Drinks that already last longer (the resistance meads) keep their own timer, and food with a real burn time is unaffected. This does NOT let you drink more often - vanilla's cooldown between meads is untouched. 600 = 10 minutes (default). Scaled by the Food Duration Multiplier like everything else.", 10f, 7200f);

            enableDrunk = BindSynced(config, "9 - Drink Buffs", "Enable Drunk", true,
                "Drinking several Tasty Meads at once makes you Drunk: harder hitting and harder to stagger, but your guard suffers. See 'Drunk Stacks Required' for how many.");
            drunkStacksRequired = BindSynced(config, "9 - Drink Buffs", "Drunk Stacks Required", 6,
                "How many Tasty Meads must be in your drink slot at once to get Drunk. Capped by Max Food Stacks, so it can never ask for more meads than your stomach can hold. 6 = default, which sits deliberately ABOVE the default Max Food Stacks of 4 - the cap pulls it back down, so Drunk means 'fill your drink slot with mead' at any stack setting rather than a fixed count that gets easy once you raise the stack limit.", 1, 20);
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
            foodBreakdownPanelX = BindLocal(config, "10 - HUD Panels", "Food Breakdown Panel X", 1f,
                "Saved screen X position (pixels from the left edge) of the Feast Ledger panel. Updated automatically when you drag it. Defaults to the top-left corner - drag it wherever you like, it remembers.");
            foodBreakdownPanelY = BindLocal(config, "10 - HUD Panels", "Food Breakdown Panel Y", 2f,
                "Saved screen Y position (pixels from the top edge) of the Feast Ledger panel. Updated automatically when you drag it.");
            foodBreakdownPanelWidth = BindLocalRanged(config, "10 - HUD Panels", "Food Breakdown Panel Width", 622f,
                "Saved width of the Feast Ledger panel. Updated automatically when you resize it. Range matches the gilt frame's own minimum/maximum - below the minimum the corner ornaments would overlap.", 340f, 900f);
            foodBreakdownPanelHeight = BindLocalRanged(config, "10 - HUD Panels", "Food Breakdown Panel Height", 700f,
                "Saved height of the Feast Ledger panel. Updated automatically when you resize it.", 280f, 700f);
            useSegmentedStackBar = BindLocal(config, "10 - HUD Panels", "Use Segmented Stack Bar", false,
                "How a stacked food slot shows its individual items. ON = a thin segmented bar under the icon, one segment per stacked item, dimming as each expires. OFF = a simple 'xN' count badge instead (default). Only one shows at a time.");
            breakdownToggleKey = config.Bind("10 - HUD Panels", "Breakdown Panel Hotkey", new KeyboardShortcut(KeyCode.F8),
                "Opens/closes the Feast Ledger panel from the keyboard, alongside its own on-screen toggle button. Default F8.");
            feastLedgerFontSizeDelta = config.Bind("10 - HUD Panels", "Feast Ledger Font Size Delta", 2,
                new ConfigDescription(
                    "FLAT points added to every Feast Ledger text size. 0 = the theme's own base sizes, 2 = +2pt (default, easier to read), negative = smaller. Takes effect on the next panel open (styles are built once).",
                    new AcceptableValueRange<int>(-6, 12)));

            // Note: Feast export. OFF by default - this writes a file naming every player and what
            // they eat, so it is opt-in rather than something a server starts doing unannounced.
            enableFeastExport = BindSynced(config, "11 - Feast Export", "Enable Feast Export", false,
                "OFF by default. ON = the SERVER collects every player's lifetime food counts and writes them to BepInEx\\config\\Fatty\\BarrkBOT_feasts.json, for a Discord bot or web dashboard to read. Existing history is included the first time a player syncs, so nobody starts from zero. OFF = nothing is collected, nothing is sent and no file is created. Only the SERVER's copy of this setting does anything.");
            feastExportIntervalSeconds = BindSynced(config, "11 - Feast Export", "Feast Export Interval Seconds", 60,
                "How often the server rewrites the export file, in seconds. 60 = default. Only matters when Enable Feast Export is on.", 10, 3600);
            feastSyncIntervalSeconds = BindSynced(config, "11 - Feast Export", "Feast Sync Interval Seconds", 300,
                "How often the server asks each connected player for a fresh copy of their food counts, in seconds. 300 = default (five minutes); this is a leaderboard, not telemetry, so there is no reason to ask often. A player who has just joined is always asked straight away regardless of this.", 30, 3600);

            drunkMotionBlur = BindLocal(config, "12 - Effects", "Drunk Motion Blur", true,
                "ON = while you are Drunk the screen smears with heavy motion blur and ghost trails, fading in as you get drunk and out as you sober up. Works even with Motion Blur off in Valheim's graphics settings, and puts your own setting back afterwards. OFF = Drunk changes nothing on screen.");
            drunkBlurStrength = BindLocalRanged(config, "12 - Effects", "Drunk Blur Strength", 0.6f,
                "How heavy the drunk blur is. 0.0 = barely there, 0.6 = default, 1.0 = the room will not stop moving. Takes effect live.", 0f, 1f);

            // Carry across anything the 1.1.9 rename would otherwise have silently reset to default.
            MigrateRenamedKeys(config);
        }

        // ------------------------------------------------------------------
        // Renamed-key migration.
        //
        // BepInEx keys a setting by its (section, key) name. Rename the key and Bind() finds nothing,
        // so the entry starts at its DEFAULT and the player's tuned value is left behind in the .cfg
        // as an orphan - silently, with no error and nothing on screen. That is a real cost to pay
        // for a cosmetic rename, so the value is fetched back out and re-applied here.
        //
        // ConfigFile keeps every line it read but nothing bound to in its OrphanedEntries dictionary
        // (ConfigDefinition -> the raw, unparsed string). It is reached by reflection rather than
        // referenced directly: its accessibility is not part of BepInEx's public contract and has no
        // guarantee of staying put across versions, and a rename that fails to migrate has to stay a
        // one-line log message rather than taking the whole config - and with it the mod - down.
        //
        // Removing the orphan afterwards is what stops the dead line being written back out on the
        // next save, so the .cfg ends up with exactly one entry per setting.
        // ------------------------------------------------------------------
        private static void MigrateRenamedKeys(ConfigFile config)
        {
            // 1.1.9: the Vegetable category widened to cover fruit and became Produce.
            bool removedAny = false;
            removedAny |= TryMigrateKey(config, "8 - Milestones", "Vegetable Tier 1 Regen Multiplier", milestoneTier1Regen);
            removedAny |= TryMigrateKey(config, "8 - Milestones", "Vegetable Tier 2 Regen Multiplier", milestoneTier2Regen);
            removedAny |= TryMigrateKey(config, "8 - Milestones", "Vegetable Tier 3 Regen Multiplier", milestoneTier3Regen);

            // Dropping the orphan from the in-memory dictionary only reaches the .cfg on the next
            // write, and nothing here is guaranteed to cause one: assigning ConfigEntry.Value does
            // save, but that path is skipped whenever the old value already equals the new default -
            // which is the common case, since most people never touch these. Without this the dead
            // lines survive every launch and the file ends up listing both names forever.
            if (removedAny) config.Save();
        }

        private static bool TryMigrateKey(ConfigFile config, string section, string oldKey, ConfigEntry<float> target)
        {
            if (config == null || target == null) return false;

            try
            {
                var property = typeof(ConfigFile).GetProperty("OrphanedEntries",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property == null) return false;

                var orphans = property.GetValue(config, null) as System.Collections.IDictionary;
                if (orphans == null || orphans.Count == 0) return false;

                var oldDefinition = new ConfigDefinition(section, oldKey);
                if (!orphans.Contains(oldDefinition)) return false;

                string raw = orphans[oldDefinition] as string;
                orphans.Remove(oldDefinition); // drop the dead line either way - it is never coming back

                // A value we can't parse is not worth guessing at: the entry keeps its default and
                // the player is told, rather than being handed a silently wrong regen multiplier.
                if (!float.TryParse(raw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value))
                {
                    FattyPlugin.Instance.Log.LogWarning(
                        $"Fatty: could not read the old '{oldKey}' setting (value was '{raw}'); '{target.Definition.Key}' keeps its default.");
                    return true;
                }

                // Range-clamped rather than assigned raw. The old entry's AcceptableValueRange is
                // gone with it, so this is the one path into these values that isn't already
                // range-checked at bind time.
                var range = target.Description?.AcceptableValues as AcceptableValueRange<float>;
                if (range != null) value = Mathf.Clamp(value, range.MinValue, range.MaxValue);

                // Already equal (the usual case - most people never touch these), so there is nothing
                // to carry across. Still reports true: the orphan WAS removed and that removal has
                // to be flushed to disk by the caller.
                if (Mathf.Approximately(value, target.Value)) return true;

                target.Value = value;
                FattyPlugin.Instance.Log.LogInfo(
                    $"Fatty: carried your '{oldKey}' setting ({value}) across to '{target.Definition.Key}' after the 1.1.9 rename.");
                return true;
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning(
                    $"Fatty: could not migrate the renamed setting '{oldKey}' ({ex.Message}). '{target.Definition.Key}' keeps its default.");
                return false;
            }
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
