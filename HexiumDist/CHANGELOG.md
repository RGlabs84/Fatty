# Changelog

## [1.1.6] - Buff Timers Fixed, and a Ceiling On Them

### New

- **Max Effect Duration - a hard ceiling on how long a Fatty buff can ride.** Between the 2x food
  duration and four banked stacks, a single activation could sit on you for well over an hour, which
  is not what these were balanced for. Every food-driven Fatty effect - the four synergies, the four
  drink draughts and Drunk - is now capped at **30 minutes by default** (*4 - Synergies*, server-
  synced). When an effect hits the ceiling it ends even if your diet still qualifies, and it will not
  come back until its recipe stops being met and is met again, so you cannot simply keep topping the
  same buff up forever. Set it to **0** if you want the old uncapped behaviour. The permanent Getting
  Fat perks are progression, not buffs, and are never touched by this.

### Fixed

- **Buff countdowns sat still instead of counting down, twitching down a second and snapping back
  once a second.** Two separate faults, both now closed:
  - The countdown was reading the timer of a *parked* food stack. Fatty burns stacks of the same food
    one after another rather than all at once, so an older stack's timer is frozen solid until the one
    ahead of it runs out - a number that by design never moves. Any buff whose requirement landed on a
    parked stack therefore showed a countdown that never counted. **Drunk got this every single time**,
    since it always asks for several of the same mead, and Sugar Rush, Lumberjack's Feast and
    Fisherman's Friend got it whenever you met them with repeats of one food. Countdowns now add up
    the whole queue, so they show your real reserve - and actually tick down.
  - The once-a-second twitch was an ordering fault inside Fatty: the countdown was reading your
    stomach in the middle of the same-food-stack bookkeeping, catching every parked stack a second
    short for a single frame before it was put back. The two now run in a fixed order.

## [1.1.5] - Compatibility Detection Fix

### Fixed

- **Fatty could fail to notice another mod was installed, purely because of capital letters.** Its
  checks for other mods matched the mod's ID letter-for-letter including case, so a mod that changed
  the capitalisation of its own ID between releases would silently stop being detected - the mod was
  right there, loaded and running, and Fatty treated it as absent. Two things depended on this: the
  **Food Duration Multiplier** compatibility hand-off (Fatty steps aside and lets that mod own food
  timers when it's present) and **Valheim Cuisine** food categorisation (its dishes get their proper
  categories instead of falling through to name-guessing). Both now match regardless of case.
- **The Lifetime Feast Log is readable again.** Its entries were drawn in a muted grey meant for
  indented sub-details, which left the whole list looking greyed-out next to the Getting Fat
  section above it. It now uses the same bright parchment text as the rest of the panel.

## [1.1.4] - Compatibility Pass

### Changed

- **Rebuilt and re-verified against the current Valheim build.** There are no gameplay changes in
  this release - every setting, milestone, perk and synergy behaves exactly as it did in 1.1.3. If
  1.1.3 is working for you, nothing you can see will be different; this is here so the build you're
  running is the one that has been checked against the game as it ships today.

## [1.1.3] - Perk Sync Fix

### Fixed

