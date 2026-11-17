using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Fatty.Configuration;
using Fatty.Progression;

namespace Fatty.Patches
{
    // ---------------------------------------------------------------------
    // Feast export - getting per-player food history off the clients and onto the server, so
    // BarrkBOT can answer "what is X's favourite food", "who has eaten the most Serpent Stew",
    // "who is the biggest glutton on the server".
    //
    // THIS DOES NOT CONTRADICT NetworkPatches.cs. That file says Fatty applies food EFFECTS entirely
    // on the local player and ships no custom ZDO packing for the food SLOTS - both still true, and
    // nothing here touches either. This is a named routed-RPC channel carrying one compact string,
    // which is the ordinary supported way to move mod data, and it has none of the fragility of
    // fighting a fixed-size vanilla ZDO field. Do not delete this as contradicting that note.
    //
    // WHY THE DATA HAS TO CROSS THE WIRE AT ALL
    //
    // FoodLifetimeLog lives in Player.m_customData, which vanilla saves into the CHARACTER file - and
    // on a dedicated server that file is on the player's own machine. Reading it server-side was
    // investigated (FEAST_EXPORT.md section 2 proposed probing for it) and is IMPOSSIBLE, not merely
    // unreliable: a dedicated server has no Player instances at all. Player.GetAllPlayers() iterates
    // the local instance list, which is empty headless - see libs-Tools\VALHEIM-DEDICATED-SERVER-FACTS.md
    // ("Player visibility server-side"). So there is no arrangement of file paths or poll timers that
    // avoids a client report.
    //
    // WHY THE SERVER PULLS INSTEAD OF THE CLIENT PUSHING
    //
    // FEAST_EXPORT.md section 4 sketched a client-driven push on a timer. That would need the client
    // to read the ServerSync'd "Enable Feast Export" value to decide whether to send - and reading a
    // synced value early in a session to decide whether to send something is a documented race the
    // client usually loses (libs-Tools\PLAYER-IDENTITY-FACTS.md, checklist item 5). Inverting the
    // direction removes the race rather than timing around it:
    //
    //   * The SERVER owns the switch and is the only machine that reads it. Disabled means it never
    //     asks, so there is no traffic, no directory and no file - genuinely inert, not merely quiet.
    //   * The client only ever RESPONDS. It has no gate to get wrong and no timer to drift.
    //   * A client running an older Fatty has no handler registered for the request, so the call is
    //     silently dropped at its end. Mixed-version servers degrade to "that player has no record".
    //
    // WHAT IS SENT: absolute lifetime totals, never deltas. A delta stream starts every player at
    // zero, can never recover history from before the mod shipped, and drifts permanently from a
    // single dropped message. Absolute snapshots backfill a character's whole history on the first
    // sync and self-heal on the next one - which is what makes the retroactive backfill free.
    // ---------------------------------------------------------------------
    [HarmonyPatch]
    internal static class FeastExportPatches
    {
        // RPC names share ONE global hash namespace with the base game and every other mod, so both
        // are prefixed with the plugin GUID. See VALHEIM-DEDICATED-SERVER-FACTS.md, "Routed RPCs".
        private const string RpcRequest = FattyPlugin.PluginGUID + ".FeastRequest";
        private const string RpcSync = FattyPlugin.PluginGUID + ".FeastSync";

        // Payload sanity bounds. The payload arrives from a client that can send anything, so it is
        // treated as untrusted: length, entry count and per-entry magnitude are all capped, and a
        // payload that fails any of them is dropped whole rather than partially trusted.
        private const int MaxPayloadChars = 16384;   // a real diet is a few hundred bytes
        private const int MaxDistinctFoods = 2000;   // far past any real modded food list
        private const int MaxCountPerFood = 10000000;
        private const float MinSecondsBetweenSyncs = 5f;  // per sender; the server asks every 300s
        private const float MinSecondsBetweenResponses = 15f;  // client-side floor on answering

        private const float PeerScanSeconds = 2f;

