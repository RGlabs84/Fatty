namespace Fatty.Patches
{
    // ---------------------------------------------------------------------
    // Multiplayer scope notes (why there is no ZDO patch here)
    // ---------------------------------------------------------------------
    //
    // Food effects in Fatty are applied entirely on the LOCAL player:
    //   * EatFood / UpdateFood run on the owning client.
    //   * The resulting max Health / Stamina / Eitr are pushed through the
    //     vanilla SetMaxHealth / SetMaxStamina / SetMaxEitr, which already
    //     sync to other clients via the character's ZDO.
    //   * Synergy StatusEffects are registered in the ObjectDB on every client
    //     and driven from each owner's own diet.
    //
    // So other players see your correct health/stamina bars and your active
    // synergies with no extra networking.
    //
    // The one vanilla limitation that remains cosmetic-only: the ZDO fields
    // that broadcast which foods another player is eating cover 3 slots, so a
    // REMOTE player's floating food icons may not show your 4th/5th slot. This
    // has no gameplay effect (their stats are already correct) and is left as a
    // deliberate design choice rather than shipping fragile custom ZDO packing.
    //
    // This file is intentionally code-free; it documents the decision.
    // ---------------------------------------------------------------------
}
