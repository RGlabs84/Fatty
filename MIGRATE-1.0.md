# Fatty — Valheim 1.0.7 migration audit

> **Verified 2026-09-11 on Valheim 1.0.12 (build 25253764 client / 25253791 server, network version 40) - no change
> needed, Fatty stays at 1.1.12.** The 1.0.7 DLL (md5 `75b1de2f7f6c738d2aa917591d3ac374`, identical in
> `bin/Release/net48/`, `HexiumDist/plugins/` and the boot profile `~/valheim-testbed/profiles/final-Fatty`) was
> re-checked against the 1.0.12 engine without rebuilding. Not bumped, not rebuilt, csproj / manifest / CHANGELOG
> untouched.
>
> **What changed in the engine (1.0.7 -> 1.0.12, assembly_valheim only):** 37 types / 55 method bodies / 5 methods
> added / 1 removed / 6 fields added / 1 removed / 1 const (`Version.c_networkVersion` 39 -> 40, inlined into 14
> methods). `assembly_utils`, `assembly_guiutils`, `gui_framework`, `assembly_postprocessing`, `Splatform` are
> IL-identical. Ground truth: `MigrationStation/diffs/{server,client}_assembly_valheim_1.0.7_to_1.0.12.md`
> (+ `.ildiff.txt`) and the decompile diff `libs-Tools/GAME-SNAPSHOT-1.0.7-build25185596/DECOMPILED/` vs
> `libs-Tools/1.0/DECOMPILED/`. Line numbers below are the 1.0.12 client decompile unless marked (server).
>
> **Every touchpoint between Fatty and that delta, with verdict** (greps over the source root excluding bin/obj:
> `HarmonyPatch|HarmonyTranspiler|CodeInstruction|CodeMatcher|AccessTools\.|GetMethod\(|GetField\(|GetProperty\(`
> plus every changed member name and every semantic point of the 1.0.12 delta):
> - **Fatty's 24 Harmony targets - none is a changed member.** `Player.CanEat` (l.11695), `RemoveOneFood` (11748),
>   `EatFood` (11758), `UpdateFood` x4 incl. `SynergyManager` (11827), `GetTotalFoodValue` x3 (11882), `OnSpawned`
>   (11905), `CanConsumeItem` (15442), `ConsumeItem` (15464) - Player's only body-changed members are
>   `HaveRequirementItems` and `TryPlacePiece` (`Patches/PlayerPatches.cs:224-816`, `Scanners/FoodScanner.cs:63`,
>   `Synergies/SynergyManager.cs:17`). `Hud.Awake` (47694), `Hud.UpdateFood` (48327), `Hud.UpdateStatusEffects`
>   (48950) - Hud is not a changed type (`Patches/HudPatches.cs:55,120,272`). `ObjectDB.Awake` (104110),
>   `ObjectDB.CopyOtherDB` (104116) - not a changed type (`Synergies/SynergyEffects.cs:136,155`). `ZNetScene.Awake`
>   (82170) - not a changed type (`Scanners/FoodScanner.cs:41`). `ZNet.Awake` (79298), `ZNet.OnDestroy` (79670),
>   `ZNet.Update` (80223) - ZNet's five changed members are `OpenServer` / `SendPeerInfo` / `RPC_PeerInfo` /
>   `ListContainsId` / `DelayThenRegisterCoroutine` (`Patches/FeastExportPatches.cs:105,143,172`). No transpiler
>   exists anywhere in Fatty (0 hits). The injected fields `Player.m_foods` / `m_foodRegenTimer` (9739) and
>   `Hud.m_foodBars/m_foodIcons/m_foodTime/m_foodBarRoot/m_foodBaseBar` (47440-47448) / `m_statusEffects` are
>   untouched (the delta's only field changes are the eight listed in the asmdiff). Unaffected.
> - **Vendored `Libs/ServerSync.cs` - 10 targets, one of them body-changed.** `ZNet.RPC_PeerInfo` (79954) carries a
>   Priority.First prefix + postfix (the `BufferingSocket` socket swap, `ServerSync.cs:753-900`) and the VersionCheck
>   prefix (`ServerSync.cs:1305`). Its IL hunk (`server ... .ildiff.txt` l.794, identical on the client) is exactly
>   three `ldc.i4.s 39 -> 40` literals - the "Network version check, their/mine" log string and the `if (num != 40)`
>   compare (server decompile l.79788-79809). Neither patch reads the peer's network version or the package; the
>   prefix swaps `ZRpc.m_socket`, the postfix flushes it after the config packages, the VersionCheck prefix compares
>   Fatty's own version strings. Unaffected. The other nine - `ZRpc.HandlePackage` (84135), `ZNet.Awake` (79298),
>   `ZNet.OnNewConnection` x2 (79815), `ZNet.Shutdown` (79614), `ZNet.Disconnect` (80207),
>   `FejdStartup.ShowConnectError` (97944; FejdStartup's changed members are `Awake`/`SetupGui`/`JoinServer`, the
>   literal only), `ConfigEntryBase.GetSerializedValue`/`SetSerializedValue` (BepInEx) - are not changed members.
>   Unaffected.
> - **`ZNet.ListContainsId(SyncedList, string)` invoked by reflection** (`AccessTools.DeclaredMethod`, called at
>   `ServerSync.cs:264/290`, `349/351`, `886/888` to decide the `lockexempt` admin flag). Signature unchanged
>   (80351); body-changed: the filtered-ID lookup went from `flag = list.Contains(...)` to `flag |= ...`
>   (server decompile l.80167-80171; ildiff l.817: `+ ldloc.1 ... + or`). **Behaviour-changed, inherited from
>   vanilla, no adaptation:** on 1.0.7 an admin listed in `adminlist.txt` only under the unfiltered spelling of the
>   ID was overwritten to "not admin" when the filtered form was absent; on 1.0.12 either spelling counts. Fatty
>   takes the game's answer, so its config-lock exemption now agrees with the server's own admin check in that
>   edge case - the correct direction. The companion reflection targets `ZNet.m_adminList`, `ZNet.GetPeer(ZRpc)`
>   (80696), `ZNet.m_connectionStatus`, `ZNet.m_onlineBackend`, `ZRpc.m_socket`, `ZRpc.m_functions`,
>   `ZNetPeer.m_socket`, `ZRoutedRpc.m_peers`, `ZPlayFabSocket.m_remotePlayerId` are all unchanged.
> - **Game interface implemented:** only ServerSync's `BufferingSocket : ZPlayFabSocket, ISocket`
>   (`ServerSync.cs:756`). `ISocket` is textually identical (1.0.7 l.71891 vs 1.0.12 l.71941; all 25 interface
>   declarations identical on client and server; the decompile diff contains no `interface` line) and
>   `ZPlayFabSocket` is not a changed type. Fatty's own classes implement no game interface (`FattyPlugin :
>   BaseUnityPlugin` only). Unaffected.
> - **New non-defaulted parameters:** none in 1.0.12 - the only signature reshape is the private
>   `ZDOMan.ConvertInventories(List<ZDO>, World)` -> `ConvertInventories(List<ZDOID>, World, int)`, never called.
>   **Constructor reshapes:** none - only the bodies of `CookingStation..ctor`, `PresentManager..ctor`,
>   `PlayerProfile..cctor`, `Version..cctor` changed (field initialisers); the only game ctors Fatty uses are
>   `Player.Food` (`PlayerPatches.cs:282`) and `ZPackage` (ServerSync), both untouched. Unaffected.
> - **Direct engine calls in Fatty's own code:** `Chat.instance.HasFocus()` (41667) and `Console.IsVisible()`
>   (42939) in the F8 hotkey gate (`HudPatches.cs:512-513`) - Chat's only change is `isAllowedCommand`, Console's
>   only change is the version literal in `Awake`; `Menu/TextInput/InventoryGui.IsVisible()` - InventoryGui's changes
>   are `DoCrafting`/`UpdateAchievementsList`. `ZDOMan.instance.GetZDO(ZDOID)` (76895) + `zdo.GetString(s_playerName)`
>   / `GetLong(s_playerID)` on the character ZDO (`FeastExportPatches.cs:388-395`) - ZDOMan's changed members are the
>   world-load item-slot conversion (`Load`, `ConvertContainers`, `ConvertInventories`, `GetConvertHash`,
>   `ConvertPrefabStrings`); `ZDO`/`ZDOVars` unchanged. `ZNet.instance.IsServer()/GetPeers()/GetServerPeer()/
>   GetPeer(long)`, `ZRoutedRpc.Register/InvokeRoutedRPC` (`FeastExportPatches.cs:115-332`), `Player.m_customData`
>   (`MilestoneStore.cs`, `FoodLifetimeLog.cs`), `Player.m_baseHP/m_baseStamina/m_maxCarryWeight`
>   (`FoodMilestones.cs:140-184`), `SEMan.Add/Remove/Get/HaveStatusEffect`, `ModifyHealthRegen`,
>   `ObjectDB.GetStatusEffect`, `SE_Stats`/`StatusEffect` members - none changed. Unaffected.
> - **Mirrors of vanilla logic:** `EatFood_Prefix` replaces `Player.EatFood`, the `GetTotalFoodValue` trio stashes
>   `m_foods` around the original, the two `UpdateFood` prefixes mirror the 10 s regen timer, and
>   `HudPatches.UpdateFood_Prefix` re-implements `Hud.UpdateFood`. All four originals are absent from the changed
>   list (IL-identical), so the mirrors are still faithful. Unaffected.
> - **Console commands:** Fatty registers none (`ConsoleCommand\(|Terminal\.|ConsoleEventArgs|hideBehind|isCheat`:
>   0 hits), so the dev-command gating rewrite (`ConsoleCommand.IsValid/ShowCommand`, `Chat.isAllowedCommand`) and
>   the new `yesiuseddevcommandsbutiwantmyachievementsanyway` command have nothing to bite. n/a.
> - **Version / network version:** no `Version.*`, `GetVersionString`, `c_networkVersion`, `GameVersion` or
>   network-version reference anywhere. The only `CurrentVersion` is ServerSync's mod-version string
>   (`Configuration/ConfigManager.cs:13` -> `ConfigSync.CurrentVersion = FattyPlugin.PluginVersion`), compared
>   between peers' Fatty copies, not against the game. The game-side consequence of network version 40 (a 1.0.7
>   peer cannot join a 1.0.12 server at all) is vanilla and mod-independent. Unaffected.
> - **`PlayerProfile.s_bypassCheatChecks` field -> property:** 0 source hits; refcheck resolves all 4826 references on
>   both sides, so no IL `ldsfld` on it exists. n/a.
> - **Item data on ZDOs / item stands / containers** (`ItemDrop.SaveToZDO` guard flip, `ZDOMan.ConvertContainers` /
>   `GetConvertHash`): Fatty never calls `SaveToZDO`/`LoadFromZDO`, touches no `ItemStand`/`Container`, and writes
>   no ZDO key of its own - both lifetime stores live in `Player.m_customData` (character file). n/a.
> - **Cooking / smelting / fermenting output and cheated-item flags** (`CookingStation.m_spawnFullDurability`,
>   `Smelter.Spawn`, `Fermenter.DelayedTap`, `Inventory.Changed`/`m_cheatedPopup`, `Inventory.AddItem`): 0 hits.
>   `FoodScanner` classifies by `ObjectDB.m_items` + `SharedData`, never by station output, and Fatty never calls
>   `Inventory.AddItem` (only a comment at `PlayerPatches.cs:114`). n/a.
> - **Landing / `m_onLand` (`Character.UpdateGroundContact`), `Attack.DoMeleeAttack` snow gate, terrain ops
>   (`TerrainComp.PaintCleared`), `Player.HaveRequirementItems`/`TryPlacePiece`, `InventoryGui.DoCrafting`,
>   `Destructible`/`DropOnDestroyed`/`MineRock`/`Piece.DropResources`, `Achievements`/`AchievementUnlockPopup`,
>   `GrapplingPoint`/`RuneStone`/`PresentManager`, `ItemSets`, `ZoneSystem.TestSpawnLocation`, `MasterClient`,
>   `ZSteamMatchmaking`/`ZPlayFab*`/`ServerListGui`/`ServerOptionsGUI`, `Container.AddDefaultItems`:** 0 hits each.
>   n/a.
> - Reflection into other mods (`Compat/FoodDurationMultiplierCompat.cs`, BepInEx `ConfigFile.OrphanedEntries` in
>   `ConfigManager.cs:339`) and into Unity (`ImageConversion.LoadImage`, `SynergyIcons.cs:94`) touch nothing in
>   assembly_valheim. n/a.
>
> **Evidence.** refcheck against the 1.0.12 Steam Managed folders, staged DLL: client `checked 4826 references, 34
> Harmony targets (0 dynamic) - RESULT: OK`, server the same
> (`scratchpad/refcheck-1012/final-Fatty-Fatty-{client,server}.txt`, `SUMMARY.txt` row `final-Fatty client=0
> server=0`). Boot on the real 1.0.12 Linux dedicated server, BepInEx 5.4.2350, profile `final-Fatty`
> (`scratchpad/boot-1012/final-Fatty.txt`): `BOOTED after 28s`; `BepInEx/LogOutput.log` l.16-22 `Loading [Fatty
> 1.1.12]`, `Fatty v1.1.12 loaded successfully! Time to get fat.`, `Scanning ObjectDB for custom food items...`,
> `ObjectDB not populated yet; deferring food scan until player spawn.`, `expanded food HUD 3 -> 5 slots (icons
> grouped).`, `food HUD ready with 5 slot(s); custom steady draw active.`; `server-console.log:141 Valheim version:
> l-1.0.12 (network version 40)`, `:185 Registered 'com.wubarrk.fatty ConfigSync' RPC`. The errors section holds only
> the six headless video-decode shader-pass notes every profile prints. Compared line-for-line with the preserved
> 1.0.7 run of the same profile (`~/valheim-testbed/profiles/final-Fatty/logs-1.0.7-run/LogOutput.log` l.16-22 and
> `server-console.log:142/187`): identical plugin output, only the engine's version line differs.
>
> **Left open (unchanged from 2026-09-09).** The boot proves `PatchAll` and every static binding; eating, stacking,
> the 5-slot HUD, the ledger and the feast export only run with a spawned client. Nothing in this delta touches any
> of those paths.

> **Applied 2026-09-09 - Fatty 1.1.12.** Audited from scratch (there was no prior report for this
> project). **No source change was needed**: all 34 Harmony targets (24 of Fatty's own plus the
> vendored `Libs/ServerSync.cs`) and every game member Fatty binds to - the player's food list and
> regen timer, the food HUD arrays, the status-effect list, `ObjectDB`, the `ZNet` lifecycle - are
> present in 1.0.7 with the shape the code already binds to, and every injected `___field` and
> named parameter still exists under the same name. Nothing on the 1.0.7 change catalogue is
> referenced by Fatty (no `Inventory.Load` patch, no `VisEquipment`/`ItemStand`, no sector API, no
> save paths, no `Version.*`, no console commands, no `GetTooltip`). What was done: version 1.1.11 ->
> 1.1.12 in lockstep in the three places CLAUDE.md names (`Fatty.csproj`, `Plugin.cs`,
> `HexiumDist/manifest.json`), manifest dependency `denikson-BepInExPack_Valheim-5.4.2333` ->
> `5.4.2350` (the 1.0 pack, as Wonderland/MigrationStation already did), a `HexiumDist/CHANGELOG.md`
> entry, this file, and the built DLL copied to `HexiumDist/plugins/` by hand (Fatty.csproj has no
> post-build copy; the Gale profile deploy in CLAUDE.md is Windows-only and was skipped).
>
> **Evidence.** `dotnet build Fatty.csproj -c Release --no-incremental`: 0 errors.
> refcheck (Mono.Cecil, exact signatures) on `bin/Release/net48/Fatty.dll`:
> client `checked 4826 references, 34 Harmony targets (0 dynamic) - RESULT: OK`;
> server `checked 4826 references, 34 Harmony targets (0 dynamic) - RESULT: OK`.
> Boot-check on the real 1.0.7 Linux dedicated server (BepInEx 5.4.2350, profile
> `~/valheim-testbed/profiles/fatty`, port 2661, `Newtonsoft.Json.dll` beside the plugin as the
> JsonDotNET dependency would place it): `BOOTED after 25s`, `Loading [Fatty 1.1.12]`,
> `Fatty v1.1.12 loaded successfully! Time to get fat.`, `Scanning ObjectDB for custom food items...`,
> `ObjectDB not populated yet; deferring food scan until player spawn.`,
> `expanded food HUD 3 -> 5 slots (icons grouped).`, `food HUD ready with 5 slot(s); custom steady
> draw active.`; `Registered 'com.wubarrk.fatty ConfigSync' RPC` in the Unity log. The errors/warnings
> section of the boot report was empty. Logs: `~/valheim-testbed/profiles/fatty/BepInEx/LogOutput.log`
> and `.../profiles/fatty/server-console.log`.
>
> **Left open.** The boot proves `PatchAll` and every static binding; the food/regen/synergy paths
> themselves only run once a player has spawned and eaten, which a headless server never does. A
> client-side in-game pass on 1.0.7 (eat, stack, watch the 5-slot HUD and the ledger) is still worth
> doing. Nothing in this audit suggests it will find anything. `Libs/ServerSync.cs` is the vendored
> copy and did not need an edit.

Audited 2026-09-09 against `libs-Tools/1.0/DECOMPILED/assembly_valheim.decompiled.cs` (client 1.0.7,
build 25185596) and `assembly_valheim_SERVER.decompiled.cs` (server 1.0.7, build 25185644). Line
numbers are from the client decompile. Pre-1.0 shapes checked in
`libs-Tools/OLD/OLD-DECOMPILED ASSEMBLY VALHEIM-build21981559/` where relevant.

---

## 1. Harmony targets

| Patch (file) | Target | 1.0.7 shape (decompile line) | Injected params / fields checked | Verdict |
|---|---|---|---|---|
| `FoodScanner` | `ZNetScene.Awake` | `private void Awake()` (82099) | `__instance` | unchanged |
| | `Player.OnSpawned` by-name | `public void OnSpawned(bool spawnValkyrie)` (11898), one overload | `__instance` | unchanged |
| `PlayerPatches` | `Player.CanEat` by-name | `public bool CanEat(ItemDrop.ItemData item, bool showMessages)` (11688), one overload on Player (`ItemData.CanEat()` at 70577 is a different type) | `item`, `showMessages`, `__result` | unchanged |
| | `Player.EatFood` | `public bool EatFood(ItemDrop.ItemData item)` (11751) | `item`, `ref ___m_foods` | unchanged |
| | `Player.GetTotalFoodValue` (prefix, postfix, finalizer) | `private void GetTotalFoodValue(out float hp, out float stamina, out float eitr)` (11875) | `hp`, `stamina`, `eitr`, `___m_foods` | unchanged |
| | `Player.UpdateFood` (x3, plus `SynergyManager`) | `private void UpdateFood(float dt, bool forceUpdate)` (11820), one overload | `dt`, `forceUpdate`, `___m_foodRegenTimer`, `___m_foods` | unchanged |
| | `Player.CanConsumeItem` by-name on `typeof(Player)` | `public override bool CanConsumeItem(ItemDrop.ItemData item, bool checkWorldLevel = false)` (15435) | `item`, `checkWorldLevel`, `__result` | unchanged |
| | `Player.ConsumeItem` | `public override bool ConsumeItem(Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel = false)` (15457) | `item`, `__result` | unchanged |
| | `Player.RemoveOneFood` | `public bool RemoveOneFood()` (11741) | `ref ___m_foods`, `__result` | unchanged |
| `HudPatches` | `Hud.Awake` | `private void Awake()` (47655) | `___m_foodIcons`, `___m_foodBars`, `___m_foodTime`, `___m_foodBarRoot` | unchanged |
| | `Hud.UpdateFood` | `private void UpdateFood(Player player)` (48288), one overload | `player`, the four above + `___m_foodBaseBar` | unchanged |
| | `Hud.UpdateStatusEffects` | `private void UpdateStatusEffects(List<StatusEffect> statusEffects)` (48911) | `statusEffects`, `___m_statusEffects` | unchanged |
| `SynergyEffects` | `ObjectDB.Awake` | `private void Awake()` (104039) | `__instance` | unchanged |
| | `ObjectDB.CopyOtherDB` | `public void CopyOtherDB(ObjectDB other)` (104045) | `__instance` | unchanged |
| `FeastExportPatches` | `ZNet.Awake` / `ZNet.OnDestroy` / `ZNet.Update` | 79227 / 79599 / 80152, all `private void` | none | unchanged |
| `Libs/ServerSync.cs` | `ZRpc.HandlePackage`, `ZNet.Awake/OnNewConnection/Shutdown/RPC_PeerInfo/Disconnect`, `FejdStartup.ShowConnectError`, `ConfigEntryBase.Get/SetSerializedValue` | present | | unchanged (boot log: ConfigSync RPC registered) |

Injected fields, verified by declaration:
`Player.m_foods` = `private readonly List<Food> m_foods` (9734; already `readonly` pre-1.0 at old 15605,
so the `ref ___m_foods` injections are the same IL they always were);
`Player.m_foodRegenTimer` = `private float` (9732);
`Hud.m_foodBars/m_foodIcons` = `public Image[]`, `m_foodTime` = `public TMP_Text[]`,
`m_foodBarRoot/m_foodBaseBar` = `public RectTransform` (Hud 117-125);
`Hud.m_statusEffects` = `private List<RectTransform>` (47553).

## 2. Direct engine members on or near the catalogue

| Member (site) | 1.0.7 | Verdict |
|---|---|---|
| `Player.Food` (`m_name`, `m_item`, `m_time`, `m_health`, `m_stamina`, `m_eitr`, `CanEatAgain()`) | 9418+ | unchanged |
| `ItemDrop.ItemData.SharedData` food fields, `SEMan`/`SE_Stats`/`StatusEffect` members, `ObjectDB.m_items`, `Game.m_foodRate` | compile-checked against the 1.0.7 assemblies and exact-signature resolved by refcheck | unchanged |
| `ImageConversion.LoadImage` (Synergies/SynergyIcons.cs, via `AccessTools.TypeByName`) | Unity, unchanged | n/a |
| `Input.mousePosition` / `Event.mousePosition` (Patches/FeastLedgerGui.cs) | Unity, not `ZInput.mousePosition` | n/a |
| `Inventory.Load`, `VisEquipment`, `ItemStand`, `GetTooltip`, `SaveToZDO/LoadFromZDO`, sector API, save paths, `Version.*`, `Terminal.ConsoleCommand`, `ZNet.PlayerInfo`, `Minimap` | not referenced anywhere in Fatty | n/a |

## 3. What changed in the repo

- `Fatty.csproj` `<Version>` 1.1.11 -> 1.1.12; `Plugin.cs` `PluginVersion` 1.1.12;
  `HexiumDist/manifest.json` `version_number` 1.1.12 and dependency `denikson-BepInExPack_Valheim-5.4.2350`.
- `HexiumDist/CHANGELOG.md`: new `[1.1.12]` entry.
- `HexiumDist/plugins/Fatty.dll`: rebuilt and copied by hand (md5 `75b1de2f7f6c738d2aa917591d3ac374`,
  identical to `bin/Release/net48/Fatty.dll`). Per CLAUDE.md this DLL is never committed.
- No `.cs` file other than `Plugin.cs` (version constant) was edited. Git was not touched; the 18
  dirty files in the working tree are exactly as they were, with these edits on top.
