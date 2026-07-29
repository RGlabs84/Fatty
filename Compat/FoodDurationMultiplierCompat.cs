using System;
using System.Reflection;
using Fatty.Configuration;

namespace Fatty.Compat
{
    // Single source of truth for how long food lasts AND for how much stat each food is worth.
    //
    // Fatty now has its own duration multiplier, so blacks7ar's FoodDurationMultiplier is optional.
    // When that mod IS installed we defer to its value entirely rather than multiplying the two -
    // anyone running 2x in each would otherwise silently get 4x and report it as a bug.
    //
    // The benefit percentages exist for the same reason. FoodDurationMultiplier TRANSPILES
    // Player.GetTotalFoodValue (when its "Food Degrade" is Off, which is its shipped default) so the
    // method stops reading food.m_health entirely and reads m_shared.m_food * benefit instead.
    // Fatty is now the sole author of the per-food sum, so if we did not reproduce those
    // percentages ourselves they would silently stop working for everyone running both mods.
    public static class FoodDuration
    {
        public static float GetMultiplier()
        {
            // If their internals moved and we can no longer read the value, fall through to Fatty's
            // own config rather than silently pinning everyone to 1x.
            if (FoodDurationMultiplierCompat.IsInstalled && !FoodDurationMultiplierCompat.ResolveFailed)
            {
                float external = FoodDurationMultiplierCompat.GetDurationMultiplier();
                if (external > 0f) return external;
            }

            float own = ConfigManager.foodDurationMultiplier.Value;
            return own > 0f ? own : 1f;
        }

        // FoodDurationMultiplier's Health/Stamina/Eitr "Benefits" sliders, as plain multipliers
        // (its config is 0-200 percent). 1.0 when that mod is absent, unreadable, or has its own
        // degrade enabled - in which case it leaves GetTotalFoodValue alone and the sliders are
        // documented by the mod itself as ignored.
        public static float GetHealthBenefit() => FoodDurationMultiplierCompat.GetBenefit(FoodDurationMultiplierCompat.Benefit.Health);
        public static float GetStaminaBenefit() => FoodDurationMultiplierCompat.GetBenefit(FoodDurationMultiplierCompat.Benefit.Stamina);
        public static float GetEitrBenefit() => FoodDurationMultiplierCompat.GetBenefit(FoodDurationMultiplierCompat.Benefit.Eitr);
    }

    public static class FoodDurationMultiplierCompat
    {
        private const string PluginGuid = "blacks7ar.FoodDurationMultiplier";

        public enum Benefit { Health, Stamina, Eitr }

        private static bool _initialized;
        private static bool _isInstalled;
        private static Type _pluginType;

        // Cached across calls - this used to be resolved by reflection on every food, every tick.
        private static PropertyInfo _valueProperty;
        private static object _configEntry;
        private static bool _resolveFailed;

        // One cache entry per benefit slider plus the degrade toggle. Same fail-soft contract as
        // the duration multiplier: resolve once, and if their internals moved, stop retrying.
        private static readonly object[] _benefitEntries = new object[3];
        private static readonly PropertyInfo[] _benefitProperties = new PropertyInfo[3];
        private static readonly bool[] _benefitFailed = new bool[3];
        private static object _degradeEntry;
        private static PropertyInfo _degradeProperty;
        private static bool _degradeResolved;

        private static readonly string[] BenefitFields =
        {
            "_foodHealthBenefits", "_foodStaminaBenefits", "_foodEitrBenefits"
        };

        public static bool IsInstalled => _isInstalled;
        public static bool ResolveFailed => _resolveFailed;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(PluginGuid, out var pluginInfo))
                    return;

                // pluginInfo.Instance is null until that plugin's own Awake has run. Fatty.Awake
                // calls Initialize() BEFORE PatchAll() and without a try/catch of its own, so an
                // unguarded .GetType() here would take the entire mod down on an unlucky load
                // order. Detection simply fails soft instead, and Fatty uses its own multiplier.
                if (pluginInfo.Instance == null)
                {
                    FattyPlugin.Instance.Log.LogInfo(
                        $"Found {PluginGuid} but it has not initialized yet; using Fatty's own food duration multiplier.");
                    return;
                }

