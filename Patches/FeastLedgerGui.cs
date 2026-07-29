using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Fatty.Configuration;
using Fatty.Progression;

namespace Fatty.Patches
{
    // The Feast Ledger - Fatty's own version of Njord's ship-telemetry HUD
    // (Njord\HUD\NjordHudManager.cs), built the same way: legacy Unity IMGUI (OnGUI/GUILayout/
    // GUI.Window/Event.current), not uGUI Canvas+RectTransform.
    //
    // The previous panel (PanelShell + the old FoodBreakdownUI toggle/panel) was uGUI, and neither
    // its on-screen toggle nor its drag handle worked reliably - both go through Unity's
    // EventSystem/GraphicRaycaster pipeline, which the Hud's own canvas was never built to support
    // (vanilla HUD has no clickable elements at all), and that held true even after
    // CanvasGroup.ignoreParentGroups was added as a defensive fix. IMGUI sidesteps that pipeline
    // completely - Event.current is read directly every OnGUI call regardless of any Canvas/
    // EventSystem/GraphicRaycaster state - which is exactly why Njord's panel "just works". This
    // ports that mechanism rather than continuing to debug the raycast.
    //
    // GUI.Window (not a hand-rolled Rect+drag, which Njord used because it had no interactive
    // content) is used here because the panel body has its own clickable foldouts; GUI.DragWindow is
    // scoped to just the title bar strip so dragging never fights with clicking a foldout below it.
    // Resizing (a gap in Njord's own panel - it only has a fixed WIDTH/HEIGHT const) is handled the
    // same way Njord handles dragging: a manual Event.current-driven Rect adjustment, just for a
    // corner grip drawn outside the window instead of the whole panel.
    internal static class FeastLedgerGui
    {
        private const int WindowId = 0x4A7791;
        private const float TitleHeight = 24f;
        private const float ToggleButtonWidth = 26f;
        private const float ClosedWidth = 96f;
        private const float GripSize = 14f;
        private const float MinWidth = 200f, MaxWidth = 800f;
        private const float MinHeight = 150f, MaxHeight = 600f;

        private static bool _initialized;
        private static bool _open;
        private static Rect _rect;

        private static bool _resizing;
        private static Vector2 _resizeStartMouse;
        private static Vector2 _resizeStartSize;

        private static Vector2 _scroll;
        private static bool _milestonesOpen;
        private static bool _feastLogOpen;
        private static readonly Dictionary<string, bool> _stomachExpanded = new Dictionary<string, bool>();

        private static bool _stylesReady;
        private static GUIStyle _windowStyle;
        private static GUIStyle _panelBoxStyle;
        private static GUIStyle _titleBoxStyle;
        private static GUIStyle _titleLabelStyle;
        private static GUIStyle _toggleBtnStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _foldoutStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _smallLabelStyle;
        private static GUIStyle _boldLabelStyle;
        private static GUIStyle _gripStyle;

        // "Transparent black" per the user's own words describing what to copy from Njord.
        private static readonly Color BgColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color TitleBgColor = new Color(0.16f, 0.11f, 0.05f, 0.96f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.35f, 1f);
        private static readonly Color GoldDim = new Color(0.85f, 0.72f, 0.35f, 1f);
        private static readonly Color GoldTrim = new Color(0.82f, 0.66f, 0.22f, 0.9f);
        private static readonly Color BodyText = new Color(0.95f, 0.9f, 0.8f, 1f);

        private static readonly StringBuilder _sb = new StringBuilder(256);
        private static Dictionary<string, GameObject> _itemPrefabCache;

        // Called from HudPatches.Hud_UpdateFood_Prefix's F8-hotkey poll and, once styles exist, from
        // the toggle button drawn in the title bar itself - the click path this whole file exists to
        // make reliable again.
        internal static void Toggle()
        {
            EnsureInit();
            _open = !_open;
        }

