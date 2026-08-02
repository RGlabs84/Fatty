using System.Collections.Generic;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Synergies
{
    // ---------------------------------------------------------------------
    // Mead & drink buffs. Vikings love their mead.
    //
    // A buff is active for exactly as long as a qualifying drink occupies the dedicated drink slot,
    // so it expires with the drink and needs no timer of its own - the same add/remove model the
    // dietary synergies already use. Which buff you get is driven by the drink's DIET category, so
    // an eitr mead gives the arcane draught and a meat broth gives the warrior's.
    //
    // Two vanilla footguns this code is written around, both confirmed by decompiling SE_Stats:
    //   * m_damageModifier is MULTIPLICATIVE - ModifyAttack calls hitData.m_damage.Modify(x), and
    //     DamageTypes.Modify(float) multiplies every damage channel. "+15%" must be written 1.15f,
    //     not 0.15f.
    //   * m_skillLevel2 is dead unless m_skillLevel is also set - ModifySkillLevel nests the second
    //     check inside `if (m_skillLevel != 0)`. Always assign both slots.
    // ---------------------------------------------------------------------
    public static class DrinkBuffs
    {
        // Vanilla Tasty Mead. Drunk is deliberately this prefab only - it is THE generic "just
        // drinking" mead, so over-committing the drink slot to it is what gets you drunk.
        public const string TastyMeadPrefab = "MeadTasty";

        private static bool _built;
        private static readonly Dictionary<string, SE_Stats> _byCategory = new Dictionary<string, SE_Stats>();
        private static readonly Dictionary<string, int> _hashByCategory = new Dictionary<string, int>();
        private static SE_Stats _drunk;
        public static int DrunkHash { get; private set; }

        public static IEnumerable<int> AllHashes
        {
            get
            {
                foreach (var h in _hashByCategory.Values) yield return h;
                if (DrunkHash != 0) yield return DrunkHash;
            }
        }

        public static void Register(ObjectDB odb)
        {
            if (odb == null || odb.m_StatusEffects == null) return;

            if (!_built) Build();
            ApplyConfigValues();

            foreach (var se in _byCategory.Values) AddIfMissing(odb, se);
            AddIfMissing(odb, _drunk);
        }

        private static void AddIfMissing(ObjectDB odb, StatusEffect se)
        {
            if (se == null) return;
            if (odb.GetStatusEffect(se.NameHash()) == null)
                odb.m_StatusEffects.Add(se);
        }

        private static SE_Stats CreateBase(string internalName, string displayName, string tooltip)
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = internalName;
            se.m_name = displayName;
            se.m_tooltip = tooltip;
            se.m_startMessage = displayName;
            se.m_startMessageType = MessageHud.MessageType.TopLeft;
            se.m_ttl = 0f; // infinite; lifecycle is the drink in the drink slot

            // SE_Stats multiplies regen by these when <= 1, so a default of 0 would ZERO the
            // player's regen while active. Baseline them to 1 (no change) on every effect.
            se.m_healthRegenMultiplier = 1f;
            se.m_staminaRegenMultiplier = 1f;
            se.m_eitrRegenMultiplier = 1f;

            // Multiplicative: 1 = unchanged. Leaving this at 0 would delete all damage.
            se.m_damageModifier = 1f;
            return se;
        }

        private static void Build()
        {
            var arcane = CreateBase("SE_Fatty_ArcaneDraught", "Arcane Draught",
                "The old magic runs in you.\nEitr regeneration and magic skills improved.");
            arcane.m_icon = SynergyIcons.Resolve("ArcaneDraught", SynergyIcons.ArcaneDraught);
            Add("Eitr", arcane);

            var warrior = CreateBase("SE_Fatty_WarriorsDraught", "Warrior's Draught",
                "Fed for the fight.\nSword and axe skill improved.");
            warrior.m_icon = SynergyIcons.Resolve("WarriorsDraught", SynergyIcons.WarriorsDraught);
            Add("Meat", warrior);

            var reveler = CreateBase("SE_Fatty_RevelersDraught", "Reveler's Draught",
                "The feast is on.\nStamina regeneration, running and jumping improved.");
            reveler.m_icon = SynergyIcons.Resolve("RevelersDraught", SynergyIcons.RevelersDraught);
            Add("Sweet", reveler);

            var seafarer = CreateBase("SE_Fatty_SeafarersDraught", "Seafarer's Draught",
                "Salt in your blood.\nSwimming and fishing improved.");
            seafarer.m_swimSpeedModifier = 0.10f;
            seafarer.m_icon = SynergyIcons.Resolve("SeafarersDraught", SynergyIcons.SeafarersDraught);
            Add("Fish", seafarer);

            _drunk = CreateBase("SE_Fatty_Drunk", "Drunk",
                "Skal!\nHarder hitting and harder to stagger - but your guard is a rumour.");
            _drunk.m_icon = SynergyIcons.Resolve("Drunk", SynergyIcons.Drunk);
            DrunkHash = _drunk.NameHash();

            _built = true;
        }

        private static void Add(string category, SE_Stats se)
        {
            _byCategory[category] = se;
            _hashByCategory[category] = se.NameHash();
        }

        // Re-read every config-derived value. Called on each Register() because ObjectDB.Awake fires
        // at the main menu, before ServerSync has delivered the server's values on connect.
        private static void ApplyConfigValues()
        {
            float skill = ConfigManager.drinkBuffSkillLevels.Value;

            if (_byCategory.TryGetValue("Eitr", out var arcane))
            {
                arcane.m_eitrRegenMultiplier = Mathf.Max(0.01f, ConfigManager.drinkBuffEitrRegen.Value);
                arcane.m_skillLevel = Skills.SkillType.ElementalMagic;
                arcane.m_skillLevelModifier = skill;
                arcane.m_skillLevel2 = Skills.SkillType.BloodMagic;
                arcane.m_skillLevelModifier2 = skill;
            }

            if (_byCategory.TryGetValue("Meat", out var warrior))
            {
                warrior.m_skillLevel = Skills.SkillType.Swords;
                warrior.m_skillLevelModifier = skill;
                warrior.m_skillLevel2 = Skills.SkillType.Axes;
                warrior.m_skillLevelModifier2 = skill;
            }

            if (_byCategory.TryGetValue("Sweet", out var reveler))
            {
                reveler.m_staminaRegenMultiplier = Mathf.Max(0.01f, ConfigManager.drinkBuffStaminaRegen.Value);
                reveler.m_skillLevel = Skills.SkillType.Run;
                reveler.m_skillLevelModifier = skill;
                reveler.m_skillLevel2 = Skills.SkillType.Jump;
                reveler.m_skillLevelModifier2 = skill;
            }

            if (_byCategory.TryGetValue("Fish", out var seafarer))
            {
                seafarer.m_skillLevel = Skills.SkillType.Swim;
                seafarer.m_skillLevelModifier = skill;
                seafarer.m_skillLevel2 = Skills.SkillType.Fishing;
                seafarer.m_skillLevelModifier2 = skill;
            }

            if (_drunk != null)
            {
                // Boon: harder hitting, harder to stagger. m_damageModifier is a MULTIPLIER, hence 1 + x.
                _drunk.m_modifyAttackSkill = Skills.SkillType.All;
                _drunk.m_damageModifier = 1f + Mathf.Max(0f, ConfigManager.drunkDamageBonus.Value);
                _drunk.m_staggerModifier = -Mathf.Clamp01(ConfigManager.drunkStaggerResist.Value);

                // Cost: your guard falls apart. Blocking skill down, blocking costs more stamina.
                _drunk.m_skillLevel = Skills.SkillType.Blocking;
                _drunk.m_skillLevelModifier = -Mathf.Abs(ConfigManager.drunkBlockingPenalty.Value);
                _drunk.m_skillLevel2 = Skills.SkillType.Blocking;
                _drunk.m_skillLevelModifier2 = 0f;
                _drunk.m_blockStaminaUseModifier = Mathf.Max(0f, ConfigManager.drunkBlockStaminaPenalty.Value);
            }
        }

        // ------------------------------------------------------------------
        // Per-tick evaluation, driven from SynergyManager's UpdateFood postfix.
        // ------------------------------------------------------------------
        public static void Evaluate(Player player, SEMan seman, List<Player.Food> foods)
        {
            if (seman == null) return;

            bool enabled = ConfigManager.enableDrinkBuffs.Value;

            string activeCategory = null;
            int tastyMeadStacks = 0;

            if (enabled && foods != null)
            {
                foreach (Player.Food food in foods)
                {
                    if (food == null || food.m_item == null) continue;

                    string prefab = Patches.PlayerPatches.ResolvePrefabName(food);
                    if (!CategoryManager.IsDrinkPrefab(prefab)) continue;

                    if (prefab == TastyMeadPrefab) tastyMeadStacks++;

                    // Only one drink type can hold the slot, so the first one found decides the buff.
                    if (activeCategory == null)
                    {
                        string cat = CategoryManager.GetCategoryForPrefab(prefab);
                        if (_hashByCategory.ContainsKey(cat)) activeCategory = cat;
                    }
                }
            }

            foreach (var kvp in _hashByCategory)
            {
                string cat = kvp.Key;
                bool active = enabled && cat == activeCategory;

                // The draught lasts exactly as long as a drink of this category holds the slot -
                // i.e. until the last of them expires. NthLargestRemaining projects Fatty's
                // sequential stack decay, so banked stacks of the same drink are counted one after
                // another rather than in parallel.
                float remaining = active
                    ? SynergyManager.NthLargestRemaining(foods,
                        prefab => CategoryManager.IsDrinkPrefab(prefab) && CategoryManager.GetCategoryForPrefab(prefab) == cat, 1)
                    : 0f;

                SynergyManager.ApplyTimedEffect(seman, kvp.Value, active, remaining);
            }

            // Drunk: Tasty Mead only, and only once you've committed enough of the drink slot to it.
            //
            // Deliberately its OWN threshold rather than maxFoodStacks. Keying it to the stack cap
            // made the requirement move with an unrelated setting - and move the wrong way: a player
            // who raised Max Food Stacks to 7 needed SEVEN Tasty Meads at once, which is why Drunk
            // was unreachable in playtesting. Clamped to the cap so it can never ask for more meads
            // than the stomach is allowed to hold.
            int required = Mathf.Clamp(ConfigManager.drunkStacksRequired.Value, 1, Mathf.Max(1, ConfigManager.maxFoodStacks.Value));
            bool drunk = enabled
                && ConfigManager.enableDrunk.Value
                && tastyMeadStacks >= required;

            // Drunk ends when the stack count falls back under the threshold, which is when the
            // required-th longest-lived Tasty Mead expires. Every stack here is the same prefab, so
            // they expire strictly one after another - the projected times NthLargestRemaining
            // returns, not their raw m_time, which for a parked stack never moves at all.
            float drunkRemaining = drunk
                ? SynergyManager.NthLargestRemaining(foods, prefab => prefab == TastyMeadPrefab, required)
                : 0f;

            SynergyManager.ApplyTimedEffect(seman, DrunkHash, drunk, drunkRemaining);
        }

        // ------------------------------------------------------------------
        // Max-HP bonus, applied through Fatty's GetTotalFoodValue delta rather than the status
        // effect - SE_Stats has no m_addMaxHealth field, so max stats can only be reached through
        // GetTotalFoodValue. Scales with stacks on the same diminishing-returns ladder as every
        // other stat, and expires with the drink because it is recomputed every tick.
        // ------------------------------------------------------------------
        public static float GetMaxHealthBonus(string prefabName, float stackMultiplier)
        {
            if (!ConfigManager.enableDrinkBuffs.Value) return 0f;
            if (!CategoryManager.IsDrinkPrefab(prefabName)) return 0f;

            string cat = CategoryManager.GetCategoryForPrefab(prefabName);
            if (!_hashByCategory.ContainsKey(cat)) return 0f;

            return ConfigManager.drinkBuffMaxHealth.Value * stackMultiplier;
        }
    }
}
