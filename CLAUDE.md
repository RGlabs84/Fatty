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
