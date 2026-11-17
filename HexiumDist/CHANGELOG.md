# Changelog

## [1.2.0] - Last Call

### Added

- **Puke button in The Feast Ledger.** A **Puke** button now sits on the right of the Ledger's
  Stomach heading. Click once and it arms (**Really? Click again**, in red); click again within 3
  seconds and every food slot *and* the drink slot are emptied at once, all stacks included, with
  vanilla's own Pukeberries retching effect and sound (borrowed from the `Pukeberries` item, so the
  male/female variant matches your character) and a damage flash. Max health, stamina and eitr drop
  back to base immediately, and Fatty's synergies, drink draughts and Drunk end on the same frame
  because they follow what is in your stomach.
  - **Nothing is refunded.** The Lifetime Feast Log, category counts, Fatness and milestones keep
    every item you puked: what you ate stays eaten.
  - **Vanilla mead effects are left alone.** A mead's own status effect (regen, a resistance) is
    also vanilla's potion cooldown, so stripping it would make the button a "drink, puke, drink
    again" loop for unlimited healing. Pukeberries leave them in place too.
  - New server-synced setting **`2 - Core` → `Allow Puke Button`** (default on). Off hides the
    button; Pukeberries keep working as before.
- **Drunk motion blur.** While you are Drunk the screen smears with motion blur and ghosting trails.
  It fades in over about 3 seconds and fades out over about 5 seconds as you sober up. It drives the
  game's own post-processing motion blur (shutter angle plus frame blending, which vanilla ships at
  zero), so it works even with Motion Blur turned off in Valheim's graphics settings, and your own
  setting is put back afterwards, including if you changed it while drunk.
  - New local settings, section **`12 - Effects`**: **`Drunk Motion Blur`** (default on) and
    **`Drunk Blur Strength`** (0.0–1.0, default 0.6). Local on purpose: motion blur is a comfort
    choice, and a server should not be able to force it on anyone.

### Changed

- README rewritten to the Wubarrk store README schema v2.
- Fatty now references `assembly_postprocessing.dll` (shipped with both the game client and the
  dedicated server). Static reference check clean against both.

## [1.1.14] - Valheim 1.0.21

### Changed

