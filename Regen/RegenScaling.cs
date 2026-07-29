using System.Runtime.CompilerServices;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Regen
{
    // ---------------------------------------------------------------------
    // Why this exists
    // ---------------------------------------------------------------------
    // Vanilla stamina and eitr regen are FLAT absolute rates. From Player.UpdateStats:
    //
    //     stamina: (m_staminaRegen + (1 - cur/max) * m_staminaRegen * m_staminaRegenTimeMultiplier) * state
    //     eitr:    (m_eiterRegen   + (1 - cur/max) * m_eiterRegen)                                  * state
    //
    // with m_staminaRegen = m_eiterRegen = 5f. Nothing in there scales with the SIZE of the bar -
    // it is ~5-10 points/sec whether your pool is 100 or 1000. Fatty hands out roughly 3x vanilla's
    // pool (5 slots x the 1+0.5+0.25+0.05+0.05 stack ladder, versus vanilla's 3 slots), so the bar
    // took ~3x longer to refill in percentage terms - strictly worse than vanilla. That is what
    // users reported as Fatty "actively stopping regen".
    //
    // Health hid the problem, which is why only mages noticed: food health regen sums m_foodRegen
    // over every stack, so it got BETTER as you stacked. Only stamina and eitr regressed.
    //
    // The fix is to scale vanilla's own rate fields by however much bigger Fatty made the pool,
    // which holds time-to-fill at vanilla parity. Scaling the field scales both terms of the
    // expression uniformly, so no transpiler is needed. Synergy multipliers (Balanced Diet's
    // m_staminaRegenMultiplier) still apply on top through SEMan as a genuine bonus.
    // ---------------------------------------------------------------------
    public static class RegenScaling
    {
        private class Baseline
        {
            public bool Has;
            public float StaminaBase, EitrBase;       // the rate we scale FROM (may be another mod's)
            public float StaminaWritten, EitrWritten; // what we last wrote, to spot foreign writes
        }

        // Keyed weakly so a destroyed Player can't keep its entry (and its Player) alive.
        private static readonly ConditionalWeakTable<Player, Baseline> _state =
            new ConditionalWeakTable<Player, Baseline>();

        /// <param name="stamina">Fatty's final max stamina.</param>
        /// <param name="refStamina">Max stamina vanilla's own slot rules would have granted.</param>
        /// <param name="eitr">Fatty's final max eitr.</param>
        /// <param name="refEitr">Max eitr vanilla's own slot rules would have granted.</param>
        public static void Apply(Player player, float stamina, float refStamina, float eitr, float refEitr)
        {
            if (player == null) return;

            if (!ConfigManager.scaleRegenWithPool.Value)
            {
                Restore(player);
                return;
            }

            Baseline b = _state.GetOrCreateValue(player);

            // Re-baseline instead of caching a "pristine" value once. Other mods write these same
            // fields on their own schedule - AllTheBases' UpdateBases sets m_staminaRegen and
            // m_eiterRegen from its config on Player.Awake AND on every config change - so a
            // capture-once baseline would go stale and we would stomp their value back. If the
            // field no longer holds what we last wrote, someone else owns it now: adopt it.
            if (!b.Has || !Mathf.Approximately(player.m_staminaRegen, b.StaminaWritten))
                b.StaminaBase = player.m_staminaRegen;
            if (!b.Has || !Mathf.Approximately(player.m_eiterRegen, b.EitrWritten))
                b.EitrBase = player.m_eiterRegen;
            b.Has = true;

            float cap = Mathf.Max(1f, ConfigManager.maxRegenScale.Value);

            player.m_staminaRegen = b.StaminaBase * Scale(stamina, refStamina, cap);
            player.m_eiterRegen = b.EitrBase * Scale(eitr, refEitr, cap);

            b.StaminaWritten = player.m_staminaRegen;
            b.EitrWritten = player.m_eiterRegen;
        }

        private static float Scale(float pool, float reference, float cap)
        {
            // No food, or no eitr food at all -> nothing to compensate for. Returning 1 here is also
            // what restores the base rate as food expires and the stomach empties.
            if (reference <= 0f || pool <= 0f) return 1f;

            float scale = pool / reference;
            if (float.IsNaN(scale) || float.IsInfinity(scale)) return 1f;

            // Never scale DOWN - Fatty should not be able to make regen worse than vanilla.
            return Mathf.Clamp(scale, 1f, cap);
        }

        // Puts the rates back when the feature is switched off mid-session, but only if the fields
        // still hold our value - if another mod has written them since, leave theirs alone.
        private static void Restore(Player player)
        {
            if (!_state.TryGetValue(player, out Baseline b) || !b.Has) return;

            if (Mathf.Approximately(player.m_staminaRegen, b.StaminaWritten))
                player.m_staminaRegen = b.StaminaBase;
            if (Mathf.Approximately(player.m_eiterRegen, b.EitrWritten))
                player.m_eiterRegen = b.EitrBase;

            _state.Remove(player);
        }
    }
}
