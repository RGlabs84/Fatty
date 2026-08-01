using BepInEx;
using HarmonyLib;

namespace Fatty
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class FattyPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "wubarrk.fatty";
        public const string PluginName = "Fatty";
        public const string PluginVersion = "1.1.4";

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