- **Rebuilt for Valheim 1.0.21.** Rebuilt against the Valheim 1.0.21 game assemblies (network version 41) on BepInExPack 5.4.2360, and re-verified: static reference check clean against both the client and dedicated-server assemblies, and the mod boots on a real 1.0.21 dedicated server. Every Harmony target (including ServerSync's) still resolves. Fatty's food hooks (`Player.UpdateFood`, `EatFood`, `CanEat`, `GetTotalFoodValue`, the Hud food slots) are untouched by the update; the changed `Player.UpdateStats`, `Player.Save`/`Load`, `PlayerProfile.GetStat` and `CookingStation` code does not intersect anything Fatty patches or reads, and Fatty keeps its per-character data in `Player.m_customData` (which vanilla still saves and loads the same way) rather than appending to the player save. No source change and no behaviour change.
- Website link moved to https://www.rossgilsonlabs.dev.
- The BepInExPack dependency is now `denikson-BepInExPack_Valheim-5.4.2360`.

## [1.1.13] - Valheim 1.0.16

### Changed

- **Rebuilt for Valheim 1.0.16.** Rebuilt against the 1.0.16 game assemblies (client build
  25527674 / server build 25527701, network version 40) and BepInExPack Valheim **5.4.2350**. The
  update removed `ObjectDB.GetAllFoodItems()` and `ButtonSfx.Start()`, neither of which Fatty
  calls, so every Harmony target (34, ServerSync's included) and every game member Fatty reads -
  the player's food list and regen timer, the five food HUD slots, the status-effect list,
  `ObjectDB`, and the `ZNet` lifecycle the feast export hangs off - was unchanged, so there is
  **no source change and no behaviour change**. Verified by loading on a real 1.0.16 Linux
  dedicated server: every patch applies, the food HUD expands to its 5 slots, and the log is
  clean.

## [1.1.12] - Valheim 1.0.7

### Changed

- **Built for Valheim 1.0.7.** Rebuilt against the 1.0.7 game assemblies (client build 25185596 /
  server build 25185644, network version 39) and BepInExPack Valheim **5.4.2350**, which the package
  now lists as its dependency in place of 5.4.2333. Every Harmony target (34, ServerSync's included)
  and every game member Fatty reads - the player's food list and regen timer, the five food HUD
  slots, the status-effect list, `ObjectDB`, and the `ZNet` lifecycle the feast export hangs off -
  was checked against the 1.0.7 decompile and none of them moved, so there is **no source change
  and no behaviour change**. Verified by loading on a real 1.0.7 Linux dedicated server: every patch
  applies, the food HUD expands to its 5 slots, and the log is clean.

## [1.1.11] - The Feast, Shared

### Added

- **Server-wide feast records, for Discord bots and dashboards.** A server can now collect what
  everyone eats into a single file — `BepInEx/config/Fatty/BarrkBOT_feasts.json` — so a bot can
  answer "what's Asfaloth's favourite food", "who has eaten the most Serpent Stew" and "who is the
  biggest glutton on the server". **It is OFF by default** and lives in the new config section
  *11 - Feast Export*; a server that leaves it alone collects nothing, sends nothing and creates no
  file. Only the **server's** copy of the setting does anything, and it takes effect immediately —
  no restart needed to turn it on or off.
- **Your existing history counts.** The first time you connect to a server with this switched on,
  your **whole** lifetime feast log goes up as-is. Nobody starts from zero, and there is nothing to
  migrate or re-eat — if you've been playing since 1.1.0, all of it is already there.
- **Players who log off keep their place.** Records persist across relogs and server restarts, so a
  leaderboard doesn't reset to whoever happens to be online.

### Fixed

- **The Feast Ledger now closes when you close your inventory or the ESC menu**, like every other
  window in the game, instead of being left hanging over the world with no way to click it. The
  small **▲ FEAST** tab stays where it is — that's how you open the panel again.

### Notes for server admins

- The export is written every **60s** (configurable), and each player is asked for a fresh copy of
  their counts every **300s** (configurable). Someone who has just joined is asked straight away.
- Counts are **whole totals, not "since yesterday"**. A number can jump the first time a player
  syncs after updating — that's their existing history arriving, not an error.
- The file names raw prefabs (`CookedMeat`, not "Cooked Meat"), and `favourite_food` is `null`
  when there's a tie. A player who has never been seen is **absent** rather than listed with zeroes.

## [1.1.10] - Getting the Feast Ledger Back

### Fixed

- **🐧 Linux (Proton) only: the Ledger now survives a desktop that scales X11 apps.** On a Wayland
  session where one display is set to a fractional scale, the Ledger's buttons would highlight under
  the mouse and then ignore the click, while the **F8** hotkey opened the panel perfectly. Nothing was
  wrong with the click — the game and the desktop simply disagreed about where the pointer was, by
  exactly the scaling factor. **This is not a Fatty bug and it is not a Valheim bug**: it affects every
  mod that draws this style of window, and the real cure is to set your displays to the same scale (on
  KDE: System Settings → Display → Legacy applications (X11)). Fatty now reads the pointer straight
  from your mouse rather than trusting what it is handed, so the Ledger keeps working even when that
  setting is wrong. **Windows players were never affected and nothing changes for them.**
- **Opening the panel by clicking ▲ FEAST could collapse it to a sliver.** Opening it that way also,
  wrongly, shrank it to the size of the little button it had just replaced — a 110×30 stub with
  nothing drawable inside, so it drew nothing at all and the button vanished with it. The hotkey was
  unaffected, since it toggles the panel from outside the drawing code. The panel now keeps its real
  size whichever way you open it, and can no longer be shrunk below its minimum by any route.
- **The Feast Ledger could vanish completely, with no way to get it back.** Drag the panel far
  enough past the left edge of the screen, close it, and the little **▲ FEAST** button went with it —
  and because the panel remembers where you left it, it stayed gone on every launch after that.
  1.1.8 added a safety net meant to prevent exactly this, but it measured the *open* panel while the
  thing actually on screen was the small closed button, so it left that button sitting as much as
  440 pixels beyond the edge and called it safe. It now measures whatever is really being drawn, so
  a grabbable strip is always on screen. If your saved position is already bad, launching 1.1.10
  rescues it — and **remembers** the rescue, instead of quietly re-doing it every session while the
  bad position stays on file.
- **The ▲ FEAST button could stop drawing after you reloaded a world.** Its background was being
  discarded by the engine on the scene change and never rebuilt, so the button faded away for the
  rest of the session the moment you returned to the main menu and loaded a save again. Since that
  button is the only way to reopen a closed panel, losing it meant losing the panel.
- **A stumble while building the panel's styling took the panel down for the whole session.** It now
  retries on the next frame rather than staying broken until you restart the game.

## [1.1.9] - Fruit, and the Food Fatty Was Never Counting

### Fixed

- **Berries were never being counted as anything.** Raspberries, Blueberries, Cloudberries and
  Vineberries all fell through Fatty's classifier into "Unknown", which earns no category milestone
  at all — so a character living on berries got credit toward Fatness and nothing else. Fatty matched
  food names a whole word at a time, and "Raspberry" is the single word *raspberry*, which is not
  *berry*; no raw berry in the game could ever match.
- **Blueberries were seeded under a prefab that doesn't exist.** The built-in starter list said
  `Blueberry`; the actual item in Valheim is `Blueberries`. That entry matched nothing, so even a
  brand-new install had blueberries uncategorised.
- **The same fault was hiding far more than berries.** Because matching was strictly whole-word,
  `GlazedCarrots` didn't match "carrot", `SauteMushrooms` didn't match "mushroom", `VikingCupcake`
  didn't match "cake", `MisthareSupreme` didn't match "hare" and `FierySvinstew` didn't match
  "stew". On a fully modded test setup that left **85 real foods earning nothing at all**. Matching
  now understands plurals and compound word-endings, so modded foods are covered without Fatty
  having to know their names — recovering Feasts, broths, stews, egg dishes, glazed vegetables and a
  long tail of Valheim Cuisine plates. Measured on a real 624-item scan: unclassified real food
  dropped from **85 to 58**, with **no** previously-correct category changed.
- **A dish is now categorised by its ingredients, not its cooking method.** "Turnip Stew" and
  "Fish Stew" are Produce and Fish rather than Meat; only a stew or broth whose ingredients Fatty
  can't recognise falls back to Meat.
- **Food eaten while milestones were switched off was lost forever.** The per-category tally stopped
  being written entirely when "Enable Milestones" was off, while the Lifetime Feast Log kept
  recording — so the two drifted apart and Fatness permanently under-counted. The tally now always
  runs; the setting turns off the *rewards*, not the bookkeeping. Switch it back on and you pick up
  exactly where you left off.
- **Re-categorising a food used to strand everything you'd already eaten of it.** Counts were filed
  under whatever category a food had at the moment you ate it and never moved again, so a
  "Rebuild Food Data", a food-mod update, or an improvement to Fatty's own classifier left your
  progress under a name nothing read any more. Fatty now re-derives your lifetime category counts
  from the per-food Feast Log when the classification changes. **This runs once on your first launch
  of 1.1.9** and repairs every berry you have ever eaten retroactively.
- **Unclassified food is now visible.** The "Getting Fat" panel showed five category lines and a
  Fatness total that didn't add up to them, with nothing to explain the difference. It now shows an
  **Unclassified** line whenever you have any, and tells you how to fix it.
- **Renamed config settings no longer silently reset to their defaults.** BepInEx looks settings up
  by name, so renaming one leaves your tuned value orphaned in the config file and the setting back
  at default with no warning. Fatty now carries the old value across and tidies the dead line away.
- **Loading a second character without restarting the game used the first one's base stats.** Fatty
  records your pristine base health, stamina and carry weight so milestone bonuses can be added on
  top; it was capturing them once per session rather than once per character, so switching
  characters carried the wrong baseline across. Now captured on every spawn.
- **A modded food whose internal name contained a comma or an equals sign could corrupt your whole
  Lifetime Feast Log.** Both are used as separators in the saved record, so a single such food broke
  every entry after it — not just its own — and silently truncated your history on the next load.
  Names are now escaped. Existing logs are unaffected and need no conversion.
- **Reduced per-second work on the food stat path.** Your lifetime counts were being re-parsed from
  their saved text once a second to answer the max-eitr milestone bonus; the parse is now reused
  until the counts actually change.

### Changed

- **"Vegetable" is now "Produce", and covers fruit as well as vegetables.** Berries and other raw
  fruit join carrots, turnips and mushrooms in one category rather than being counted as
  confectionery. Anything actually *made* from fruit — pies, jams, tarts, Queen's Jam — stays
  **Sweet**, so preparation still decides the category rather than the ingredient.
  - The Balanced Diet and Fisherman's Friend synergies now read "1 Produce" instead of "1 Veggie",
    and berries satisfy them.
  - The three `Vegetable Tier N Regen Multiplier` config entries are renamed to `Produce Tier N ...`.
    **If you had customised those three values they are carried across for you** and the old lines
    are cleaned out of your config file. Nothing else in the config is affected.
  - Your existing Vegetable progress is carried over by the one-time rebuild described above, not
    lost.
- **Your food data file will be rebuilt once on first launch.** `Fatty_FoodData.json` moves to data
  version 7; the previous file is backed up to `Fatty_FoodData.json.bak` as usual. Any categories you
  hand-edited in it will be overwritten, so re-apply them afterwards if you had any.

> **Coming from 1.1.7?** 1.1.8 was a test build and was never published, so its fixes ship here too —
> see the 1.1.8 section below for the Feast Ledger drag, off-screen and text-wrapping fixes.

## [1.1.8] - Hotfix: Feast Ledger Drag/Position/Wrapping

### Fixed

- **The Feast Ledger rewrote its whole config file to disk on every frame of a drag.** Moving the
  panel wrote its X/Y position to config every single frame the mouse moved rather than once the
  drag ended, and BepInEx saves the whole config file to disk on every settings change. Position is
  now only committed once you let go of the mouse.
- **A panel position saved on a monitor you no longer have could strand it off-screen for good.**
  There was no clamp on the restored position and no in-game way to drag it back. The panel now
  keeps at least its title bar on screen, both on load and live.
- **Some "Getting Fat" milestone lines could run off the edge of the panel instead of wrapping.**
  The tier-3 lines for Meat/Sweet/Eitr/Fish join two full reward descriptions and could exceed the
  panel width, especially at the minimum window size; they now wrap onto a second line instead of
  getting clipped.

## [1.1.7] - Hotfix: Feast Ledger Readability

### Fixed

- **The per-stack lines in the Feast Ledger's Stomach section were greyed out.** Expanding a stacked
  food showed its per-stack breakdown in a muted grey meant for minor sub-details, which left the
  densest and most useful text in the panel as the hardest thing on it to read - and mismatched
  against the Getting Fat and Lifetime Feast Log sections right below it, which are drawn in bright
  parchment. They now use that same bright parchment. The indent, the numbering and a slightly
  smaller size already mark them as detail; the dimming was doing nothing but hurting legibility.
  This is the same fix the Lifetime Feast Log got in 1.1.5 - these lines were simply missed at the
  time.

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

