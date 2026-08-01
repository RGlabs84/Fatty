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