        // Registration is Dictionary.Add on a ZRoutedRpc instance that is replaced every time
        // ZNet.Awake runs, so it is keyed on the INSTANCE REFERENCE rather than a bool: a stale
        // "already registered" flag would leave the next world with no handlers at all, and
        // re-registering on the same instance throws.
        private static ZRoutedRpc _registeredOn;

        private static bool _isServer;
        private static bool _started;

        // Latched only by an unexpected exception, so a fault stops the feature instead of throwing
        // once per frame forever. Kept separate from the config gate: an admin toggling the setting
        // off and on again is not an error and must not be treated as one.
        private static bool _faulted;

        private static float _nextPeerScan;
        private static float _nextFullRequest;
        private static float _nextWrite;

        // Client-side: when this machine last answered a request, so it cannot be made to spam.
        private static float _lastResponse;

        // Peers already asked this session, so a joiner is picked up immediately instead of waiting
        // out a full sync interval. Pruned against the live peer list so it cannot grow forever.
        private static readonly HashSet<long> _asked = new HashSet<long>();
        private static readonly Dictionary<long, float> _lastSyncHandled = new Dictionary<long, float>();

        // Read live rather than latched at world load, so an admin flipping the setting takes effect
        // on the next tick instead of at the next server restart. "Off" stays genuinely inert: the
        // server stops asking, stops writing, and never creates the directory.
        private static bool Exporting =>
            _isServer && !_faulted && ConfigManager.enableFeastExport != null && ConfigManager.enableFeastExport.Value;

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        [HarmonyPostfix]
        private static void ZNet_Awake_Postfix()
        {
            try
            {
                var rpc = ZRoutedRpc.instance;
                if (rpc == null || ReferenceEquals(rpc, _registeredOn)) return;
                _registeredOn = rpc;

                _isServer = ZNet.instance != null && ZNet.instance.IsServer();
                _faulted = false;
                _started = false;

                // Both handlers are registered unconditionally, and deliberately so.
                //
                // FEAST_EXPORT.md section 6 asked for registration itself to be gated on the setting.
                // It is not, for two reasons. On the CLIENT, the value is ServerSync'd and has not
                // arrived yet at this point in the session, so gating on it is the race described in
                // the header. On the SERVER, gating it here would freeze the setting at world load,
                // turning a live toggle into a restart.
                //
                // Nothing is given up by registering: a handler is a dictionary entry that produces
                // no traffic, no directory and no file unless a server that has the setting ON asks
                // for something. Every observable effect of "off" is still off - which is what the
                // requirement was actually protecting.
                rpc.Register(RpcRequest, new Action<long>(OnFeastRequest));
                rpc.Register<string, string>(RpcSync, OnFeastSync);
            }
            catch (Exception ex)
            {
                _faulted = true;
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Feast Export (registration). Reason: {ex}");
            }
        }

        // A world session ending must not leave the next one holding the previous world's players, a
        // registration flag pointing at a dead ZRoutedRpc, or unwritten changes.
        [HarmonyPatch(typeof(ZNet), "OnDestroy")]
        [HarmonyPostfix]
        private static void ZNet_OnDestroy_Postfix()
        {
            try
            {
                if (_started && Exporting) FeastRegistry.Flush();
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: final feast export write failed ({ex.Message}).");
            }
            finally
            {
                _isServer = false;
                _started = false;
                _faulted = false;
                _registeredOn = null;
                _asked.Clear();
                _lastSyncHandled.Clear();
                _lastResponse = 0f;
                FeastRegistry.Reset();
            }
        }

        // ------------------------------------------------------------------
        // Server tick
        // ------------------------------------------------------------------

