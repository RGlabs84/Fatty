# Changelog

## [1.1.0]

### New

- **Getting fat now means something.** Every food you eat is counted per category, and passing a
  threshold permanently raises your **base max health** - small awards, well spaced out, capped by
  default at +50. Because they're per category, a varied diet pays and spamming one cheap food
  doesn't - each category runs its own independent ladder, and the +50 cap only limits the sum.
  Progress is stored on the character and survives relogs.
- **Fatty has its own food timer extension.** blacks7ar's FoodDurationMultiplier is now optional -
  see "7 - Duration". If you do have that mod installed, Fatty defers to its value entirely, so
  running both can never double up.
- **Mead & drink buffs.** A drink in your drink slot now grants a buff for as long as it lasts:
  Arcane Draught (eitr regen + magic skills), Warrior's Draught (sword + axe), Reveler's Draught
  (stamina regen + run/jump) and Seafarer's Draught (swim + fishing). Which one you get is decided by
  **what the drink actually does** rather than by guessing from its name - an eitr mead gives the
  Arcane, a stamina mead the Reveler's, a swimming mead the Seafarer's - so every mead in the game
  grants something, including the resistance and repellent meads. Every drink buff also grants bonus
  max health that scales with how many stacks you commit.
- **Drunk.** Drink three Tasty Meads and you get Drunk: harder hitting and harder to stagger, but
  your guard suffers. The number is configurable. Skal!
- **Food breakdown panel and lifetime feast log.** A stacked slot's timer now shows exactly when
  that slot empties (every stacked item decays in parallel, so it's the longest-lasting item, not a
  sum), and a segmented bar under each icon shows every individual item dimming as it runs down.
  Click the gold ▲ FEAST toggle above your food bar for a full breakdown of every stacked item's own
  timer, stack position and HP/Stamina/Eitr contribution, plus two more sections inside the same
  panel: your Getting Fat progress, and a Lifetime Feast Log of every distinct food you've ever
  eaten on that character and how many times. The panel drags by its title bar, resizes from a
  corner grip, and remembers where you left it across a full game restart.
- **Synergy and drink-buff icons now show a real countdown**, computed from your actual stomach
  rather than a fixed timer, so it always reflects exactly when your diet will stop qualifying.

### Changed

- **Food categories reworked.** There is now a real **Eitr** category - previously all 118 eitr foods
  were scattered across Meat/Vegetable/Fish/Sweet, so mage food was invisible to the mod. Eitr foods
  are now identified by their stats rather than their name, so "Mushroom Omelette" is correctly mage
  food rather than a vegetable.
- **"Drink" is no longer a food category, it's a property.** A drink now has a real food category
  *and* takes the drink slot, so an eitr mead counts for mage synergies while still being a drink.
- **Unknown foods are no longer silently called vegetables.** Anything the mod can't identify goes to
  a real `Unknown` category and is always treated as food, never a drink - so it can't take your
  drink slot. This makes the "1 Veggie" synergy requirement mean something again.
- Your `Fatty_FoodData.json` is migrated automatically on first launch. **Your old file is backed up
  to `Fatty_FoodData.json.bak`** - any categories you edited by hand will be re-derived.

### Fixed

- **Health, stamina and eitr bars flashing every second, and max stats slowly inflating.** If you run
  blacks7ar's FoodDurationMultiplier - at its default settings, so this hit most people who had it -
  Fatty and that mod disagreed about what a food was worth, and the gap widened every tick once a
  food passed its half-life. Fatty now owns the per-food maths outright instead of trying to work
  backwards from the game's own total, so the number is stable. That mod's Health/Stamina/Eitr
  *Benefits* sliders are read directly and still apply, and its duration setting is still deferred to.
- **You couldn't eat a 4th food after logging back in.** The game restores your stomach in a way that
  loses each food's prefab reference, and Fatty was reading the wrong name for it - so after a relog
  your *drink* was counted as a solid food (filling all 4 food slots with only 3 foods), stacking
  reset, and categories stopped matching. Everything now resolves the name the same way.
- **Foods filed under the wrong category stayed wrong forever.** Fatty only ever re-derived a
  category it had never seen. There's now a **`Rebuild Food Data`** switch in "2 - Core": turn it on,
  launch once, and every food is re-sorted from scratch (with a `.bak` of your old file), then the
  switch turns itself off.
- **Meads never actually reached your drink slot.** The game only routes an item through the food
  system if it has a health value, and every vanilla mead has none - so no mead could hold the drink
  slot, Drunk could never trigger, and the "+ 1 Drink" synergies were unreachable. Meads now take the
  slot. Drinking one is never *blocked*: if the slot is busy, the mead just works the way it always
  did without a Fatty buff.
