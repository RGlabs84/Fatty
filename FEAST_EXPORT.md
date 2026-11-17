# Feast export — getting per-player food history off the client and onto the server

**Goal.** BarrkBOT should be able to answer "what's Fatty's favourite food", "who has eaten the
most Serpent Stew", "who is the biggest glutton on the server". Fatty already has every number
needed. None of it currently exists anywhere BarrkBOT can reach.

Read this whole file before writing code. The first section is the reason the obvious approach
does not work, and it is the part that decides the design.

---

## 1. The problem: Fatty is client-authoritative, on purpose

`FoodLifetimeLog` already keeps exactly the data we want, per prefab, for the lifetime of a
character:

```
Fatty.FoodLifetimeCounts = "CookedMeat=142,Sausages=30,Cloudberry=88"
```

It lives in `Player.m_customData`, which vanilla saves into the character file. On a dedicated
server **that file is on the player's own machine**, not the server's.

And the write only ever happens client-side. `FoodLifetimeLog.RecordEaten` is called from Fatty's
`EatFood` patch (`Patches/PlayerPatches.cs:310`), and `EatFood` runs on the owning client. The
dedicated server never executes it for a remote player, so the server-side `Player` object never
accumulates anything.

This is deliberate, and `Patches/NetworkPatches.cs` is a 26-line file that exists only to say so:

> Food effects in Fatty are applied entirely on the LOCAL player [...] This file is intentionally
> code-free; it documents the decision.

So the data has to cross the network. There is no arrangement of file paths that avoids it.

**Do not "fix" this by making the server write files from its own `Player` objects without doing
step 2 first.** It will appear to work — you will get a file, with a player name in it, and every
count zero — and that failure is invisible until someone asks the bot a question and gets a
confidently wrong answer. That exact class of bug (a tool that returns plausible, wrong data, which
the bot then relays faithfully) has already cost this project a public correction in
`#general-chat`.

---

## 2. First, spend ten minutes finding out if you need any of this

Valheim syncs *some* player state to the server. If `m_customData` is among it, this entire job
collapses to "read the dictionary server-side and write a file", with no networking at all.

