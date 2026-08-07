using UnityEngine;
using Fatty.Configuration;
using Fatty.Synergies;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // Permanent milestone perks for the reward types with no vanilla "base pool" field to write into
    // (unlike Meat/Sweet/Eitr/Fatness's flat tiers - see FoodMilestones): Fish's swim-speed tiers,
    // Produce's all-round-regen tiers, and every category's tier-2 "alternate" reward (Eitr/Sweet/
    // Fatness's regen%, Meat's skill bonus, Fish's swim-stamina reduction). Modeled as SE_Stats
    // effects, same tool DrinkBuffs/SynergyEffects already use for non-pool bonuses, but ALWAYS ON
    // once earned rather than gated on current stomach contents.
    //
    // Fish and Produce get three separate effects each, one per tier (not one effect whose fields
    // get rewritten), because Valheim already combines multiple simultaneously-active SE_Stats
    // correctly, which is what makes earning tiers 1-3 cumulative for free.
    //
    // How they combine, verified against the decompile - NOT what it looks like: SE_Stats'
    // ModifyHealth/Stamina/EitrRegen are `if (m > 1f) mult += m - 1f; else mult *= m;`. So regen
    // bonuses ABOVE 1 are ADDITIVE, not multiplicative. Veg tiers 1.02/1.04/1.06 therefore total
    // 1 + .02 + .04 + .06 = +12% exactly, not 1.02*1.04*1.06. Swim speed sums (`speed += baseSpeed *
    // m_swimSpeedModifier`). Only the <= 1 branch multiplies, which is what makes leaving a regen
    // field at 0 a stat-wipe rather than a no-op.
    //
    // The five tier-2-only alternate effects need just ONE effect each, since only a single tier
    // ever grants them - simple on/off, no stacking involved.
    //
    // Deliberately no m_startMessage on any of these: FoodMilestones.Apply() re-syncs every one of
    // these on every respawn (fresh Player -> fresh SEMan -> effect starts inactive again), and
    // AddStatusEffect fires m_startMessage on every inactive->active transition. A lifetime unlock
    // toasting itself on every death would be noise, not a reward - the one genuine announcement
    // happens through FoodMilestones' own tier-crossing detection instead.
    // ---------------------------------------------------------------------
    public static class MilestonePerks
    {
        private static bool _built;
        private static SE_Stats _fish1, _fish2, _fish3;
        private static SE_Stats _veg1, _veg2, _veg3;
        private static SE_Stats _meatSkill, _staminaRegen, _eitrRegen, _hpRegen;

        public static void Register(ObjectDB odb)
        {
            if (odb == null || odb.m_StatusEffects == null) return;

            if (!_built) Build();
            ApplyConfigValues();

            AddIfMissing(odb, _fish1);
            AddIfMissing(odb, _fish2);
            AddIfMissing(odb, _fish3);
            AddIfMissing(odb, _veg1);
            AddIfMissing(odb, _veg2);
            AddIfMissing(odb, _veg3);
            AddIfMissing(odb, _meatSkill);
            AddIfMissing(odb, _staminaRegen);
            AddIfMissing(odb, _eitrRegen);
            AddIfMissing(odb, _hpRegen);
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
            se.m_startMessageType = MessageHud.MessageType.TopLeft; // set for completeness; m_startMessage stays unset (see header)
            se.m_ttl = 0f; // infinite; lifecycle is add/remove from FoodMilestones.Apply(), not a timer

            // SE_Stats multiplies regen by these when <= 1 and multiplies damage by m_damageModifier,
            // so leaving either at its field default of 0 would zero the stat instead of leaving it
            // unchanged. Baseline every effect to "no change" - same trap DrinkBuffs/SynergyEffects
            // already guard against.
            se.m_healthRegenMultiplier = 1f;
            se.m_staminaRegenMultiplier = 1f;
            se.m_eitrRegenMultiplier = 1f;
            se.m_damageModifier = 1f;
            return se;
        }

        private static void Build()
        {
            _fish1 = CreateBase("SE_Fatty_MilestoneFish1", "Sea Legs I", "A lifetime of fish.\nSwim speed permanently improved.");
            _fish1.m_icon = SynergyIcons.Resolve("MilestoneFish", SynergyIcons.Fisherman);
            // Tier 2 is Fish's alternate reward - swim stamina reduction instead of more swim speed
            // (see FoodMilestones' header comment) - so this effect sets m_swimStaminaUseModifier,
            // not m_swimSpeedModifier like its tier-1/3 siblings.
            _fish2 = CreateBase("SE_Fatty_MilestoneFish2", "Sea Legs II", "Practically part fish by now.\nSwim stamina cost permanently reduced.");
            _fish2.m_icon = SynergyIcons.Resolve("MilestoneFish", SynergyIcons.Fisherman);
            _fish3 = CreateBase("SE_Fatty_MilestoneFish3", "Sea Legs III", "The sea knows your name.\nSwim speed permanently improved further.");
            _fish3.m_icon = SynergyIcons.Resolve("MilestoneFish", SynergyIcons.Fisherman);

            // Internal names stay "...Veg..." deliberately even though the category is now Produce:
            // se.name feeds NameHash(), which is the identity SEMan/ObjectDB key these effects by.
            // Renaming them would orphan the copy already active on a character mid-session for no
            // player-visible gain - the displayed name and tooltip are what anyone actually reads.
            _veg1 = CreateBase("SE_Fatty_MilestoneVeg1", "Well Nourished I", "A lifetime of greens and orchard fruit.\nHealth, stamina and eitr regeneration permanently improved.");
            _veg1.m_icon = SynergyIcons.Resolve("MilestoneVeg", SynergyIcons.BalancedDiet);
            _veg2 = CreateBase("SE_Fatty_MilestoneVeg2", "Well Nourished II", "Your diet is the envy of the village.\nRegeneration permanently improved further.");
            _veg2.m_icon = SynergyIcons.Resolve("MilestoneVeg", SynergyIcons.BalancedDiet);
            _veg3 = CreateBase("SE_Fatty_MilestoneVeg3", "Well Nourished III", "A truly balanced life.\nRegeneration permanently improved further still.");
            _veg3.m_icon = SynergyIcons.Resolve("MilestoneVeg", SynergyIcons.BalancedDiet);

            // The five tier-2-only alternate rewards - each a single always-on effect, mirroring the
            // flavour of that category's own drink buff/synergy (Warrior's Draught = Swords+Axes,
            // Arcane Draught = eitr regen, Reveler's = stamina regen, Seafarer's/Fisherman's = swim
            // stamina reduction).
            _meatSkill = CreateBase("SE_Fatty_MilestoneMeatSkill", "Warrior's Build", "A lifetime of meat.\nSword and axe skill permanently improved.");
            _meatSkill.m_icon = SynergyIcons.Resolve("MilestoneMeat", SynergyIcons.WarriorsDraught);
            _staminaRegen = CreateBase("SE_Fatty_MilestoneStaminaRegen", "Boundless Energy", "A lifetime of sweets.\nStamina regeneration permanently improved.");
            _staminaRegen.m_icon = SynergyIcons.Resolve("MilestoneSweet", SynergyIcons.RevelersDraught);
            _eitrRegen = CreateBase("SE_Fatty_MilestoneEitrRegen", "Arcane Constitution", "A lifetime of eitr food.\nEitr regeneration permanently improved.");
            _eitrRegen.m_icon = SynergyIcons.Resolve("MilestoneEitr", SynergyIcons.ArcaneDraught);
            _hpRegen = CreateBase("SE_Fatty_MilestoneHpRegen", "Iron Constitution", "A lifetime of eating well.\nHealth regeneration permanently improved.");
            _hpRegen.m_icon = SynergyIcons.Resolve("MilestoneFatness", SynergyIcons.BalancedDiet);

            _built = true;
        }

        // Re-read every tick config could plausibly change on (server sync arriving late, etc.),
        // mirrors DrinkBuffs/SynergyEffects' own ApplyConfigValues.
        private static void ApplyConfigValues()
        {
            if (_fish1 != null) _fish1.m_swimSpeedModifier = Mathf.Max(0f, ConfigManager.milestoneTier1SwimSpeed.Value);
            if (_fish2 != null) _fish2.m_swimStaminaUseModifier = -Mathf.Clamp01(ConfigManager.milestoneTier2FishSwimStamina.Value);
            if (_fish3 != null) _fish3.m_swimSpeedModifier = Mathf.Max(0f, ConfigManager.milestoneTier3SwimSpeed.Value);

            if (_veg1 != null) SetRegen(_veg1, ConfigManager.milestoneTier1Regen.Value);
            if (_veg2 != null) SetRegen(_veg2, ConfigManager.milestoneTier2Regen.Value);
            if (_veg3 != null) SetRegen(_veg3, ConfigManager.milestoneTier3Regen.Value);

            if (_meatSkill != null)
            {
                float lvl = ConfigManager.milestoneTier2MeatSkill.Value;
                _meatSkill.m_skillLevel = Skills.SkillType.Swords;
                _meatSkill.m_skillLevelModifier = lvl;
                _meatSkill.m_skillLevel2 = Skills.SkillType.Axes;
                _meatSkill.m_skillLevelModifier2 = lvl;
            }
            if (_staminaRegen != null) _staminaRegen.m_staminaRegenMultiplier = Mathf.Max(0.01f, 1f + ConfigManager.milestoneTier2StaminaRegen.Value);
            if (_eitrRegen != null) _eitrRegen.m_eitrRegenMultiplier = Mathf.Max(0.01f, 1f + ConfigManager.milestoneTier2EitrRegen.Value);
            if (_hpRegen != null) _hpRegen.m_healthRegenMultiplier = Mathf.Max(0.01f, 1f + ConfigManager.milestoneTier2HpRegen.Value);
        }

        private static void SetRegen(SE_Stats se, float multiplier)
        {
            float m = Mathf.Max(0.01f, multiplier);
            se.m_healthRegenMultiplier = m;
            se.m_staminaRegenMultiplier = m;
            se.m_eitrRegenMultiplier = m;
        }

        // Called from FoodMilestones.Apply() - on every eat-triggered re-apply AND every respawn,
        // never per-UpdateFood-tick, since these are permanent lifetime unlocks, not diet-current
        // buffs. Idempotent: safe to call every time regardless of whether anything changed.
        public static void Sync(Player player, int fishTiers, int vegTiers,
            bool meatSkillActive, bool staminaRegenActive, bool eitrRegenActive, bool hpRegenActive)
        {
            SEMan seman = player?.GetSEMan();
            if (seman == null) return;

            // Re-read config before touching anything. Register() is the only other caller of this,
            // and it runs on ObjectDB.Awake/CopyOtherDB only - both of which can land BEFORE
            // ServerSync delivers the server's values on connect. Sync runs on every eat and every
            // respawn, so this is what actually gets a server's values onto these effects.
            ApplyConfigValues();

            ApplyEffect(seman, _fish1, fishTiers >= 1);
            ApplyEffect(seman, _fish2, fishTiers >= 2);
            ApplyEffect(seman, _fish3, fishTiers >= 3);
            ApplyEffect(seman, _veg1, vegTiers >= 1);
            ApplyEffect(seman, _veg2, vegTiers >= 2);
            ApplyEffect(seman, _veg3, vegTiers >= 3);

            ApplyEffect(seman, _meatSkill, meatSkillActive);
            ApplyEffect(seman, _staminaRegen, staminaRegenActive);
            ApplyEffect(seman, _eitrRegen, eitrRegenActive);
            ApplyEffect(seman, _hpRegen, hpRegenActive);
        }

        // Adds, removes, or - the part the plain add/remove pattern misses - REFRESHES an effect
        // that is already active.
        //
        // SEMan.AddStatusEffect stores a Clone() of the ObjectDB template, not the template itself
        // (decompile: `StatusEffect statusEffect3 = statusEffect.Clone();`). So mutating the
        // template in ApplyConfigValues never reaches an effect already on the player.
        //
        // The diet-driven Synergy/DrinkBuffs systems get away with plain add/remove because their
        // effects come and go with your stomach, re-cloning current values within seconds. These
        // perks are PERMANENT - added once when the tier is earned, then never removed - so a live
        // clone otherwise keeps whatever values the template held at the moment it was first added,
        // for the entire session. That silently pinned every milestone perk to CLIENT-LOCAL config
        // whenever ServerSync arrived after the effect was already active, despite all of these
        // being registered as server-synced.
        private static void ApplyEffect(SEMan seman, SE_Stats template, bool qualified)
        {
            if (template == null) return;
            int hash = template.NameHash();
            StatusEffect live = seman.GetStatusEffect(hash);

            if (qualified && live == null) seman.AddStatusEffect(hash, true);
            else if (!qualified && live != null) seman.RemoveStatusEffect(hash, false);
            else if (qualified && live is SE_Stats stats) RefreshTunables(template, stats);
        }

        // Every field ApplyConfigValues can write, copied template -> live clone. Deliberately an
        // explicit list rather than a wholesale re-Clone: the live instance also carries per-player
        // runtime state SEMan owns (m_time, m_character), and resetting that because a config value
        // moved would restart the effect rather than just retune it.
        private static void RefreshTunables(SE_Stats from, SE_Stats to)
        {
            to.m_healthRegenMultiplier = from.m_healthRegenMultiplier;
            to.m_staminaRegenMultiplier = from.m_staminaRegenMultiplier;
            to.m_eitrRegenMultiplier = from.m_eitrRegenMultiplier;
            to.m_swimSpeedModifier = from.m_swimSpeedModifier;
            to.m_swimStaminaUseModifier = from.m_swimStaminaUseModifier;
            to.m_skillLevel = from.m_skillLevel;
            to.m_skillLevelModifier = from.m_skillLevelModifier;
            to.m_skillLevel2 = from.m_skillLevel2;
            to.m_skillLevelModifier2 = from.m_skillLevelModifier2;
        }
    }
}
