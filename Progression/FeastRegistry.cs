using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Newtonsoft.Json;

namespace Fatty.Progression
{
    // ---------------------------------------------------------------------
    // The server-held feast record: every player this server has ever heard from, and how many of
    // each food they have eaten in their character's lifetime.
    //
    // This is the SERVER's copy of what FoodLifetimeLog holds per character. It exists because that
    // log lives in Player.m_customData, which is saved into the CHARACTER file - a file that sits on
    // the player's own machine on a dedicated server. See Patches\FeastExportPatches.cs for how the
    // data crosses the wire and why it has to.
    //
    // Keyed by display name, deliberately: that is the key BarrkBOT's own player store already uses
    // and the key TheRavensCall's export is keyed by, so one reader handles both. The durable
    // identity (s_playerID) is carried alongside as player_id rather than used as the key - it is
    // strictly better as an identity, but switching the key would fork the reader for no gain.
    //
    // Records are never dropped for going offline. "Absent" means NEVER SEEN, which is a different
    // answer from "has eaten nothing", and a leaderboard that conflates them will tell someone they
    // have never eaten anything.
    // ---------------------------------------------------------------------
    internal class FeastRecord
    {
        internal string Name = "";
        internal long PlayerId;
        internal string LastUpdated = "";
        internal Dictionary<string, int> FoodCounts = new Dictionary<string, int>();
        internal Dictionary<string, int> CategoryCounts = new Dictionary<string, int>();
        internal bool Dirty;
    }

    internal static class FeastRegistry
    {
        // The aggregate is the file BarrkBOT reads over FTP. The per-player files beside it are this
        // mod's persistence store and are NOT read by the bot - the aggregate carries the same
        // fields, and one FTP read beats thirty-five. Same split, and the same reasoning, as
        // TheRavensCall's BarrkBOT_data1.json / players\*.json.
        private const string AggregateFileName = "BarrkBOT_feasts.json";

        // Encoding.UTF8 emits a byte-order mark, and a BOM is not valid JSON (RFC 8259). .NET's own
        // File.ReadAllText silently tolerates one, which is exactly why it survives being written -
        // but Python's json.load, jq and most other parsers reject the file outright unless the
        // reader has specifically been taught to strip it. Every consumer of this file is somebody
        // else's program, so it is written clean rather than relying on each of them to cope.
        // Reads keep using Encoding.UTF8, which strips a BOM if an older file still has one.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Dictionary<string, FeastRecord> _records =
            new Dictionary<string, FeastRecord>(StringComparer.OrdinalIgnoreCase);

        // Set when LoadAll could not enumerate the store at all. Every write is refused afterwards,
        // because writing an aggregate built from a registry we know is incomplete would overwrite
        // the good file on disk with a shorter one - turning a readable error into silent data loss.
        // A single unreadable PLAYER file is not this: that loses one player and is logged.
        private static bool _loadFailed;
        private static bool _loaded;

        internal static string RootDir => Path.Combine(Paths.ConfigPath, "Fatty");
        internal static string PlayersDir => Path.Combine(RootDir, "feasts");
        internal static string AggregatePath => Path.Combine(RootDir, AggregateFileName);

        internal static int Count => _records.Count;

        // ------------------------------------------------------------------
        // Load / persist
        // ------------------------------------------------------------------

        // Called once when the server comes up, so the export is complete from the first tick rather
        // than only listing whoever happens to reconnect first.
        internal static void LoadAll()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                Directory.CreateDirectory(PlayersDir);