        // Called every frame from FattyPlugin.OnGUI().
        internal static void Draw()
        {
            if (Player.m_localPlayer == null) return;

            EnsureInit();
            EnsureStyles();

            Rect drawRect = _open ? _rect : new Rect(_rect.x, _rect.y, ClosedWidth, TitleHeight);
            Rect result = GUI.Window(WindowId, drawRect, DrawWindow, GUIContent.none, _windowStyle);

            _rect.x = result.x;
            _rect.y = result.y;

            if (_open)
            {
                _rect.width = result.width;
                _rect.height = result.height;
                HandleResize();
            }
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            float w = Mathf.Clamp(ConfigManager.foodBreakdownPanelWidth.Value, MinWidth, MaxWidth);
            float h = Mathf.Clamp(ConfigManager.foodBreakdownPanelHeight.Value, MinHeight, MaxHeight);
            _rect = new Rect(ConfigManager.foodBreakdownPanelX.Value, ConfigManager.foodBreakdownPanelY.Value, w, h);
        }

        // ---- window body -------------------------------------------------

        private static void DrawWindow(int id)
        {
            float w = _open ? _rect.width : ClosedWidth;
            float h = _open ? _rect.height : TitleHeight;

            GUI.Box(new Rect(0f, 0f, w, h), GUIContent.none, _panelBoxStyle);
            GUI.Box(new Rect(0f, 0f, w, TitleHeight), GUIContent.none, _titleBoxStyle);

            GUI.Label(new Rect(8f, 0f, w - ToggleButtonWidth - 12f, TitleHeight),
                _open ? "The Feast Ledger" : "FEAST", _titleLabelStyle);

            if (GUI.Button(new Rect(w - ToggleButtonWidth, 1f, ToggleButtonWidth - 2f, TitleHeight - 2f),
                _open ? "▼" : "▲", _toggleBtnStyle))
            {
                _open = !_open;
            }

            // Drag region is the title bar MINUS the toggle button, so the two never fight over the
            // same click - dragging moves the window, the button always toggles.
            GUI.DragWindow(new Rect(0f, 0f, w - ToggleButtonWidth, TitleHeight));

            if (!_open) return;

            GUILayout.BeginArea(new Rect(8f, TitleHeight + 4f, w - 16f, h - TitleHeight - 12f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawStomachSection();
            GUILayout.Space(8f);
            DrawMilestoneSection();
            GUILayout.Space(8f);
            DrawFeastLogSection();
            GUILayout.Space(4f);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // Stomach breakdown: one row per distinct food, stacked items collapsed behind a "x N -
        // empties in ..." header that expands into the per-stack detail on click, instead of always
        // printing the same food name N times.
        private static void DrawStomachSection()
        {
            GUILayout.Label("Stomach", _sectionStyle);

            if (HudPatches.GroupCount == 0)
            {
                GUILayout.Label("An empty stomach is no way to live.", _labelStyle);
                return;
            }

            for (int gi = 0; gi < HudPatches.GroupCount; gi++)
            {
                HudPatches.FoodGroup g = HudPatches.Groups[gi];
                if (g?.Rep == null || g.Members.Count == 0) continue;

                string name = FoodBreakdownUI.ResolveDisplayName(g.Rep);

                if (g.Count <= 1)
                {
                    PlayerPatches.FoodContribution only = g.Members[0];
                    GUILayout.Label(FormatMemberLine(name + "  -  ", only), _labelStyle);
                    continue;
                }

                string key = PlayerPatches.ResolvePrefabName(g.Rep) ?? name;
                bool expanded = _stomachExpanded.TryGetValue(key, out bool v) && v;

                _sb.Clear();
                _sb.Append(expanded ? "▼ " : "▶ ").Append(name).Append(" x").Append(g.Count)
                   .Append("  -  empties in ").Append(FoodBreakdownUI.FormatTime(g.MaxTime))
                   .Append(",  ").Append(FoodBreakdownUI.FormatTime(g.TotalTime)).Append(" banked");

                if (GUILayout.Button(_sb.ToString(), _foldoutStyle))
                    _stomachExpanded[key] = !expanded;

                if (!expanded) continue;

                for (int i = 0; i < g.Members.Count; i++)
                {
                    PlayerPatches.FoodContribution c = g.Members[i];
                    string prefix = "     " + (i + 1) + ". ";
                    GUILayout.Label(FormatMemberLine(prefix, c, c.StackIndex, c.Multiplier), _smallLabelStyle);
                }
            }
        }

        private static string FormatMemberLine(string prefix, PlayerPatches.FoodContribution c)
            => FormatMemberLine(prefix, c, -1, 0f);

        private static string FormatMemberLine(string prefix, PlayerPatches.FoodContribution c, int stackIndex, float multiplier)
        {
            _sb.Clear();
            _sb.Append(prefix).Append(FoodBreakdownUI.FormatTime(c.Food.m_time)).Append(" left");
            if (stackIndex >= 0)
                _sb.Append("  (stack ").Append(stackIndex + 1).Append(" @ ").Append(Mathf.RoundToInt(multiplier * 100f)).Append("%)");
            if (c.Hp > 0.01f) _sb.Append("  +").Append(c.Hp.ToString("0.#")).Append(" HP");
            if (c.Stamina > 0.01f) _sb.Append("  +").Append(c.Stamina.ToString("0.#")).Append(" Sta");
            if (c.Eitr > 0.01f) _sb.Append("  +").Append(c.Eitr.ToString("0.#")).Append(" Eitr");
            return _sb.ToString();
        }

        private static void DrawMilestoneSection()
        {
            if (GUILayout.Button((_milestonesOpen ? "▼ " : "▶ ") + "Getting Fat", _foldoutStyle))
                _milestonesOpen = !_milestonesOpen;

            if (!_milestonesOpen) return;

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                GUILayout.Label("(no player)", _smallLabelStyle);
                return;
            }

            var counts = MilestoneStore.Load(player);
            foreach (string category in CategoryManager.BaseCategories)
            {
                if (!MilestoneStore.EarnsMilestones(category)) continue;

                int eaten = MilestoneStore.Get(counts, category);
                int tiers = FoodMilestones.TiersEarned(eaten);

                _sb.Clear();
                _sb.Append(category).Append(": ").Append(eaten).Append(" eaten");
                if (tiers > 0)
                {
                    float hp = 0f;
                    for (int t = 1; t <= tiers; t++) hp += FoodMilestones.HpForTier(t);
                    _sb.Append("  -  tier ").Append(tiers).Append(", +").Append(hp.ToString("0.#")).Append(" HP");
                }
                GUILayout.Label(_sb.ToString(), _labelStyle);
            }

            GUILayout.Label($"Total: +{FoodMilestones.TotalBonus(player):0.#} max health  (cap +{ConfigManager.milestoneTotalCap.Value:0.#})", _boldLabelStyle);
        }

        private static void DrawFeastLogSection()
        {
            if (GUILayout.Button((_feastLogOpen ? "▼ " : "▶ ") + "Lifetime Feast Log", _foldoutStyle))
                _feastLogOpen = !_feastLogOpen;

            if (!_feastLogOpen) return;

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                GUILayout.Label("(no player)", _smallLabelStyle);
                return;
            }

            Dictionary<string, int> counts = FoodLifetimeLog.Load(player);
            if (counts.Count == 0)
            {
                GUILayout.Label("Nothing eaten yet.", _smallLabelStyle);
                return;
            }

            var entries = new List<KeyValuePair<string, int>>(counts);
            entries.Sort((a, b) => b.Value.CompareTo(a.Value));

            EnsureItemCache();
            foreach (var kv in entries)
                GUILayout.Label(ResolveLifetimeDisplayName(kv.Key) + "   x" + kv.Value, _smallLabelStyle);
        }

        private static string ResolveLifetimeDisplayName(string prefabName)
        {
            if (_itemPrefabCache != null && _itemPrefabCache.TryGetValue(prefabName, out GameObject prefab) && prefab != null)
            {
                var itemData = prefab.GetComponent<ItemDrop>()?.m_itemData;
                if (itemData?.m_shared != null)
                    return FoodBreakdownUI.Localize(itemData.m_shared.m_name);
            }
            return prefabName;
        }

        // Built lazily on first open: by then the player has spawned and ObjectDB is fully
        // populated (including modded food), same guarantee FoodScanner relies on for its own walk.
        private static void EnsureItemCache()
        {
            if (_itemPrefabCache != null && _itemPrefabCache.Count > 0) return;

            var db = ObjectDB.instance;
            if (db == null || db.m_items == null) return;

            _itemPrefabCache = new Dictionary<string, GameObject>();
            foreach (GameObject item in db.m_items)
            {
                if (item == null) continue;
                _itemPrefabCache[item.name] = item;
            }
        }

        // ---- resize --------------------------------------------------------

        // Drawn and handled OUTSIDE GUI.Window (in screen space, after the window's own Rect has
        // been read back) so the drag math is plain screen-pixel delta, same as Njord's own
        // HandleDragging - just applied to size instead of position. This is the capability Njord's
        // panel explicitly lacks (fixed WIDTH/HEIGHT consts).
        private static void HandleResize()
        {
            Rect grip = new Rect(_rect.xMax - GripSize, _rect.yMax - GripSize, GripSize, GripSize);
            GUI.Box(grip, GUIContent.none, _gripStyle);

            Event e = Event.current;
            if (e == null) return;

            if (!_resizing && e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _resizing = true;
                _resizeStartMouse = e.mousePosition;
                _resizeStartSize = new Vector2(_rect.width, _rect.height);
                e.Use();
            }
            else if (_resizing && e.type == EventType.MouseDrag && e.button == 0)
            {
                Vector2 delta = e.mousePosition - _resizeStartMouse;
                _rect.width = Mathf.Clamp(_resizeStartSize.x + delta.x, MinWidth, MaxWidth);
                _rect.height = Mathf.Clamp(_resizeStartSize.y + delta.y, MinHeight, MaxHeight);
                e.Use();
            }
            else if (_resizing && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _resizing = false;
                ConfigManager.foodBreakdownPanelWidth.Value = _rect.width;
                ConfigManager.foodBreakdownPanelHeight.Value = _rect.height;
                e.Use();
            }

            // Position is written on every frame the window moved rather than gated on mouse-up,
            // since GUI.Window's own drag doesn't hand back a discrete "drag ended" event here.
            ConfigManager.foodBreakdownPanelX.Value = _rect.x;
            ConfigManager.foodBreakdownPanelY.Value = _rect.y;
        }

        // ---- styles ----------------------------------------------------------

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _windowStyle = new GUIStyle();

            _panelBoxStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _panelBoxStyle.normal.background = MakeTex(BgColor);

            _titleBoxStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _titleBoxStyle.normal.background = MakeTex(TitleBgColor);

            _titleLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = GoldText }
            };

            _toggleBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GoldText }
            };

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = GoldDim }
            };

            // A clickable row that reads like plain text, not a button - no background in any state.
            _foldoutStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 2, 2)
            };
            _foldoutStyle.normal.textColor = GoldText;
            _foldoutStyle.normal.background = null;
            _foldoutStyle.hover.textColor = Color.white;
            _foldoutStyle.hover.background = null;
            _foldoutStyle.active.textColor = Color.white;
            _foldoutStyle.active.background = null;

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = BodyText } };
            _smallLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = BodyText } };
            _boldLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = GoldText } };

            _gripStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _gripStyle.normal.background = MakeTex(GoldTrim);
        }

        private static Texture2D MakeTex(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }
    }
}