        [HarmonyPatch(typeof(ZNet), "Update")]
        [HarmonyPostfix]
        private static void ZNet_Update_Postfix()
        {
            if (!Exporting)
            {
                _started = false;
                return;
            }

            try
            {
                float now = Time.realtimeSinceStartup;

                // First tick after the feature became active - at world load, or the moment an admin
                // turns it on. Loading here rather than at Awake is what keeps "off" from creating
                // the directory.
                if (!_started)
                {
                    _started = true;
                    FeastRegistry.LoadAll();

                    _nextPeerScan = now;
                    _nextFullRequest = now;
                    _nextWrite = now + ConfigManager.feastExportIntervalSeconds.Value;
                    _asked.Clear();

                    FattyPlugin.Instance.Log.LogInfo(
                        $"Fatty: feast export ON - writing {FeastRegistry.AggregatePath} every " +
                        $"{ConfigManager.feastExportIntervalSeconds.Value}s, asking clients every " +
                        $"{ConfigManager.feastSyncIntervalSeconds.Value}s.");
                }

                if (now >= _nextPeerScan)
                {
                    _nextPeerScan = now + PeerScanSeconds;

                    bool full = now >= _nextFullRequest;
                    if (full) _nextFullRequest = now + ConfigManager.feastSyncIntervalSeconds.Value;

                    RequestSnapshots(full);
                }

                if (now >= _nextWrite)
                {
                    _nextWrite = now + ConfigManager.feastExportIntervalSeconds.Value;

                    CaptureLocalPlayer();
                    FeastRegistry.Flush();
                }
            }
            catch (Exception ex)
            {
                _faulted = true;
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Feast Export (server tick). Reason: {ex}");
            }
        }

        // Asks connected clients for a fresh snapshot. `full` asks everyone; otherwise only peers
        // this session has never asked, so someone who just joined does not wait out a sync interval.
        private static void RequestSnapshots(bool full)
        {
            var peers = ZNet.instance?.GetPeers();
            if (peers == null) return;

            var live = new HashSet<long>();

            foreach (var peer in peers)
            {
                if (peer == null || !peer.IsReady()) continue;
                live.Add(peer.m_uid);

                if (!full && _asked.Contains(peer.m_uid)) continue;

                _asked.Add(peer.m_uid);
                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, RpcRequest);
            }

            _asked.IntersectWith(live);
            PruneRateLimiter(live);
        }

        // A listen host (and a single-player world, which is also a server) plays on the same machine
        // that writes the file, and is not in its own peer list - so its character would be the one
        // player missing from the export. Read directly instead of round-tripping an RPC to itself.
        // Always null on a dedicated server, where this is a no-op.
        private static void CaptureLocalPlayer()
        {
            var local = Player.m_localPlayer;
            if (local == null) return;

            string name = local.GetPlayerName();
            if (!FeastRegistry.IsUsableName(name)) return;

            FeastRegistry.Merge(name, local.GetPlayerID(), FoodLifetimeLog.Load(local), MilestoneStore.Load(local));
        }

        // ------------------------------------------------------------------
        // RPC handlers
        // ------------------------------------------------------------------

        // CLIENT SIDE. The server asked for this character's lifetime totals; send them back.
        //
        // Only the server may ask. A routed RPC from one client is relayed by the server to whatever
        // peer it names, so without this check any modded client could make every other client emit
        // a message on demand. It gains an attacker nothing directly - the reply below always goes to
        // the real server, never to whoever asked, so no diet ever reaches another player - but it is
        // free amplification, and the cooldown backs the check up rather than relying on it alone.
        private static void OnFeastRequest(long sender)
        {
            try
            {
                var local = Player.m_localPlayer;
                if (local == null) return;   // dedicated server, or not spawned in yet

                // Null on a server, and on a client this is the one peer entitled to ask.
                var server = ZNet.instance?.GetServerPeer();
                if (server == null || server.m_uid != sender) return;

                float now = Time.realtimeSinceStartup;
                if (_lastResponse > 0f && now - _lastResponse < MinSecondsBetweenResponses) return;
                _lastResponse = now;

                // The same compact strings already stored in m_customData - no reformatting, and no
                // per-food message traffic. The name is deliberately NOT sent: the server resolves it
                // from the character ZDO, which a client cannot forge by editing a payload.
                string foods = CountsCodec.Serialize(FoodLifetimeLog.Load(local)) ?? "";
                string categories = CountsCodec.Serialize(MilestoneStore.Load(local)) ?? "";

                ZRoutedRpc.instance.InvokeRoutedRPC(RpcSync, foods, categories);
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: feast snapshot could not be sent ({ex.Message}).");
            }
        }