                foreach (string file in Directory.GetFiles(PlayersDir, "*.json"))
                {
                    try
                    {
                        var dto = JsonConvert.DeserializeObject<PlayerDto>(File.ReadAllText(file, Encoding.UTF8));
                        if (dto == null) continue;

                        // The name comes from INSIDE the file, never from the filename. The filename is
                        // sanitized for the filesystem and is therefore lossy; the stored name is not.
                        string name = dto.Name;
                        if (!IsUsableName(name)) continue;

                        _records[name] = new FeastRecord
                        {
                            Name = name,
                            PlayerId = dto.PlayerId,
                            LastUpdated = dto.LastUpdated ?? "",
                            FoodCounts = dto.FoodCounts ?? new Dictionary<string, int>(),
                            CategoryCounts = dto.CategoryCounts ?? new Dictionary<string, int>(),
                        };
                    }
                    catch (Exception ex)
                    {
                        FattyPlugin.Instance.Log.LogWarning(
                            $"Fatty: feast record '{Path.GetFileName(file)}' could not be read ({ex.Message}); that player starts empty.");
                    }
                }

                FattyPlugin.Instance.Log.LogInfo($"Fatty: feast export loaded {_records.Count} player record(s).");
            }
            catch (Exception ex)
            {
                _loadFailed = true;
                FattyPlugin.Instance.Log.LogError(
                    $"Fatty Mod: Feature Lost - Feast Export. The record store could not be read, so nothing will be written (rather than overwriting it with an incomplete file). Reason: {ex}");
            }
        }

        // Takes the incoming counts as the whole truth rather than adding to a running total. The
        // client sends absolute lifetime snapshots, so assignment self-backfills a character's entire
        // history on the first sync and self-heals from any dropped message on the next one - while
        // accumulation would double-count every replayed or duplicated message forever.
        internal static void Merge(string name, long playerId, Dictionary<string, int> foodCounts, Dictionary<string, int> categoryCounts)
        {
            if (!IsUsableName(name) || foodCounts == null) return;

            if (!_records.TryGetValue(name, out FeastRecord rec))
            {
                rec = new FeastRecord { Name = name };
                _records[name] = rec;
            }

            rec.Name = name;
            if (playerId != 0L) rec.PlayerId = playerId;
            rec.FoodCounts = foodCounts;
            if (categoryCounts != null) rec.CategoryCounts = categoryCounts;
            rec.LastUpdated = DateTime.UtcNow.ToString("o");
            rec.Dirty = true;
        }

        // Writes changed player files, then the aggregate. One call per export tick.
        internal static void Flush()
        {
            if (_loadFailed) return;

            try { Directory.CreateDirectory(PlayersDir); }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: feast export directory could not be created ({ex.Message}).");
                return;
            }

            foreach (var rec in _records.Values)
            {
                if (!rec.Dirty) continue;
                if (WriteAtomic(PlayerFilePath(rec.Name), JsonConvert.SerializeObject(ToDto(rec), Formatting.Indented)))
                    rec.Dirty = false;
            }

            WriteAtomic(AggregatePath, JsonConvert.SerializeObject(BuildAggregate(), Formatting.Indented));
        }

        // Dropped when the world session ends so a second world in the same process does not inherit
        // the first one's players.
        internal static void Reset()
        {
            _records.Clear();
            _loaded = false;
            _loadFailed = false;
        }

        // ------------------------------------------------------------------
        // Serialization
        // ------------------------------------------------------------------

        private static AggregateDto BuildAggregate()
        {
            var players = new Dictionary<string, PlayerDto>();
            foreach (var rec in _records.Values) players[rec.Name] = ToDto(rec);

            return new AggregateDto
            {
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Source = $"{FattyPlugin.PluginName} {FattyPlugin.PluginVersion}",
                Players = players,
            };
        }

        // total_meals, distinct_foods and favourite_food are emitted rather than left for the reader
        // to derive. A computed field that both sides calculate is a field that will eventually
        // disagree, and the disagreement surfaces as the bot confidently stating a wrong number.
        private static PlayerDto ToDto(FeastRecord rec)
        {
            int total = 0;
            foreach (int v in rec.FoodCounts.Values) total += v;

            return new PlayerDto
            {
                Name = rec.Name,
                PlayerId = rec.PlayerId,
                LastUpdated = rec.LastUpdated,
                TotalMeals = total,
                DistinctFoods = rec.FoodCounts.Count,
                FavouriteFood = FavouriteOf(rec.FoodCounts),
                FoodCounts = rec.FoodCounts,
                CategoryCounts = rec.CategoryCounts,
            };
        }

        // Null on a tie, and null on an empty log. Silently picking the first of a tie invents a
        // fact, and the reader has no way to tell an invented one from a real one.
        private static string FavouriteOf(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0) return null;

            string best = null;
            int bestCount = 0;
            bool tied = false;

            foreach (var kvp in counts)
            {
                if (kvp.Value > bestCount) { best = kvp.Key; bestCount = kvp.Value; tied = false; }
                else if (kvp.Value == bestCount) tied = true;
            }

            return (bestCount <= 0 || tied) ? null : best;
        }

        // ------------------------------------------------------------------
        // Disk
        // ------------------------------------------------------------------

        // Write to a sibling temp file and rename over the target, because BarrkBOT pulls the
        // aggregate over FTP on its own schedule and would otherwise eventually read a half-written
        // one. The rename is the atomic step; File.Replace and File.Move both map to rename(2).
        private static bool WriteAtomic(string path, string contents)
        {
            string tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, contents, Utf8NoBom);

                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);

                return true;
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: could not write '{Path.GetFileName(path)}' ({ex.Message}).");
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* nothing useful to do */ }
                return false;
            }
        }

        // Ordinary names stay plain "<Name>.json" - readable in a directory listing, and the same
        // shape TheRavensCall's players\ store uses. A name that needed sanitizing gets a hash of the
        // ORIGINAL appended, because sanitizing is many-to-one: without it "Bj/rn" and "Bj:rn" both
        // land on "Bj_rn.json" and one player's history silently overwrites the other's.
        private static string PlayerFilePath(string name)
        {
            string safe = Sanitize(name);
            if (!string.Equals(safe, name, StringComparison.Ordinal))
                safe += "_" + ((uint)name.GetHashCode()).ToString("x8");

            return Path.Combine(PlayersDir, safe + ".json");
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";

            var sb = new StringBuilder(name.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in name) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        // "Stranger" is the name on a PlayerProfile that was constructed and never loaded - which is
        // exactly what a dedicated server holds - and "..." is Player.GetPlayerName's default when the
        // character ZDO has no name yet. Both are placeholders, and a placeholder written down is
        // worse than a missing record, because it reads as an answer.
        // See libs-Tools\PLAYER-IDENTITY-FACTS.md section 5.
        internal static bool IsUsableName(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && name != "Stranger"
                && name != "...";
        }

        // ------------------------------------------------------------------
        // Wire shape. Field names here ARE the contract with BarrkBOT - see
        // PAUSED\TheRavensCall-OLD-1.2.1\BARRKBOT_CONTRACT.md before renaming any of them.
        // ------------------------------------------------------------------
        private class AggregateDto
        {
            [JsonProperty("generated_at")] public string GeneratedAt;
            [JsonProperty("source")] public string Source;
            [JsonProperty("players")] public Dictionary<string, PlayerDto> Players;
        }

        private class PlayerDto
        {
            [JsonProperty("name")] public string Name;
            [JsonProperty("player_id")] public long PlayerId;
            [JsonProperty("last_updated")] public string LastUpdated;
            [JsonProperty("total_meals")] public int TotalMeals;
            [JsonProperty("distinct_foods")] public int DistinctFoods;

            // Explicitly null rather than omitted on a tie - the reader distinguishes "no single
            // favourite" from "this field is missing", so NullValueHandling must stay at Include.
            [JsonProperty("favourite_food")] public string FavouriteFood;

            // Raw PREFAB names ("CookedMeat", not "Cooked Meat"). BarrkBOT resolves display names
            // against its own wiki index; two different prettifiers would drift.
            [JsonProperty("food_counts")] public Dictionary<string, int> FoodCounts;

            [JsonProperty("category_counts")] public Dictionary<string, int> CategoryCounts;
        }
    }
}
