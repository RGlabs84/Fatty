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

Then build Release and copy the DLL to **both** the Gale `Fatty-Tester` profile and
`HexiumDist/plugins/`, never just one. Verify with matching `md5sum` across both copies.

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