        // SERVER SIDE. Everything below treats the payload as hostile input.
        private static void OnFeastSync(long sender, string foods, string categories)
        {
            try
            {
                if (!Exporting) return;
                if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

                float now = Time.realtimeSinceStartup;
                if (_lastSyncHandled.TryGetValue(sender, out float last) && now - last < MinSecondsBetweenSyncs)
                    return;   // unsolicited flood; the server asks at most once per sync interval

                // Stamped here rather than on success, so a message REJECTED below is also throttled.
                // Otherwise a peer whose character ZDO never resolves would be re-asked every scan and
                // answer every time, forever.
                _lastSyncHandled[sender] = now;

                if (foods == null || foods.Length > MaxPayloadChars) return;
                if (categories != null && categories.Length > MaxPayloadChars) return;

                // Identity comes from the routed-RPC sender resolved through ZNet, never from the
                // payload. A modded client can put any name in a message; it cannot become a
                // different connected peer.
                var peer = ZNet.instance.GetPeer(sender);
                if (peer == null) return;

                ResolveIdentity(peer, out string name, out long playerId);
                if (!FeastRegistry.IsUsableName(name))
                {
                    // m_characterID arrives AFTER the peer is otherwise ready, so a snapshot can land
                    // before the character ZDO resolves. Dropping it and retrying on the next tick is
                    // correct; recording it under a placeholder name would not be.
                    _asked.Remove(sender);
                    return;
                }

                var foodCounts = ParseChecked(foods);
                if (foodCounts == null) return;

                var categoryCounts = ParseChecked(categories ?? "") ?? new Dictionary<string, int>();

                FeastRegistry.Merge(name, playerId, foodCounts, categoryCounts);

                if (ConfigManager.debugMode.Value)
                {
                    FattyPlugin.Instance.Log.LogInfo(
                        $"Fatty: feast snapshot from {name} - {foodCounts.Count} distinct food(s).");
                }
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: feast snapshot from sender {sender} was rejected ({ex.Message}).");
            }
        }

        // Returns null when the payload is out of bounds, so the caller drops it whole. CountsCodec
        // already skips unparseable and non-positive entries rather than throwing, which is the
        // degradation wanted here; the caps below are what it does not do.
        private static Dictionary<string, int> ParseChecked(string serialized)
        {
            var counts = CountsCodec.Parse(serialized);
            if (counts.Count > MaxDistinctFoods) return null;

            foreach (int v in counts.Values)
                if (v > MaxCountPerFood) return null;

            return counts;
        }

        // The durable identity is s_playerID on the character ZDO, written beside s_playerName by the
        // same one-shot call - so one read gets both. ZNetPeer.m_playerName is whatever the client
        // said at handshake and is a fallback only. See libs-Tools\PLAYER-IDENTITY-FACTS.md.
        private static void ResolveIdentity(ZNetPeer peer, out string name, out long playerId)
        {
            name = peer.m_playerName;
            playerId = 0L;

            if (peer.m_characterID.IsNone()) return;

            var zdo = ZDOMan.instance?.GetZDO(peer.m_characterID);
            if (zdo == null || !zdo.IsValid()) return;

            string zdoName = zdo.GetString(ZDOVars.s_playerName, "");
            if (!string.IsNullOrEmpty(zdoName)) name = zdoName;

            playerId = zdo.GetLong(ZDOVars.s_playerID, 0L);
        }

        private static void PruneRateLimiter(HashSet<long> live)
        {
            if (_lastSyncHandled.Count == 0) return;

            List<long> stale = null;
            foreach (long uid in _lastSyncHandled.Keys)
            {
                if (live.Contains(uid)) continue;
                (stale ?? (stale = new List<long>())).Add(uid);
            }

            if (stale == null) return;
            foreach (long uid in stale) _lastSyncHandled.Remove(uid);
        }
    }
}
