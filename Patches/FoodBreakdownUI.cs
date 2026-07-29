using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fatty.Patches
{
    // Per-icon segmented stack bar - purely decorative uGUI (an Image with a null sprite draws a
    // solid colour quad and needs no font or raycaster, so it was never at risk from the click/drag
    // problems below). The clickable, draggable Feast Ledger panel that used to live in this file
    // moved to FeastLedgerGui.cs as an IMGUI window - see that file's header comment for why: both
    // the on-screen toggle's click and the panel's drag depend on Unity's EventSystem/
    // GraphicRaycaster pipeline, which the Hud's own canvas was never built to support (vanilla HUD
    // has no clickable elements), and that held true even after CanvasGroup.ignoreParentGroups was
    // added defensively. IMGUI reads Event.current directly every frame, bypassing that pipeline
    // entirely - the same mechanism Njord's HUD (Njord\HUD\NjordHudManager.cs) already relies on.
    internal static class FoodBreakdownUI
    {
        private const int MaxSegments = 8;

        private static Image[][] _segments;   // [slot][segment]

        private static readonly Color DimSegment = new Color(0.35f, 0.30f, 0.18f, 0.9f);
        private static readonly Color BrightSegment = new Color(1f, 0.86f, 0.35f, 1f);

        internal static void BuildSlotExtras(Hud hud, Image[] icons, TMP_Text textTemplate)
        {
            try
            {
                if (hud == null || icons == null || icons.Length == 0) return;

                int slots = icons.Length;
                _segments = new Image[slots][];
                for (int i = 0; i < slots; i++)
                {
                    if (icons[i] == null) continue;
                    _segments[i] = BuildSegmentBar(icons[i].transform);
                }
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Segmented Stack Bars. Reason: {ex}");
            }
        }

        internal static void SyncFrame(int slotCount)
        {
            try
            {
                for (int i = 0; i < slotCount; i++)
                    SyncSegmentBar(i, i < HudPatches.GroupCount ? HudPatches.Groups[i] : null);
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Segmented Stack Bar Sync. Reason: {ex}");
                _segments = null;
            }
        }

        private static void SyncSegmentBar(int slot, HudPatches.FoodGroup g)
        {
            Image[] segs = (_segments != null && slot < _segments.Length) ? _segments[slot] : null;
            if (segs == null) return;

            // Segmented bar and the icon's "xN" badge show the same information - either/or by
            // config, never both (see HudPatches.Hud_UpdateFood_Prefix for the badge side).
            bool show = Configuration.ConfigManager.useSegmentedStackBar.Value;
            int memberCount = show ? (g?.Members?.Count ?? 0) : 0;
            for (int s = 0; s < segs.Length; s++)
            {
                Image seg = segs[s];
                if (seg == null) continue;

                if (s >= memberCount)
                {
                    SetActive(seg.transform, false);
                    continue;
                }

                SetActive(seg.transform, true);
                PlayerPatches.FoodContribution c = g.Members[s];
                float fraction = c.BurnTime > 0f ? Mathf.Clamp01(c.Food.m_time / c.BurnTime) : 0f;
                seg.color = Color.Lerp(DimSegment, BrightSegment, fraction);
            }
        }

        internal static string ResolveDisplayName(Player.Food food)
        {
            string token = food?.m_item?.m_shared?.m_name;
            if (string.IsNullOrEmpty(token)) return PlayerPatches.ResolvePrefabName(food);
            return Localize(token);
        }

        internal static string Localize(string token)
        {
            try { return Localization.instance.Localize(token); }
            catch { return token; }
        }

        internal static string FormatTime(float t)
        {
            if (t <= 0f) return "0s";
            return t >= 60f ? Mathf.CeilToInt(t / 60f) + "m" : Mathf.FloorToInt(t) + "s";
        }

        private static Image[] BuildSegmentBar(Transform iconRoot)
        {
            GameObject container = new GameObject("Fatty_SegmentBar", typeof(RectTransform));
            container.transform.SetParent(iconRoot, false);
            RectTransform crt = (RectTransform)container.transform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(0f, 4f);
            crt.anchoredPosition = new Vector2(0f, 1f);

            var segs = new Image[MaxSegments];
            for (int i = 0; i < MaxSegments; i++)
            {
                GameObject segGo = new GameObject("Seg" + i, typeof(RectTransform));
                segGo.transform.SetParent(container.transform, false);
                RectTransform srt = (RectTransform)segGo.transform;

                float gap = 0.015f;
                srt.anchorMin = new Vector2((float)i / MaxSegments + gap, 0f);
                srt.anchorMax = new Vector2((float)(i + 1) / MaxSegments - gap, 1f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;

                Image img = segGo.AddComponent<Image>();
                img.color = DimSegment;
                img.raycastTarget = false;
                segGo.SetActive(false);

                segs[i] = img;
            }

            container.transform.SetAsLastSibling();
            return segs;
        }

        private static void SetActive(Transform t, bool active)
        {
            if (t != null && t.gameObject.activeSelf != active)
                t.gameObject.SetActive(active);
        }
    }
}