Verify before designing. In a server-side poll (Fatty already runs on the server; it is in the
server's plugin list), log what the server can actually see:

```csharp
foreach (var p in Player.GetAllPlayers())
{
    p.m_customData.TryGetValue("Fatty.FoodLifetimeCounts", out string raw);
    FattyPlugin.Instance.Log.LogInfo($"[FeastExport probe] {p.GetPlayerName()} customData={p.m_customData?.Count ?? -1} counts={raw ?? "<null>"}");
}
```

Have someone log in, eat something, and read the server log.

- **Counts present** → take **Path A**. Delete the RPC plan; you do not need it.
- **`<null>` or an empty dictionary** → take **Path B**.

Write the answer down in this file when you find it, with the date and the game version you tested
on. The next person should not have to re-run this.

> **Result (2026-08-22, shipped in Fatty 1.1.11): PATH B. The server cannot see `m_customData` for
> any remote player, and no probe run is needed to establish it — the probe above could not have
> worked as written.**
>
> `Player.GetAllPlayers()` iterates the **local instance list**, which is empty on a dedicated
> server. There are no `Player` objects there at all, so there is no `m_customData` to read: this is
> architecture, not a timing or sync question, and it cannot change with a game version. Documented
> and cross-checked against both the client and server decompiles in
> `libs-Tools\VALHEIM-DEDICATED-SERVER-FACTS.md` ("Player visibility server-side"), and independently
> confirmed the hard way by TheRavensCall, whose entire combat-tracking system read zero for every
> player for the same reason (`PAUSED\TheRavensCall-OLD-1.2.1\KILL_TRACKING_FINDINGS.md`).
>
> **Do not re-run the probe.** If you write one anyway, note that it would print `customData=-1` for
> zero players and look like a clean negative for the wrong reason.
>
> Path A above is therefore dead and is kept only to explain why. Everything shipped follows Path B,
> with the deviations recorded in section 4a.

---

## 3. Path A — server reads `m_customData` directly

Only if the probe showed real counts.

Add `Progression/FeastExport.cs` with a poll on a timer, modelled on TheRavensCall's
`PollAllPlayers` (that mod already does this successfully for `m_foods`). Every
`FeastExportIntervalSeconds`:

1. For each connected player, `FoodLifetimeLog.Load(player)`.
2. Merge into an in-memory registry keyed by display name.
3. Write the aggregate file (section 5).

Players who are offline must keep their last known totals — hold the registry in memory *and*
reload it from the aggregate file on startup, or every relog wipes the leaderboard back to whoever
happens to be online.

---

## 4. Path B — client pushes to the server

If the server cannot see `m_customData`.

Use `ZRoutedRpc`, not custom ZDO packing. `NetworkPatches.cs` rejected ZDO packing for the food
*slots*, and it was right to — that meant fighting a fixed-size vanilla field. This is different: a
single string on a named RPC channel is the ordinary, supported way to send mod data, and it has
none of the fragility that note was warning about. Say so in a comment next to the registration, or
someone will later delete this as contradicting that file.

**Client side.** Register once, then send:

```csharp
// Cheap: the payload is the same compact string already stored in m_customData.
ZRoutedRpc.instance.InvokeRoutedRPC(
    ZRoutedRpc.everybody,           // server handles it; see note below
    "Fatty_FeastSync",
    Player.m_localPlayer.GetPlayerName(),
    CountsCodec.Serialize(FoodLifetimeLog.Load(Player.m_localPlayer)) ?? "");
```

Send on: login (once settled), logout/quit, and on a slow timer (**default 300s** — this is a
leaderboard, not telemetry; do not send on every bite). Skip the send entirely when the serialized
string has not changed since the last one, which for an idle player is always.

**Server side.** Register the same channel name, and reject rubbish rather than trusting it:

```csharp
ZRoutedRpc.instance.Register<string, string>("Fatty_FeastSync", OnFeastSync);

static void OnFeastSync(long sender, string playerName, string serialized)
{
    if (!ZNet.instance.IsServer()) return;              // clients also receive; ignore there
    if (string.IsNullOrEmpty(playerName)) return;
    if (serialized.Length > MaxPayloadChars) return;    // 16 KB is already absurd; cap it
    var counts = CountsCodec.Parse(serialized);         // skips unparseable entries, never throws
    if (counts.Count > MaxDistinctFoods) return;        // 2000 is far past any real diet
    FeastRegistry.Merge(playerName, counts);
}
```

Treat the payload as **untrusted**. It arrives from a client that can send anything. Cap length and
entry count, and take the incoming value rather than adding to a running total, so a replayed or
duplicated message cannot inflate anyone's numbers. `CountsCodec.Parse` already skips unparseable
entries instead of throwing, which is the behaviour you want here.

`CountsCodec.Encode/Decode/Parse/Serialize` are currently `internal` — they are in the same
assembly, so no access change is needed. Do not widen them to `public` for this.

---

## 4a. What actually shipped, and where it departs from section 4

Four deliberate changes. Each one is a bug that section 4's sketch would have shipped.

**1. The SERVER PULLS; the client never pushes on its own.** Section 4 has the client sending on a
timer, which means the client must read `EnableFeastExport` to know whether to send — and that value
is ServerSync'd, so early in a session the client is reading its own default, not the server's.
`libs-Tools\PLAYER-IDENTITY-FACTS.md` checklist item 5 names this exact race. Inverting the direction
removes it instead of timing around it: the server asks (`wubarrk.fatty.FeastRequest`), the client
answers (`wubarrk.fatty.FeastSync`). The server owns the switch and is the only machine that reads
it, so "off" means it never asks — no traffic, no directory, no file. A client has no gate to get
wrong. A client on an older Fatty has no handler and is silently skipped.

**2. `ZRoutedRpc.everybody` does not exist, and would have been wrong if it did.** It is
`ZRoutedRpc.Everybody` (capital E) — and it is `0L`, meaning *every peer plus the sender locally*, so
section 4's snippet would have broadcast every player's diet to every other client and handled it on
the sender too. The correct call is `InvokeRoutedRPC(name, args)` with **no** peer id, which
auto-targets the server (decompile L71170).

**3. RPC names are prefixed with the plugin GUID.** `"Fatty_FeastSync"` sits in a hash namespace
shared with the base game and every other mod, and a collision is silent. Also: registration is
`Dictionary.Add` on a ZRoutedRpc instance **replaced on every `ZNet.Awake`** — double-registering
throws, and a stale `_registered` bool leaves the next world with no handlers. It is keyed on the
instance reference for that reason.

**4. The player name is not in the payload.** Section 4 sends `GetPlayerName()` from the client,
which a modded client can set to anything, including another player's name. The server resolves
identity from the routed-RPC `sender` through `ZNet.GetPeer` → the character ZDO's `s_playerName` /
`s_playerID` instead. A client can lie about its own diet; it cannot become a different connected
peer. `"Stranger"` and `"..."` are rejected as placeholders.

Two smaller ones: the export switch is read **live** rather than latched at `ZNet.Awake`, so an admin
toggling it does not need a restart (which also means handler registration cannot be gated on it —
registration is inert, and every observable effect of "off" is still off); and files are written with
a **BOM-less** UTF-8, because `Encoding.UTF8` emits a byte-order mark and a BOM is not valid JSON —
`File.ReadAllText` tolerates one, which is precisely why it survives being written, but `json.load`
and `jq` both reject the file outright.

## 5. Output format

Two files, in `BepInEx/config/Fatty/feasts/`. Create the directory on startup.

**Per player**, `feasts/<sanitized name>.json` — one file each, so a corrupt write loses one
player, not the server's whole history. Sanitize with `Path.GetInvalidFileNameChars()` exactly as
`PlayerRegistry.SanitizedFileName` does.

**Aggregate**, `Fatty/BarrkBOT_feasts.json` — this is the one BarrkBOT reads. *(Shipped name; it
sits beside the `feasts/` directory rather than inside it, and follows the `BarrkBOT_*.json`
convention TheRavensCall moved to. Confirmed against BarrkBOT's own discovery scanner, which matches
`/^(?:barrkbot[_-][\w.-]*|steve_export)\.json$/i` breadth-first to depth 2 under `config/` — so the
per-player files are correctly not picked up.)*

```json
{
  "generated_at": "2026-08-16T06:12:44.0000000Z",
  "source": "Fatty 1.1.11",
  "players": {
    "Fatty": {
      "name": "Fatty",
      "last_updated": "2026-08-16T06:12:40.0000000Z",
      "total_meals": 1163,
      "distinct_foods": 34,
      "favourite_food": "CookedMeat",
      "food_counts": { "CookedMeat": 142, "Sausages": 30, "Cloudberry": 88 }
    }
  }
}
```

Rules that are not optional, each one because BarrkBOT has already been burned by its absence:

- **Emit `favourite_food` and `total_meals` yourself.** Do not make the reader derive them. A
  computed field that both sides calculate is a field that will eventually disagree.
- **`favourite_food` must be `null` when there is a tie or when `total_meals` is 0.** Silently
  picking the first of a tie invents a fact.
- **Raw prefab names in `food_counts`** (`CookedMeat`, not "Cooked Meat"). BarrkBOT resolves display
  names against the wiki index; two different prettifiers would drift.
- **Never omit a player because they are offline.** Absent means "never seen", and the reader will
  say so.
- **Write atomically**: write `barrkbot_feasts.json.tmp`, then `File.Replace`/move over the real
  path. BarrkBOT pulls this file over FTP on its own schedule and will otherwise eventually read a
  half-written one.

Same shape and conventions as TheRavensCall's `barrkbot_players.json`, deliberately — keyed by
display name, aggregate plus per-player, `generated_at` at the top. One reader handles both.

---

## 6. Config

In `Configuration/ConfigManager.cs`, section `Feast Export`:

| Key | Default | Note |
|---|---|---|
| `EnableFeastExport` | `true` | Off = write nothing, register no RPC |
| `FeastExportIntervalSeconds` | `60` | Server-side file write |
| `FeastSyncIntervalSeconds` | `300` | Path B only, client push |

Gate on `EnableFeastExport` **before** registering the RPC or creating the directory, so "off"
means genuinely inert rather than quiet.

---

## 7. Definition of done

Verified (2026-08-22, Fatty 1.1.11):

- [x] Probe from section 2 answered, with its reasoning, above — **Path B**
- [x] Tie case produces `"favourite_food": null`, not an arbitrary pick
- [x] Zero-meal player is listed with `total_meals: 0`, not omitted
- [x] A player logged **out** still appears, with their totals intact
- [x] "Server restarted" — registry reloads from `feasts/*.json` and totals survive
- [x] A replayed/duplicated snapshot does not inflate a total (absolute, assigned not accumulated)
- [x] Malformed payload is rejected without an exception (length, entry-count and magnitude caps;
      `CountsCodec.Parse` skips unparseable entries rather than throwing)
- [x] Two names that sanitize to the same filename stay separate on disk
- [x] Output is valid JSON to a parser that does not special-case a BOM

The first nine are covered by a standalone harness that compiles the real `FeastRegistry.cs` and
`CountsCodec.cs` against stubs for `BepInEx.Paths` and `FattyPlugin.Log` — the same throwaway-csproj
pattern CLAUDE.md prescribes for `FoodWordClassifier`, and for the same reason: "it built" says
nothing about whether the file is *right*, and every consumer of this file is another program.

Still needs a live server (cannot be established from source):

- [ ] Two players logged in, ate different foods, both appear with correct counts
- [ ] `EnableFeastExport = false` produces no directory and no file on a real server
- [ ] A pre-1.1.11 client connected to a 1.1.11 server is skipped without errors in the log

## 8. Release, per CLAUDE.md

Bump all three in lockstep: `Fatty.csproj` `<Version>`, `Plugin.cs` `PluginVersion`,
`HexiumDist/manifest.json` `version_number`. Build Release, copy the DLL to **both** the Gale
`Fatty Only Tester` profile and `HexiumDist/plugins/`, verify with matching `md5sum`. Zip with
`System.IO.Compression.ZipArchive`, never `Compress-Archive`. Commit source only — no `bin/`,
`obj/`, no `HexiumDist/*.zip`, no `HexiumDist/plugins/*.dll`.

---

## 9. What BarrkBOT does with it

Reads `BepInEx/config/Fatty/feasts/barrkbot_feasts.json` over FTP — the same
`/BepInEx/config` tree it already pulls mod configs from, so no new access is needed. It degrades
to "no feast data yet" when the file is missing, so this can ship in either order.

The only hard requirement from this side is that a **missing player is distinguishable from a
player with zero meals**. "Nobody has eaten a Lox pie" and "we have no record for that player" are
different answers, and a leaderboard that conflates them will get someone told they have never
eaten anything.