- **A health or stamina mead's draught used to blink on and vanish.** A mead held the drink slot for
  exactly as long as its own effect ran, and a healing mead is finished in seconds - so its buff was
  gone almost before you saw it. Drinks now hold the slot for their own duration **or 10 minutes,
  whichever is longer** (configurable). This does *not* let you drink more often: the game's cooldown
  between meads is untouched, because that cooldown is what stops potion spam.
- **Drunk was impossible to reach.** It asked for a full drink slot of Tasty Mead, so raising your
  Max Food Stacks setting made it *harder* - at 7 stacks it wanted seven Tasty Meads at once. It now
  has its own setting, defaulting to 3, capped by your stack limit. Tasty Mead is also the one drink
  you can now knock back one after another, which is what makes stacking it possible at all; it's
  safe to allow because unlike every other mead its effect does nothing. Every other mead still
  respects the vanilla cooldown exactly as before.
- **Lumberjack's Feast was cutting your woodcutting damage by 55%** instead of increasing it - the
  game applies that value as a multiplier, so the default 0.45 meant you did 45% of normal damage.
- **Some Eitr foods still weren't recognised as Eitr.** The dominance check required eitr to beat
  BOTH health and stamina; foods where it only edged out one of the two stayed filed under their
  old category. Relaxed to either one - a food data migration re-derives everyone's categories
  again on next launch.
- Fatty could fail to load entirely if FoodDurationMultiplier hadn't initialized first.
- Potions, scrolls and boss-power items are no longer mistaken for drinks.
- `Fatty_FoodData.json` is no longer rewritten on every single launch.
- **The Feast Ledger panel's toggle and drag handle could be completely unresponsive.** Both went
  through Unity's on-screen click system, which the game's own HUD was never built to support (you
  can't click your health bar either) - a defensive fix last round wasn't enough to fix it for
  everyone. The whole panel is rebuilt the same way Njord's ship HUD works: it reads mouse input
  directly every frame instead of going through that system at all, so clicking the toggle and
  dragging the title bar now work the same way for everyone. Its corner grip still resizes it, its
  position/size still survive a restart, and its default spot no longer sits near your health bar.
  A stack of the same food (e.g. "Tasty Mead x7") now collapses behind one line you click to expand,
  instead of repeating the food's name once per item.

### Config

- **Every numeric setting now has a valid range**, so a mistyped value - or one pushed by a server -
  is clamped instead of quietly corrupting your health bar. Descriptions now all state the unit
  (multiplier vs. flat vs. fraction), what vanilla is, and what the default works out to.
- New: **`Rebuild Food Data`** ("2 - Core"), **`Default Drink Duration`** ("9 - Drink Buffs", the
  minimum time any drink holds the slot - drinks whose own effect lasts longer keep their own timer)
  and **`Drunk Stacks Required`** ("9 - Drink Buffs", how many Tasty Meads it takes to get Drunk).
- New section **"10 - HUD Panels"**: saved position/size for the Feast Ledger panel (screen pixels
  from the top-left corner), written automatically when you drag or resize it. Local to each
  client, not server-synced - where you like a panel isn't something a server should override. Also holds
  **`Use Segmented Stack Bar`**: the per-icon segmented bar and the "xN" badge show the same
  information, so it's one or the other, never both - ON (default) shows the segmented bar. And
  **`Breakdown Panel Hotkey`**: opens/closes the breakdown panel from the keyboard (default **F8**),
  in case the on-screen toggle isn't clickable on your setup.
- Repeated "Blocked eating..." log lines are throttled so the log stays readable.

## [1.0.3]

- **Fixed: Eitr no longer breaks alongside other mods.** Fatty was overwriting the game's food totals
  outright instead of adding to them, wiping out any other mod's contribution. Base Eitr from
  AllTheBases now works properly (it previously applied on an empty stomach and vanished the moment
  you ate), and FoodDurationMultiplier's health/stamina/eitr benefit settings apply again.
- **Fixed: stamina and Eitr regeneration no longer fall behind.** The game regenerates both at a flat
  rate that ignores how big your bars are, so Fatty's larger pools took far longer to refill - worst
  of all for mages. Regen now scales with the pool, keeping refill times at vanilla parity. New
  "6 - Regen" config section to tune or disable it.
- Fixed: food health regeneration now respects stack diminishing returns like every other stat,
  instead of healing at full value for every stack.
- Fixed: synergy config values were read once at startup, before a server could send its own, so
  server-synced values never actually applied.
- Fixed: Balanced Diet's regen multiplier can now be set below 1.0, as its description says.
- Fixed: ServerSync version mismatch messages reported a stale version number.

## [1.0.2]

- Added compatibility with blacks7ar's FoodDurationMultiplier mod

## [1.0.1]

- Removed diagnostic dump log information
- Better default configs

## [1.0.0] - Initial Release

- **Initial release! Time to get fat!**

