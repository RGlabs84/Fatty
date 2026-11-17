# Fatty — project instructions

## Git: code only. Never commit build artifacts.

**Only source goes in git.** Do not `git add`, commit, or push any of the following, ever:

- `HexiumDist/*.zip` — release packages
- `HexiumDist/plugins/*.dll` — the deployed plugin binary
- `bin/`, `obj/` — build output

All of it is reproducible from source with `dotnet build Fatty.csproj -c Release` plus the
packaging step, so committing it only bloats history with binaries that churn on every release.
`.gitignore` enforces this — do not weaken those rules to "just get a release in".

Authored assets are **not** build artifacts and DO stay tracked: `Icons/*.png`,
`HexiumDist/icon.png`, and the hand-written `README.md` / `CHANGELOG.md` / `manifest.json`.

Shipping a release is a **separate** activity from committing. The zip and the deployed DLL go to
Gale + `HexiumDist/` on disk; git only ever receives the source change that produced them.

## Releases

Bump all three in lockstep — they drift silently otherwise:
1. `Fatty.csproj` → `<Version>`
2. `Plugin.cs` → `PluginVersion`
3. `HexiumDist/manifest.json` → `version_number`

Then build Release and copy the DLL to **both** the Gale `Fatty Only Tester` profile and
`HexiumDist/plugins/`, never just one. Verify with matching `md5sum` across both copies.
(The profile directory really is `Fatty Only Tester`, with spaces — full path
`C:\Users\rglmo\AppData\Roaming\com.kesomannen.gale\valheim\profiles\Fatty Only Tester\BepInEx\plugins\Wubarrk-Fatty\`.)

Build release zips with .NET's `System.IO.Compression.ZipArchive`, **not** PowerShell's
`Compress-Archive` — that cmdlet writes nested entries with backslash paths, which don't match the
forward-slash convention every existing release zip uses.

## Vanilla API gotchas

`SEMan.AddStatusEffect` stores a `Clone()` of the ObjectDB template, so mutating the template never
reaches an already-active effect. Fine for diet-driven effects (constantly re-added), broken for
always-on ones — resolve the live instance via `seman.GetStatusEffect(hash)` and copy fields onto it.

`SE_Stats` regen multipliers are **additive above 1** (`if (m > 1f) mult += m - 1f; else mult *= m`),
so three tiers of 1.02/1.04/1.06 total +12% exactly, not the product. The `<= 1` branch is why a
regen field left at `0` wipes the stat instead of doing nothing.

`UnityEngine.Input` (hardware mouse/keyboard polling, distinct from IMGUI's `Event.current`) lives in
`UnityEngine.InputLegacyModule.dll`, which Fatty did not reference until the Feast Ledger drag fix
below needed `Input.GetMouseButton`. It's now in `Fatty.csproj` alongside the other Unity module
references (`..\libs-Tools\UnityEngine.InputLegacyModule.dll`) — mirrors what BarrkUI already had.

## Food classification (1.1.9)

Name matching lives in **`Scanners/FoodWordClassifier.cs`**, deliberately free of Unity/Valheim
types so it compiles standalone and can be run against a real `Fatty_FoodData.json` dump. Do that
for any change to the word lists — a keyword edit silently re-files hundreds of foods at once, and
"it built" says nothing about whether it re-filed them *correctly*. The harness pattern: a throwaway
`net8.0` csproj that `<Compile Include>`s just that one file, diffing old vs new `Category` over the
whole dump. Exclude hardcoded VC prefabs, drinks and stat-less items from the comparison — the real
scanner decides those through `ClassifyDrinkByEffect`/`IsHardcoded`, paths a standalone harness
can't reach, and they otherwise show up as ~100 phantom regressions.

**Whole-word matching cannot see a plural or a compound noun, and that is not a corner case.** Names
are split on PascalCase and matched as whole words — deliberately, so `Contains("tea")` stops
matching inside `Steak` and `Contains("mead")` inside `Meadows`. What that cost, unnoticed for five
data versions: `Raspberry` is the single word `raspberry`, not `berry`; `GlazedCarrots` gives
`carrots`, not `carrot`; `VikingCupcake` gives `cupcake`, not `cake`. **Every raw berry plus 85 more
real foods sat in `Unknown`**, earning no category milestone. Fixed generally rather than
per-word — `MatchesWord` folds plurals, and `Matches` adds a **suffix** pass (never substring:
suffix is what keeps `Steak`/`Meadows` rejected, since English compounds put the head noun last).

Three guards on that suffix pass, all load-bearing:
- `MinSuffixLength = 4` — `ale` would make Kale a drink, `ham` would make Graham meat.
- `SuffixExcluded` — long enough to pass yet still a suffix of an unrelated word (`Mandrake` is not
  a drake). They lose nothing: `BlackBear` splits on its own capital and matches as a whole word.
- **Suffix matching is OFF for `DrinkWords`, and only for that list.** A false positive there hands
  the single drink slot to a solid food, which then blocks a real drink entirely; a missed *diet*
  keyword only costs an `Unknown`. `wine`/`Swine` and `wort`/`-wort` herbs are the live hazards.

**Ingredients and preparations are separate passes.** `MeatPreparationWords` (`stew`, `broth`,
`roast`…) is matched only after *every* ingredient list has had its say. Putting `stew` in
`MeatWords` made `TurnipStew`, `VC_FishStew`, `VC_MagmafishStew` and `VC_MushroomStew` all Meat,
because Meat is tested first — the pot beat its contents. `soup` is in neither list on purpose: it
implies nothing (`VC_DandelionSoup`), so Unknown beats a guess.

Only a fresh install hid it, via `GenerateDefaultCategories`' seeds — and any rebuild or migration
throws those away and re-derives. Worse, one seed named `Blueberry`, **a prefab that does not
exist**; vanilla's item is `Blueberries` (plural). Seed lists are unvalidated string literals — a
typo there is silent forever. Cross-check against a real ObjectDB dump, not memory.

`Vegetable` → **`Produce`** (vegetables + fruit) as of data version 6. Fruit is not its own category:
that would have meant a seventh milestone axis and a seventh draught for what is one dietary idea.
`FoodScanner` tests `SweetWords` **before** `ProduceWords` on purpose — that ordering is the whole
raw-vs-prepared rule (`Blueberries` → Produce, `BlueberryPie` → Sweet). Don't reorder them.

`CategoryManager.BaseCategories` is not just documentation: `SynergyManager.CountCategories` seeds
its dictionary keys from it, so renaming a category without updating the `counts["..."]` lookups is a
`KeyNotFoundException` on the per-frame `UpdateFood` path, not a mis-sort.

## Lifetime counts: two stores, one of them authoritative

`MilestoneStore` (per-CATEGORY, `Fatty.FoodCounts`) is **derived and lossy**; `FoodLifetimeLog`
(per-PREFAB, `Fatty.FoodLifetimeCounts`) is the record of truth. Category counts are keyed by the
category a food had *at the moment it was eaten* and never move again, so every reclassification —
`Rebuild Food Data`, a data-version migration, a food mod updating its stats, Fatty's own classifier
improving — strands history under a name nothing reads.

`MilestoneStore.RebuildFromLifetimeLog` re-files everything through current classifications, gated
one-shot on the `Fatty.FoodCountsSchema` stamp. **Bump that stamp whenever a category is renamed or
the classifier starts filing an existing food differently.** Two things it must keep doing:

- **Gate on `FoodScanner.ScanComplete`, never `_hasScanned`.** The latter is claimed up front to
  block a second coroutine, so it is true *during* the scan; rebuilding then files live foods as
  Unknown and stamps that as done. Called from both `FoodMilestones.OnSpawned` and the tail of
  `ScanRoutine` because either can finish last; the schema stamp makes the second call a no-op.
- **Never let the rebuild reduce the total.** The feast log only exists from 1.1.0, so a long-lived
  character can hold more in category counts than the log explains. Fatness is the total-eaten axis,
  so dropping the difference visibly takes max health from the players who ate the most — the
  shortfall is parked in `Unknown`, which feeds Fatness but earns no tier.

`enableMilestones` gates **rewards, not bookkeeping**. It used to return early in `RecordEaten` while
`FoodLifetimeLog.RecordEaten` on the next line stayed ungated, so the two stores silently diverged
and Fatness permanently under-counted. `Apply()` already zeroes every bonus when disabled, so the
switch only needs to suppress the announcements.

Both stores share `CountsCodec`. **Prefab names are untrusted keys** — a modded food containing `,`
or `=` doesn't just lose its own entry, it moves the delimiter and truncates every record after it.
Percent-encoding of `%`/`,`/`=` only, `%` first; chosen over a new delimiter specifically because a
name containing none of the three encodes to itself, so **existing saves need no migration**.

`MilestoneStore.Load` caches the parse keyed on the raw string, so any write through `Save`
self-invalidates it — there is no manual invalidation to forget. It hands out a **copy**, because
`RecordEaten` mutates what `Load` returns before saving. The cache exists because `GetEitrBonus`
reaches `Load` from `GetTotalFoodValue`'s postfix, i.e. once per second forever.

## Capturing "pristine" stats

`FoodMilestones` stores `_pristineBaseHp`/`Stamina`/`MaxCarryWeight` and writes
`pristine + bonus` into the same fields on every `Apply()`. So **where you capture decides whether
the bonus compounds**: `OnSpawned` captures unconditionally (a new `Player` instance always has the
prefab values, before `Apply` has touched them), while the `Apply` path must stay guarded by
`_capturedBase` — an unconditional capture there reads a value Fatty already inflated and folds the
bonus into the baseline once per eat. The old once-per-session guard also meant the first character
loaded defined the baseline for every character loaded afterwards in that session.

## Renaming a config key silently resets it

BepInEx keys a setting by `(section, key)`. Rename it and `Bind()` matches nothing, so the entry
comes back at its **default** and the player's value is parked in `ConfigFile.OrphanedEntries`
(`Dictionary<ConfigDefinition, string>`, raw unparsed) — no error, nothing on screen.
`ConfigManager.TryMigrateKey` reads it back by **reflection** (accessibility isn't part of BepInEx's
public contract) and removes the orphan so the dead line stops being rewritten. Range-clamp on the
way in: the old entry's `AcceptableValueRange` died with it, so this is the one path into a value
that bind-time validation doesn't cover. Add a `TryMigrateKey` line for **every** future key rename.

## Feast export: the server asks, the client answers (1.1.11)

`Progression/FeastRegistry.cs` + `Patches/FeastExportPatches.cs` write
`BepInEx/config/Fatty/BarrkBOT_feasts.json` for BarrkBOT. **That file is an interface with a program
on another machine** — see `PAUSED/TheRavensCall-OLD-1.2.1/BARRKBOT_CONTRACT.md` before renaming any field, and
prefer adding over renaming: a new field costs the reader nothing, a renamed one silently zeroes a
feature and nothing anywhere reports an error.

**A dedicated server cannot read `m_customData`, and no probe is needed to know it.** `Player`
objects do not exist headless at all (`Player.GetAllPlayers()` walks the local instance list), so
`FoodLifetimeLog` is unreachable server-side for any remote player. This is architecture, not a sync
delay. `FEAST_EXPORT.md` §2 originally proposed probing for it; the probe as written would have
returned a clean-looking negative for the wrong reason.

**The server PULLS; the client never pushes on its own.** This is the load-bearing inversion, and it
is not a style choice. A client-driven push has to read the ServerSync'd `Enable Feast Export` to
decide whether to send, and reading a synced value early in a session is a race the client usually
loses (`libs-Tools/PLAYER-IDENTITY-FACTS.md`, checklist 5). With the server asking, the switch lives
on the only machine that reads it: off means it never asks, so there is no traffic, no directory and
no file, and there is no client-side gate that can disagree with the server. It follows that handler
**registration** cannot be gated on the setting either — a handler is an inert dictionary entry, and
gating it would both re-introduce the race on clients and freeze the setting at world load on the
server. Read the config **live** in the tick; an admin toggling it should not need a restart.

**Absolute snapshots, never deltas.** A delta stream starts every player at zero, can never recover
history from before the feature shipped, and drifts permanently from one dropped message. `Merge`
therefore *assigns* rather than accumulates — which is also what makes a replayed or duplicated
message harmless, and what makes retroactive backfill free rather than a migration.

**Identity comes from the sender, never the payload.** The client sends counts only. The server
resolves the name and `s_playerID` from the routed-RPC `sender` → `ZNet.GetPeer` → the character ZDO.
A modded client can lie about its own diet; it cannot become a different connected peer. `m_characterID`
arrives *after* the peer is otherwise ready, so a snapshot can land before the name resolves — drop
and retry, never record under `"Stranger"` or `"..."` (a placeholder written down reads as an answer).

**`Encoding.UTF8` writes a BOM, and a BOM is not valid JSON.** `File.ReadAllText` silently tolerates
one, which is exactly why it survives being written and is only noticed by somebody else's parser —
`json.load` and `jq` both reject the file outright. Every writer here uses
`new UTF8Encoding(false)`. Same rule for any future export.

Two more that cost nothing but are easy to get wrong: `ZRoutedRpc.Everybody` is capital-E, is `0L`,
and means *every peer plus the sender locally* — for server-directed traffic use
`InvokeRoutedRPC(name, args)` with **no** peer id, which auto-targets the server. And RPC names share
one global hash namespace with the base game and every mod, so they are GUID-prefixed; registration
is `Dictionary.Add` on a `ZRoutedRpc` instance replaced on every `ZNet.Awake`, so it is keyed on the
**instance reference** — a stale `_registered` bool leaves the next world with no handlers at all.

Test it the way `FoodWordClassifier` is tested: `FeastRegistry.cs` and `CountsCodec.cs` compile in a
throwaway `net8.0` csproj against ~20 lines of stub for `BepInEx.Paths` and `FattyPlugin.Log`. The
BOM defect above was found that way and would not have been found any other way — "it built" says
nothing about whether the emitted file is correct, and the reader is another program.

## The Feast Ledger closes with the inventory (1.1.11)

`CollapseWhenGameResumes()` triggers on the **falling edge** of `InventoryGui.IsVisible() ||
Menu.IsVisible()`, not on the state. Testing the state directly is wrong in both directions: it slams
the panel shut on the first pass after an F8 open (F8 is only accepted while no menu is up — see
`HudPatches.CanAcceptHotkey`), and it then re-closes every frame so the pill can never reopen it.
Only the OPEN panel collapses; the closed pill is the toggle and the only way back in, so hiding it
would strand the panel exactly the way 1.1.10's clamp fix existed to prevent.

## Feast Ledger audit fixes (2026-08-04)

Three issues found auditing BarrkUI, which had inherited `FeastLedgerGui.cs`'s pattern verbatim from
here — all three now fixed in both projects. See `libs-Tools/IMPLEMENTATIONS/BarrkUI.md` §4 for the
full audit table.

- **Per-frame `.cfg` rewrite during a drag.** `HandleResize()` used to write
  `foodBreakdownPanelX/Y.Value` unconditionally every frame the window moved. `ConfigEntry<T>.Value`
  fires `SettingChanged` on change and `ConfigFile.SaveOnConfigSet` defaults `true`, so that
  serialised the whole config to disk once per frame for the length of a drag. Fixed the same way as
  BarrkUI's `ChatOverhaulWindow.CommitPositionIfSettled`: the live rect stays in a static, and the
  commit to config only happens once `Input.GetMouseButton(0)` is false and the value actually moved.
- **No screen clamp on the restored rect.** `EnsureInit()` now calls `ClampWindowToScreen()` (also
  called every `Draw()`), keeping at least the title band on screen in both axes — a position saved
  on a monitor you no longer have can no longer strand the panel off-screen with no in-game recovery.
  **As first written this fix did not work, and 1.1.10 had to repair it — see below.**

## The clamp must measure the DRAWN rect, not `_rect` (1.1.10)

`_rect` always holds the **open** size; while the panel is closed `Draw()` renders a
`ClosedWidth`×`ClosedHeight` pill instead. 1.1.8's clamp used `_rect.width` unconditionally, so the
x lower bound was `72 - 622 = -550` while the thing on screen was 110px wide — every x in
`[-550, -110]` was "legal" with the entire pill off the left edge. **The recovery was a no-op in
exactly the state that needs recovering**, because the panel is closed whenever it goes missing, and
the pill is the only way back in. A sweep of 4 screen sizes × 3 widths × 715 x-positions found
15,360 such states; the repaired clamp has none. Cap the keep-visible figure to the drawn size too —
demanding 72px of a 30px-tall pill is not satisfiable.

Two more things that made it permanent rather than merely annoying:

- **`EnsureInit` seeded `_committedPos` from the POST-clamp rect**, so the rescue equalled the
  committed value by construction and `CommitPositionIfSettled` could never write it back. The bad
  coordinate stayed in the `.cfg` forever and every launch re-derived the same rescue. Leave the
  `NaN` sentinel alone — that is what it is for; BepInEx's `Value` setter early-returns when
  unchanged, so a position that needed no rescuing still costs no write.
- **`Mathf.Clamp` is `if (v < min) v = min; else if (v > max) v = max`** — hand it `min > max` and it
  *alternates* between the two on successive calls instead of settling. `Draw()` clamps on every
  OnGUI pass, so that would defeat `CommitPositionIfSettled`'s exact-equality gate and reinstate the
  per-frame `.cfg` write 1.1.8 existed to remove. Order the bounds with `Mathf.Max`.

`FeastLedgerGui.MakeTex` also has to set `hideFlags = HideAndDontSave` — the same rule
`GiltFrameTheme`'s header states, which its own local copy was not following. Without it Unity
collects the pill background and grip on the next scene load, and since `_stylesReady` latches true
they are never rebuilt. `_stylesReady` is now claimed at the **end** of `EnsureStyles`: set up front,
anything throwing partway leaves permanently-null styles, and a null style reaches `GUI.Window` and
takes the panel down for the session.

**BarrkUI inherited all three 1.1.8 fixes verbatim, so it inherits this bug too wherever it has a
collapsed state whose drawn size differs from its stored rect.** Check it before trusting that audit.
- **Milestone lines could overflow.** `GiltFrameTheme`'s styles are all `wordWrap = false` /
  `clipping = Clip` (`CalcSize` measures the string as one unwrapped line), and the tier-3
  Meat/Sweet/Eitr/Fish milestone lines join two full award descriptions — long enough to actually
  clip at `MinWidth`. `FeastLedgerGui` now builds `_wrappingValueStyle`/`_wrappingHeaderStyle`
  (`GiltFrameTheme.Value`/`Header` with `wordWrap = true`) for those lines; `GUILayout.Label` handles
  the height itself in this file since everything already renders inside `GUILayout.BeginArea`/
  `BeginScrollView`, so no manual `CalcHeight` was needed here (unlike BarrkUI's tooltip, which lays
  out raw `Rect`s outside `GUILayout`).

`libs-Tools/IMPLEMENTATIONS/SharedInfrastructure.md` has the `GiltFrameTheme` consumer-conflict note
(relevant if Fatty ever migrates off its local copy of the theme onto the shared one).

## Puke button and drunk blur (1.2.0)

**The Puke button must never strip a drink's vanilla status effect.** Every vanilla mead's SE
carries the shared `Mead` category, and `CanConsumeItem` refusing on that category *is* the game's
potion cooldown. Removing those SEs on puke turns the button into drink → puke → drink unlimited
healing. `PukeAction` clears `m_foods` and forces `UpdateFood(0, true)`, nothing else; Fatty's own
synergies/draughts/Drunk are re-derived from the stomach on that tick and end by themselves.

`PukeAction` borrows the start effects off the `Pukeberries` item's `m_consumeStatusEffect` rather
than naming `fx_Puke`/`sfx_Puke_*` (which keeps the male/female variant right). No SE is alive to run
`RemoveStartEffects`, so it destroys the instances itself after the SE's `m_ttl` (clamped 2–6 s), via
`ZNetView.Destroy` because `fx_Puke` is networked.

`DrunkVision` edits the camera's shared `PostProcessingProfile.motionBlur` in place, exactly as
vanilla's `CameraEffects.SetMotionBlur` does. Same compounding trap as the pristine stats above:
**capture the original settings only while not overriding**, or a mid-override capture reads back the
drunk values and sobering up leaves them on for good. Restore `enabled` from
`GraphicsSettingsManager.Instance.ActiveSettings.m_motionBlur`, not the captured bool: the player may
have flipped Motion Blur in the menu while drunk.

`thewall-share` (the sshfs mount this repo lives on) intermittently returns **EPERM on open** while
`stat` still works. Unconfirmed cause: the remote shell's `ulimit -n` is 1024 and sshfs maps a
generic SFTP failure to EPERM, so handle exhaustion fits. Seen 2026-11-16; it cleared by itself. Retry, or copy reference files to local
scratch first; it is not a permissions change.
