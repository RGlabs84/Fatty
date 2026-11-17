using UnityEngine;
using UnityEngine.PostProcessing;
using Fatty.Configuration;

namespace Fatty.Synergies
{
    // Drunk, seen from the inside: heavy motion blur with ghost trails while SE_Fatty_Drunk is on the
    // local player.
    //
    // Valheim renders through the legacy PostProcessing v1 stack (UnityEngine.PostProcessing in
    // assembly_postprocessing.dll), and already ships a motion blur model on the main camera's
    // profile - CameraEffects.SetMotionBlur just flips profile.motionBlur.enabled from the graphics
    // menu. This drives that same model rather than adding a shader of its own, so it costs nothing
    // to ship and looks like part of the game. Two knobs do the work:
    //   shutterAngle  - the per-frame smear along motion vectors (camera turns, swinging weapons).
    //   frameBlending - blends in PRECEDING frames, which is the ghosting/doubled-vision trail. This
    //                   is the "drunk" part; vanilla ships it at 0, so nobody has ever seen it.
    //
    // The profile is a shared asset, edited in place exactly the way vanilla's own CameraEffects edits
    // it. So everything touched is captured first and put back afterwards:
    // - The settings struct is captured once, when this starts overriding, and restored verbatim.
    // - `enabled` is restored from the PLAYER'S CURRENT graphics setting, not from the captured value,
    //   because they may have toggled Motion Blur in the menu while drunk; vanilla's ApplySettings
    //   will have written it and this overwrote it again on the next frame.
    // Capture happens only while NOT overriding - capturing mid-override would read back this file's
    // own drunk values as the "original", and the next sobering-up would leave them on permanently
    // (the same compounding trap CLAUDE.md describes for FoodMilestones' pristine stats).
    internal static class DrunkVision
    {
        // Seconds to reach full strength / to fully sober. Getting drunk creeps up on you; sobering up
        // takes longer than that.
        private const float FadeInSeconds = 3f;
        private const float FadeOutSeconds = 5f;

        // Strength 0..1 maps onto these. Frame blending is capped well under 1.0: at 1.0 the previous
        // frame never decays, and the screen becomes a smear that never resolves.
        private const float MinFrameBlending = 0.25f, MaxFrameBlending = 0.88f;
        private const float MinShutterAngle = 200f, MaxShutterAngle = 360f;
        private const int DrunkSampleCount = 16;

        private static float _intensity;

        private static PostProcessingProfile _profile;      // the one currently overridden, if any
        private static MotionBlurModel.Settings _savedSettings;
        private static bool _savedEnabled;
        private static bool _overriding;

        private static CameraEffects _cachedEffects;
        private static PostProcessingBehaviour _cachedBehaviour;

        // Every frame, from FattyPlugin.Update.
        internal static void Tick(float dt)
        {
            bool want = WantsBlur();

            float rate = want ? 1f / FadeInSeconds : 1f / FadeOutSeconds;
            _intensity = Mathf.MoveTowards(_intensity, want ? 1f : 0f, rate * Mathf.Max(0f, dt));

            PostProcessingProfile profile = CurrentProfile();

            // The camera's profile changed under us (world unload/reload) - put the old one back
            // before touching the new one, so neither is left carrying drunk values.
            if (_overriding && !ReferenceEquals(profile, _profile)) Restore();

            if (_intensity <= 0f)
            {
                if (_overriding) Restore();
                return;
            }

            if (profile == null) return;

            if (!_overriding)
            {
                _profile = profile;
                _savedSettings = profile.motionBlur.settings;
                _savedEnabled = profile.motionBlur.enabled;
                _overriding = true;
            }

            Apply(profile, _intensity);
        }

        // Called from FattyPlugin.OnDestroy, so a plugin reload never leaves the camera drunk.
        internal static void Shutdown()
        {
            _intensity = 0f;
            if (_overriding) Restore();
        }

        private static bool WantsBlur()
        {
            if (ConfigManager.drunkMotionBlur == null || !ConfigManager.drunkMotionBlur.Value) return false;

            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead() || DrinkBuffs.DrunkHash == 0) return false;

            SEMan seman = p.GetSEMan();
            return seman != null && seman.HaveStatusEffect(DrinkBuffs.DrunkHash);
        }

        private static PostProcessingProfile CurrentProfile()
        {
            CameraEffects fx = CameraEffects.instance;
            if (fx == null) return null;

            if (!ReferenceEquals(fx, _cachedEffects))
            {
                _cachedEffects = fx;
                _cachedBehaviour = fx.GetComponent<PostProcessingBehaviour>();
            }

            return _cachedBehaviour != null ? _cachedBehaviour.profile : null;
        }

        private static void Apply(PostProcessingProfile profile, float intensity)
        {
            float strength = Mathf.Clamp01(ConfigManager.drunkBlurStrength.Value);

            // Ease from what the player already had. With vanilla blur OFF that is zero; with it ON it
            // is vanilla's own shutter, so turning drunk on never makes the image jump.
            float baseShutter = _savedEnabled ? _savedSettings.shutterAngle : 0f;
            float baseBlend = _savedEnabled ? _savedSettings.frameBlending : 0f;

            // Smoothstep so the first and last second of the fade are gentle rather than a linear ramp
            // that visibly "starts".
            float t = intensity * intensity * (3f - 2f * intensity);

            MotionBlurModel.Settings s = _savedSettings;
            s.shutterAngle = Mathf.Lerp(baseShutter, Mathf.Lerp(MinShutterAngle, MaxShutterAngle, strength), t);
            s.frameBlending = Mathf.Lerp(baseBlend, Mathf.Lerp(MinFrameBlending, MaxFrameBlending, strength), t);
            s.sampleCount = Mathf.Max(_savedSettings.sampleCount, DrunkSampleCount);

            profile.motionBlur.settings = s;
            profile.motionBlur.enabled = true;
        }

        private static void Restore()
        {
            PostProcessingProfile profile = _profile;
            _profile = null;
            _overriding = false;

            if (profile == null) return; // Unity-null: the asset itself is gone, nothing to restore

            try
            {
                profile.motionBlur.settings = _savedSettings;

                bool enabled = _savedEnabled;
                if (GraphicsSettingsManager.Instance != null)
                    enabled = GraphicsSettingsManager.Instance.ActiveSettings.m_motionBlur;
                profile.motionBlur.enabled = enabled;
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: could not restore motion blur settings. Reason: {ex.Message}");
            }
        }
    }
}