                _pluginType = pluginInfo.Instance.GetType();
                _isInstalled = true;
                FattyPlugin.Instance.Log.LogInfo($"Found {PluginGuid}, deferring food duration to it.");
            }
            catch (Exception ex)
            {
                _isInstalled = false;
                _pluginType = null;
                FattyPlugin.Instance.Log.LogWarning($"Could not initialize {PluginGuid} compatibility: {ex.Message}. Using Fatty's own multiplier.");
            }
        }

        public static float GetDurationMultiplier()
        {
            if (!_isInstalled || _resolveFailed) return 1f;

            try
            {
                if (_valueProperty == null)
                {
                    var field = _pluginType.GetField("_durationMultiplier", BindingFlags.Public | BindingFlags.Static);
                    _configEntry = field?.GetValue(null);
                    _valueProperty = _configEntry?.GetType().GetProperty("Value");

                    if (_valueProperty == null)
                    {
                        // Their internals changed - stop retrying every frame and fall back.
                        _resolveFailed = true;
                        FattyPlugin.Instance.Log.LogWarning(
                            $"Could not read {PluginGuid}._durationMultiplier; falling back to Fatty's own multiplier.");
                        return 1f;
                    }
                }

                return (float)_valueProperty.GetValue(_configEntry);
            }
            catch (Exception ex)
            {
                _resolveFailed = true;
                FattyPlugin.Instance.Log.LogWarning($"Failed to read {PluginGuid}._durationMultiplier: {ex.Message}");
                return 1f;
            }
        }

        // One of FoodDurationMultiplier's 0-200% benefit sliders, as a plain multiplier.
        //
        // Returns 1.0 (i.e. "leave the food value alone") when that mod isn't installed, when we
        // can't read it, or when its own "Food Degrade" is On - because in that case FDM skips its
        // GetTotalFoodValue transpiler entirely and its own config text says the sliders are ignored.
        public static float GetBenefit(Benefit which)
        {
            if (!_isInstalled) return 1f;

            int i = (int)which;
            if (i < 0 || i >= BenefitFields.Length || _benefitFailed[i]) return 1f;

            try
            {
                if (IsDegradeEnabled()) return 1f;

                if (_benefitProperties[i] == null)
                {
                    var field = _pluginType.GetField(BenefitFields[i], BindingFlags.Public | BindingFlags.Static);
                    _benefitEntries[i] = field?.GetValue(null);
                    _benefitProperties[i] = _benefitEntries[i]?.GetType().GetProperty("Value");

                    if (_benefitProperties[i] == null)
                    {
                        _benefitFailed[i] = true;
                        FattyPlugin.Instance.Log.LogWarning(
                            $"Could not read {PluginGuid}.{BenefitFields[i]}; treating that benefit as 100%.");
                        return 1f;
                    }
                }

                float percent = (float)_benefitProperties[i].GetValue(_benefitEntries[i]);
                if (float.IsNaN(percent) || float.IsInfinity(percent) || percent < 0f) return 1f;
                return percent / 100f;
            }
            catch (Exception ex)
            {
                _benefitFailed[i] = true;
                FattyPlugin.Instance.Log.LogWarning($"Failed to read {PluginGuid}.{BenefitFields[i]}: {ex.Message}");
                return 1f;
            }
        }

        // FoodDurationMultiplier's Toggle enum is Off = 0, On = 1, and its transpiler bails out
        // (leaving vanilla's decaying sum in place) whenever the value is non-zero.
        private static bool IsDegradeEnabled()
        {
            if (!_degradeResolved)
            {
                _degradeResolved = true;
                var field = _pluginType.GetField("_enableDegrade", BindingFlags.Public | BindingFlags.Static);
                _degradeEntry = field?.GetValue(null);
                _degradeProperty = _degradeEntry?.GetType().GetProperty("Value");
            }

            if (_degradeProperty == null) return false;

            object value = _degradeProperty.GetValue(_degradeEntry);
            return value != null && Convert.ToInt32(value) != 0;
        }
    }
}
