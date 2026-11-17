using BepInEx;
using HarmonyLib;

namespace Fatty
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class FattyPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.fatty";
        public const string PluginName = "Fatty";
        public const string PluginVersion = "1.2.0";

        private readonly Harmony _harmony = new Harmony(PluginGUID);

        public static FattyPlugin Instance { get; private set; }
        public BepInEx.Logging.ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Note: Initialize external mod compatibility
            Compat.FoodDurationMultiplierCompat.Initialize();

            // Note: Initialize the configurations via ServerSync
            Configuration.ConfigManager.Init(Config);

            // Note: Load the JSON categories
            Configuration.CategoryManager.Init();

            // Note: user asked for a full re-derive - honour it after the file has been read, so
            // the entry list (and the .bak) survives while the classifications are thrown away.
            if (Configuration.ConfigManager.rebuildFoodData.Value)
                Configuration.CategoryManager.RequestRebuild();

            // Note: Apply Harmony patches
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded successfully! Time to get fat.");
        }

        // Drunk's screen blur eases in and out over several seconds, so it needs a per-frame tick
        // that keeps running after the status effect itself has gone.
        // Latched off on the first exception: this runs every frame, so an unlatched failure would
        // write the same stack trace to the log sixty times a second for the rest of the session.
        private bool _drunkVisionFailed;

        private void Update()
        {
            if (_drunkVisionFailed) return;
            try
            {
                Synergies.DrunkVision.Tick(UnityEngine.Time.deltaTime);
            }
            catch (System.Exception ex)
            {
                _drunkVisionFailed = true;
                Log.LogError($"Fatty Mod: Feature Lost - Drunk Motion Blur. Reason: {ex}");
                try { Synergies.DrunkVision.Shutdown(); } catch { /* already reported above */ }
            }
        }

        private void OnDestroy()
        {
            Synergies.DrunkVision.Shutdown();
        }

        // The Feast Ledger is IMGUI (see Patches\FeastLedgerGui.cs for why), which draws from a
        // MonoBehaviour's OnGUI - BaseUnityPlugin already is one, same as Njord's own Plugin.OnGUI.
        private void OnGUI()
        {
            try
            {
                Patches.FeastLedgerGui.Draw();
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Fatty Mod: Feature Lost - Feast Ledger GUI. Reason: {ex}");
            }
        }
    }
}