- **Milestone perks could run on the wrong values for a whole session, and on a server could ignore
  the server's config entirely.** Valheim's `SEMan.AddStatusEffect` stores a *copy* of a status
  effect, not the effect itself - so once one of the permanent Getting Fat perks (Sea Legs, Well
  Nourished, Warrior's Build, Boundless Energy, Arcane Constitution, Iron Constitution) had been
  granted, later config changes never reached it. On a multiplayer server the server's synced values
  usually arrive *after* the perks are already active, which meant a client could spend the entire
  session running its own local numbers for every one of them despite these being server-synced
  settings. They now re-read config and retune themselves in place on every meal and every respawn.

  The diet-driven buffs (the four synergies and the drink draughts) were never affected in practice
  - they're added and removed constantly as your stomach changes, so they always picked up current
  values within seconds. It was specifically the *permanent* perks, which are granted once and never
  removed, where a stale copy could persist indefinitely.

## [1.1.2] - Fatty Gets Fatter

### New

- **Getting Fat now rewards six different stats, not just health.** Each diet category has its own
  flavour: **Meat** permanently raises carry weight, **Sweet** raises base stamina, **Eitr** raises
  max eitr, **Fish** grants a permanent swim-speed bonus, and **Vegetable** grants a permanent
  all-round regen boost (health, stamina *and* eitr regeneration together). A brand new **Fatness**
  tally - your total food eaten across *every* category, including food Fatty couldn't identify -
  is what grants permanent base max health now, so getting fat still makes you tougher, it's just no
  longer the only reward for a good diet. All six thresholds, award amounts and caps are
  configurable in "8 - Milestones", and existing characters keep every count they've already banked.
- **Meat, Sweet, Eitr and Fish now ALTERNATE between two different rewards across their 3 tiers**,
  instead of granting more of the same thing three times over. Tiers 1 and 3 still grant the flat
  pool bonus above; tier 2 grants a permanent, always-on alternate perk matching that category's own
  flavour instead - **Eitr**: eitr regen, **Sweet**: stamina regen, **Meat**: Sword & Axe skill
  (mirroring Warrior's Draught), **Fish**: reduced swim stamina cost (mirroring Seafarer's/
  Fisherman's Friend). **Fatness** alternates the same way: tier 2 grants health regen instead of
  more max health. Vegetable is the one exception and stays a flat regen% on all 3 tiers, since it
  never owned a single pool to alternate a flat reward against. All alternate-tier amounts are their
  own config entries in "8 - Milestones".
- **The Feast Ledger has a full gilt picture-frame treatment**, ported from a sister project's UI
  style at the user's request: a polished double-rail gold border with mitred acanthus-flourish
  corners and a palmette crest top and bottom, a near-black leather panel background, and Valheim's
  own body font wherever it's available. The Stomach breakdown also shows each food's real in-game
  icon next to its entry instead of just a name. The panel's minimum/default size grew to comfortably
  fit the thicker frame - existing saved panel sizes below the new minimum are clamped up on load.
  Text size is +2pt over the base theme by default and is its own config entry ("10 - HUD Panels" ->
  Feast Ledger Font Size Delta) if you want it larger, smaller, or back to the theme's own sizing.

### Changed

- The Feast Ledger's Getting Fat section now shows all six axes (Meat/Sweet/Eitr/Fish/Vegetable plus
  the overall Fatness tally), including each axis' tier-2 alternate reward, instead of a single
  per-category health line.
- **The shipped defaults have been rebalanced from a full modpack playtest.** Nothing about how any
  feature *works* changed - these are only the out-of-the-box numbers:
  - **Stacking is tighter.** Max Food Stacks 5 -> **4**, and the diminishing-returns ladder is now
    100% / **30%** / **20%** / **10%** (was 100/50/25/5). Fewer copies, each worth less early, but
    the 4th-and-beyond tier is worth double what it was, so a deep stack isn't quite as dead.
  - **Food lasts twice as long by default.** Food Duration Multiplier 1.0 -> **2.0**. This also
    doubles the drink slot's minimum hold to 20 minutes, since that floor is scaled by the same
    multiplier.
  - **Drunk now means a full drink slot.** Drunk Stacks Required 3 -> **6**, which is deliberately
    above the new Max Food Stacks of 4 and gets clamped down to it - so the requirement tracks your
    stack limit instead of getting easier as you raise it. At the defaults, 4 Tasty Meads.
  - **Synergies were toned down, Lumberjack's up.** Balanced Diet 3.0x -> **2.0x** regen, Sugar Rush
    2.5x -> **2.0x** speed, Fisherman's swim stamina reduction 50% -> **80%**, Lumberjack's carry
    weight +100 -> **+175**.
  - **Regen scaling is more conservative.** Max Regen Scale 4.0x -> **2.0x**.
  - **Drink health halved**, Max Health Per Drink 10 -> **5**.
  - **Milestone caps raised above what their tiers actually grant** - Meat carry weight 30 -> **40**,
    Sweet stamina 15 -> **20**, Eitr 15 -> **20** - so raising a single tier's award no longer gets
    silently clipped by a cap sized exactly to the old defaults.
  - **The Feast Ledger opens top-left at 622x700**, and stacked slots show the "xN" count badge
    rather than the segmented bar by default. Both are per-client and still remembered once you
    move, resize or change them.

  > These apply to **new** installs only. An existing `wubarrk.fatty.cfg` keeps every value already
  > written in it - BepInEx only writes a default for a key that isn't there yet. To adopt the new
  > balance on an existing profile, delete that file and relaunch. On a server, the server's own
  > config still wins for everything except the Feast Ledger panel settings.

## [1.1.1] - Timer Fix

### Fixed

- **Stacked food no longer drains all at once.** Eating deer meat x5 used to run every stack's timer
  in lockstep, so the whole stack vanished in a single moment instead of one item draining at a time
  - which also meant the Feast Ledger's "banked" reserve figure wasn't actually being held in
  reserve. Only the most-recently-eaten stack of a food now ticks down; every older stack of that
  same food is held in reserve and takes over automatically, one at a time, as each one before it
  runs out.

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

