using System.Collections;
using UnityEngine;
using Fatty.Configuration;

namespace Fatty.Patches
{
    // The Feast Ledger's Puke button: empty every food slot AND the drink slot in one go.
    //
    // Vanilla's only way to do this is Pukeberries, whose SE_Puke calls RemoveOneFood once a second -
    // and Fatty's RemoveOneFood_Prefix (PlayerPatches) already turns that into "clear the whole
    // stomach". So this is not new stomach logic, it is the same clear, reachable without having to
    // carry a berry. It does the clear itself rather than calling RemoveOneFood so it does not depend
    // on that prefix (or anybody else's patch on it) being in place.
    //
    // What it deliberately does NOT do:
    // - Refund anything. FoodLifetimeLog / MilestoneStore count what was EATEN, and that happened.
    // - Remove a drink's own vanilla status effect (a mead's regen, a resistance). Those effects are
    //   vanilla's potion cooldown - every mead's SE carries the shared "Mead" category, and that one
    //   check is what stops chugging health meads mid-fight. Stripping them here would turn this
    //   button into "drink, puke, drink again" unlimited healing. Pukeberries leave them too.
    // Fatty's own draughts, synergies and Drunk need no handling: they are re-derived from the
    // stomach on every UpdateFood, and the forced UpdateFood below is one.
    internal static class PukeAction
    {
        // A second click inside this window confirms. Unscaled, so the confirm still works while the
        // game is paused behind the ESC menu - the one state in which the Ledger is clickable.
        private const float ArmWindowSeconds = 3f;

        // The visual/sound instances are created exactly as SE_Puke's start effects would be, but
        // there is no status effect left alive to tear them down (StatusEffect.RemoveStartEffects
        // does that on Stop), so this does it instead. Clamped: a puke that keeps going is worse than
        // one cut slightly short.
        private const float MinFxSeconds = 2f, MaxFxSeconds = 6f;

        private const string PukeberriesPrefab = "Pukeberries";

        private static float _armedUntil = -1f;

        internal static bool Enabled => ConfigManager.allowPukeButton != null && ConfigManager.allowPukeButton.Value;

        internal static bool Armed => Time.unscaledTime < _armedUntil;

        internal static void Click()
        {
            if (!Armed)
            {
                _armedUntil = Time.unscaledTime + ArmWindowSeconds;
                return;
            }

            _armedUntil = -1f;
            Puke(Player.m_localPlayer);
        }

        internal static void Puke(Player player)
        {
            if (player == null || player.IsDead()) return;
            // Re-checked at the moment of the click, not just when the button was drawn: a server
            // can flip this off between the two.
            if (!Enabled) return;

            try
            {
                var foods = player.m_foods;
                if (foods == null || foods.Count == 0)
                {
                    player.Message(MessageHud.MessageType.Center, "There is nothing left to give.");
                    return;
                }

                int count = foods.Count;
                foods.Clear();
                // Drop max HP/stamina/eitr back to base now rather than on the next 1s food tick,
                // and let SynergyManager / DrinkBuffs see the empty stomach on the same frame.
                player.UpdateFood(0f, forceUpdate: true);

                PlayVanillaPukeEffects(player);
                if (Hud.instance != null) Hud.instance.DamageFlash();

                player.Message(MessageHud.MessageType.Center,
                    count == 1 ? "You puke it back up." : $"You puke up all {count} of them.");

                if (ConfigManager.debugMode.Value)
                    FattyPlugin.Instance.Log.LogInfo($"Fatty: Puke button emptied {count} stomach entries.");
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Puke Button. Reason: {ex}");
            }
        }

        // Borrowed from the Pukeberries item rather than named prefab-by-prefab, so the button looks
        // and sounds exactly like vanilla's puke - including the male/female sound variant - and keeps
        // doing so if a game update swaps the assets. Missing berry (a mod removed it) = silent puke,
        // never a failure: the stomach is already empty by the time this runs.
        private static void PlayVanillaPukeEffects(Player player)
        {
            try
            {
                GameObject berry = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(PukeberriesPrefab) : null;
                ItemDrop drop = berry != null ? berry.GetComponent<ItemDrop>() : null;
                StatusEffect se = drop?.m_itemData?.m_shared?.m_consumeStatusEffect;
                if (se == null || se.m_startEffects == null) return;

                // Same arguments StatusEffect.TriggerStartEffects passes.
                int variant = se.m_hitVariant >= 0 ? se.m_hitVariant : player.GetPlayerModel();
                GameObject[] fx = se.m_startEffects.Create(player.GetCenterPoint(), player.transform.rotation,
                    player.transform, player.GetRadius() * 2f, variant, player.GetZDOID());

                if (fx != null && fx.Length > 0)
                {
                    float life = Mathf.Clamp(se.m_ttl > 0f ? se.m_ttl : MaxFxSeconds, MinFxSeconds, MaxFxSeconds);
                    FattyPlugin.Instance.StartCoroutine(DestroyAfter(fx, life));
                }
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: puke effects skipped. Reason: {ex.Message}");
            }
        }

        // Mirrors StatusEffect.RemoveStartEffects, minus its assumption that every instance has a
        // ZNetView: networked effects are destroyed through ZNet so other players' copies go too,
        // purely local ones (or ones TimedDestruction already took) are handled directly.
        private static IEnumerator DestroyAfter(GameObject[] instances, float seconds)
        {
            yield return new WaitForSeconds(seconds);

            foreach (GameObject go in instances)
            {
                if (go == null) continue;
                ZNetView nview = go.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    nview.ClaimOwnership();
                    nview.Destroy();
                }
                else if (nview == null)
                {
                    Object.Destroy(go);
                }
            }
        }
    }
}
