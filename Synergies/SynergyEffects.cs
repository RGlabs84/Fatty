using HarmonyLib;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Synergies
{
    // Builds the four synergy StatusEffects and registers them in the ObjectDB so
    // SEMan.AddStatusEffect(hash) can resolve them.
    public static class SynergyEffects
    {
        public static int BalancedDietHash { get; private set; }
        public static int SugarRushHash { get; private set; }
        public static int FishermanHash { get; private set; }
        public static int LumberjackHash { get; private set; }

        private static bool _built;
        private static SE_Stats _balanced, _sugar, _fisher, _lumber;

        // Exceptions bubble up to the ObjectDB patch postfixes, which log the feature-loss.
        public static void Register(ObjectDB odb)
        {
            if (odb == null || odb.m_StatusEffects == null) return;

            if (!_built) BuildEffects();

            // Re-read the config EVERY time, not just when the effects are first created. Building
            // happens on the first ObjectDB.Awake (main menu), which is before ServerSync has
            // delivered the server's values on connect - so baking them in at build time meant
            // every synergy value silently stayed client-local despite being registered as synced.
            // CopyOtherDB fires later (in-game), which is when the server's values are actually in.
            ApplyConfigValues();

            AddIfMissing(odb, _balanced);
            AddIfMissing(odb, _sugar);
            AddIfMissing(odb, _fisher);
            AddIfMissing(odb, _lumber);
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
            se.m_name = displayName;               // NameHash() hashes this; also shown in the HUD
            se.m_tooltip = tooltip;
            se.m_startMessage = displayName;
            se.m_startMessageType = MessageHud.MessageType.TopLeft;
            se.m_ttl = 0f;                          // infinite; lifecycle is controlled by the current diet

            // IMPORTANT: SE_Stats multiplies regen by these when <= 1, so a default of 0 would ZERO
            // the player's regen while active. Baseline them to 1 (no change) on every effect.
            se.m_healthRegenMultiplier = 1f;
            se.m_staminaRegenMultiplier = 1f;
            se.m_eitrRegenMultiplier = 1f;

            // Same trap, different field: m_damageModifier is a MULTIPLIER applied to every damage
            // channel, so 1 means "unchanged" and 0 would delete all damage.
            se.m_damageModifier = 1f;
            return se;
        }

        // Creates the four ScriptableObjects and everything that never changes (name, icon, hash).
        // Config-derived values deliberately live in ApplyConfigValues so they can be refreshed.
        private static void BuildEffects()
        {
            // Balanced Diet: 1 Meat + 1 Veggie -> big health & stamina regen.
            _balanced = CreateBase("SE_Fatty_BalancedDiet", "Balanced Diet",
                "A well-rounded meal.\nHealth & Stamina regeneration greatly increased.");
            _balanced.m_icon = SynergyIcons.Resolve("BalancedDiet", SynergyIcons.BalancedDiet);
            BalancedDietHash = _balanced.NameHash();

            // Sugar Rush: 3 Sweets + 1 Drink -> movement speed.
            _sugar = CreateBase("SE_Fatty_SugarRush", "Sugar Rush",
                "Buzzing with energy!\nMovement speed greatly increased.");
            _sugar.m_icon = SynergyIcons.Resolve("SugarRush", SynergyIcons.SugarRush);
            SugarRushHash = _sugar.NameHash();

            // Fisherman's Friend: 2 Fish + 1 Veggie -> swim speed & reduced swim stamina drain.
            _fisher = CreateBase("SE_Fatty_Fisherman", "Fisherman's Friend",
                "One with the water.\nImproved swim speed and reduced swim stamina use.");
            _fisher.m_swimSpeedModifier = 0.10f;
            _fisher.m_icon = SynergyIcons.Resolve("Fisherman", SynergyIcons.Fisherman);
            FishermanHash = _fisher.NameHash();

            // Lumberjack's Feast: 3 Meat + 1 Drink -> carry weight & woodcutting damage.
            _lumber = CreateBase("SE_Fatty_Lumberjack", "Lumberjack's Feast",
                "Fed and ready to fell forests.\nBonus carry weight and woodcutting damage.");
            _lumber.m_modifyAttackSkill = Skills.SkillType.WoodCutting;
            _lumber.m_icon = SynergyIcons.Resolve("Lumberjack", SynergyIcons.Lumberjack);
            LumberjackHash = _lumber.NameHash();

            _built = true;
        }

        // Every value that comes from config, re-applied on each Register() so a server's synced
        // values actually reach the live effects. Safe to call repeatedly.
        private static void ApplyConfigValues()
        {
            if (_balanced != null)
            {
                // No Mathf.Max(1f, ...) clamp here: SE_Stats treats <= 1 multiplicatively, which is
                // exactly how a sub-1.0 nerf value is meant to work. Clamping made the documented
                // nerf range impossible to configure.
                float regen = ConfigManager.balancedDietRegen.Value;
                _balanced.m_healthRegenMultiplier = regen;
                _balanced.m_staminaRegenMultiplier = regen;
            }

            if (_sugar != null)
                _sugar.m_speedModifier = Mathf.Max(0f, ConfigManager.sugarRushSpeedBonus.Value - 1f);

            if (_fisher != null)
                _fisher.m_swimStaminaUseModifier = -Mathf.Clamp01(ConfigManager.fishermanSwimStaminaReduction.Value);

            if (_lumber != null)
            {
                _lumber.m_addMaxCarryWeight = ConfigManager.lumberjackCarryWeight.Value;

                // m_damageModifier is MULTIPLICATIVE - SE_Stats.ModifyAttack calls
                // hitData.m_damage.Modify(m_damageModifier), and DamageTypes.Modify(float)
                // multiplies every damage channel. Assigning the raw config value meant the default
                // 0.45 was a 55% woodcutting damage NERF, not the +45% the description promised.
                _lumber.m_damageModifier = 1f + ConfigManager.lumberjackWoodcuttingDamage.Value;
            }
        }
    }

    // Register on both ObjectDB.Awake (menu DB) and CopyOtherDB (the full in-game DB copy),
    // because CopyOtherDB rebuilds the status-effect list and would drop our additions otherwise.
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ObjectDB_Awake_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ObjectDB __instance)
        {
            try
            {
                SynergyEffects.Register(__instance);
                DrinkBuffs.Register(__instance);
                Fatty.Progression.MilestonePerks.Register(__instance);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Synergy Registration (ObjectDB.Awake). Reason: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    public static class ObjectDB_CopyOtherDB_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ObjectDB __instance)
        {
            try
            {
                SynergyEffects.Register(__instance);
                DrinkBuffs.Register(__instance);
                Fatty.Progression.MilestonePerks.Register(__instance);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Synergy Registration (ObjectDB.CopyOtherDB). Reason: {ex}");
            }
        }
    }
}
