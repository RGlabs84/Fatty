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
    //
    // Chrome (border/panel/text styles) comes from GiltFrameTheme - see that file's header for where
    // it's ported from and why. The open window's frame (GiltFrameTheme.Band + corner ornaments) is
    // considerably thicker than the old flat panel this replaced, so MinWidth/MinHeight and the
    // default size are all larger than before - small enough to clip the ornamented corners at the
    // OLD minimums, and GiltFrameTheme.Body() is used for the content area specifically so text is
    // laid out inside the frame's Band+Pad inset rather than under it.
    internal static class FeastLedgerGui
    {
        private const int WindowId = 0x4A7791;
        private const float ToggleButtonWidth = 28f;
        private const float ClosedWidth = 110f;
        private const float ClosedHeight = 30f;
        private const float GripSize = 16f;
        private const float MinWidth = 340f, MaxWidth = 900f;
        private const float MinHeight = 280f, MaxHeight = 700f;

        private static bool _initialized;
        private static bool _open;
        private static Rect _rect;

        // Window position is committed to config on drag RELEASE, not per frame - see CommitPositionIfSettled.
        private static Vector2 _committedPos = new Vector2(float.NaN, float.NaN);

        private static bool _resizing;
        private static Vector2 _resizeStartMouse;
        private static Vector2 _resizeStartSize;

        private static Vector2 _scroll;
        private static bool _milestonesOpen;
        private static bool _feastLogOpen;
        private static readonly Dictionary<string, bool> _stomachExpanded = new Dictionary<string, bool>();

        private static bool _stylesReady;
        private static GUIStyle _windowStyle;
        private static GUIStyle _closedPillStyle;
        private static GUIStyle _toggleBtnStyle;
        private static GUIStyle _foldoutStyle;
        private static GUIStyle _smallLabelStyle;
        private static GUIStyle _feastLogStyle;
        private static GUIStyle _gripStyle;
        private static GUIStyle _wrappingValueStyle;
        private static GUIStyle _wrappingHeaderStyle;

        private const float IconSize = 18f;

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

            Rect drawRect = _open ? _rect : new Rect(_rect.x, _rect.y, ClosedWidth, ClosedHeight);
            Rect result = GUI.Window(WindowId, drawRect, DrawWindow, GUIContent.none, _windowStyle);

            _rect.x = result.x;
            _rect.y = result.y;

            if (_open)
            {
                _rect.width = result.width;
                _rect.height = result.height;
                HandleResize();
            }

            ClampWindowToScreen();
            CommitPositionIfSettled();
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;

            float w = Mathf.Clamp(ConfigManager.foodBreakdownPanelWidth.Value, MinWidth, MaxWidth);
            float h = Mathf.Clamp(ConfigManager.foodBreakdownPanelHeight.Value, MinHeight, MaxHeight);
            _rect = new Rect(ConfigManager.foodBreakdownPanelX.Value, ConfigManager.foodBreakdownPanelY.Value, w, h);

            // A persisted position from a larger monitor (or the default position on a smaller screen)
            // would put the window entirely off-screen, where it cannot be dragged back - and the
            // position persists, so there is no in-game way to recover it.
            ClampWindowToScreen();
            _committedPos = new Vector2(_rect.x, _rect.y);
        }

        // Keeps at least the draggable title band on screen in both axes.
        private static void ClampWindowToScreen()
        {
            float minVisible = GiltFrameTheme.Band + GiltFrameTheme.TitleHeight;
            _rect.x = Mathf.Clamp(_rect.x, minVisible - _rect.width, Screen.width - minVisible);
            _rect.y = Mathf.Clamp(_rect.y, 0f, Screen.height - minVisible);
        }

        // BepInEx's ConfigFile defaults to SaveOnConfigSet, and ConfigEntry.Value fires SettingChanged
        // on every change, so assigning every frame (the old behaviour, at the tail of HandleResize)
        // rewrote the whole .cfg to disk once per frame for the length of a drag.
        private static void CommitPositionIfSettled()
        {
            if (Input.GetMouseButton(0)) return;                       // still dragging
            if (_committedPos.x == _rect.x && _committedPos.y == _rect.y) return;

            _committedPos = new Vector2(_rect.x, _rect.y);
            ConfigManager.foodBreakdownPanelX.Value = _rect.x;
            ConfigManager.foodBreakdownPanelY.Value = _rect.y;
        }

        // ---- window body -------------------------------------------------

        private static void DrawWindow(int id)
        {
            if (!_open)
            {
                DrawClosedPill();
                return;
            }

            float w = _rect.width, h = _rect.height;
            Rect win = new Rect(0f, 0f, w, h);

            GiltFrameTheme.DrawPanelFill(win);
            GiltFrameTheme.DrawFrame(win);

            // Title, inset on the right to leave room for the toggle button - GiltFrameTheme's own
            // DrawWindow() centres the title across the FULL width, which would run it under the
            // button, so the pieces are drawn individually here instead of via that helper.
            Rect titleRect = new Rect(GiltFrameTheme.Band + 14f, GiltFrameTheme.Band + 8f,
                w - GiltFrameTheme.Band * 2f - 14f - ToggleButtonWidth - 10f, GiltFrameTheme.TitleHeight - 20f);
            GiltFrameTheme.DrawShadowed(titleRect, "THE FEAST LEDGER", GiltFrameTheme.Title);
            GiltFrameTheme.DrawRule(new Rect(GiltFrameTheme.Band + GiltFrameTheme.Pad,
                GiltFrameTheme.Band + GiltFrameTheme.TitleHeight - 12f,
                w - 2f * (GiltFrameTheme.Band + GiltFrameTheme.Pad), 1f));

            if (GUI.Button(new Rect(w - GiltFrameTheme.Band - ToggleButtonWidth - 6f, GiltFrameTheme.Band + 10f,
                ToggleButtonWidth, GiltFrameTheme.TitleHeight - 20f), "▼", _toggleBtnStyle))
            {
                _open = false;
            }

            // Drag region is the whole title band MINUS the toggle button, so the two never fight
            // over the same click - dragging moves the window, the button always closes it.
            GUI.DragWindow(new Rect(0f, 0f, w - GiltFrameTheme.Band - ToggleButtonWidth - 10f,
                GiltFrameTheme.Band + GiltFrameTheme.TitleHeight));

            Rect body = GiltFrameTheme.Body(win);
            GUILayout.BeginArea(body);
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

        private static void DrawClosedPill()
        {
            Rect r = new Rect(0f, 0f, ClosedWidth, ClosedHeight);
            GUI.Box(r, GUIContent.none, _closedPillStyle);
            if (GUI.Button(r, "▲ FEAST", _closedPillStyle))
                _open = true;
            GUI.DragWindow(r);
        }

        // Stomach breakdown: one row per distinct food, stacked items collapsed behind a "x N -
        // empties in ..." header that expands into the per-stack detail on click, instead of always
        // printing the same food name N times.
        private static void DrawStomachSection()
        {
            GUILayout.Label("Stomach", GiltFrameTheme.Header);

            if (HudPatches.GroupCount == 0)
            {
                GUILayout.Label("An empty stomach is no way to live.", GiltFrameTheme.Note);
                return;
            }

            for (int gi = 0; gi < HudPatches.GroupCount; gi++)
            {
                HudPatches.FoodGroup g = HudPatches.Groups[gi];
                if (g?.Rep == null || g.Members.Count == 0) continue;

                string name = FoodBreakdownUI.ResolveDisplayName(g.Rep);
                TryGetIcon(g.Rep, out Texture2D icon, out Rect iconUv);

                if (g.Count <= 1)
                {
                    PlayerPatches.FoodContribution only = g.Members[0];
                    GUILayout.BeginHorizontal();
                    DrawIcon(icon, iconUv);
                    GUILayout.Label(FormatMemberLine(name + "  -  ", only), GiltFrameTheme.Value);
                    GUILayout.EndHorizontal();
                    continue;
                }

                string key = PlayerPatches.ResolvePrefabName(g.Rep) ?? name;
                bool expanded = _stomachExpanded.TryGetValue(key, out bool v) && v;

                _sb.Clear();
                _sb.Append(expanded ? "▼ " : "▶ ").Append(name).Append(" x").Append(g.Count)
                   .Append("  -  empties in ").Append(FoodBreakdownUI.FormatTime(g.ActiveTime))
                   .Append(",  ").Append(FoodBreakdownUI.FormatTime(g.TotalTime)).Append(" banked");

                GUILayout.BeginHorizontal();
                DrawIcon(icon, iconUv);
                if (GUILayout.Button(_sb.ToString(), _foldoutStyle))
                    _stomachExpanded[key] = !expanded;
                GUILayout.EndHorizontal();

                if (!expanded) continue;

                for (int i = 0; i < g.Members.Count; i++)
                {
                    PlayerPatches.FoodContribution c = g.Members[i];
                    string prefix = "     " + (i + 1) + ". ";
                    GUILayout.Label(FormatMemberLine(prefix, c, c.StackIndex, c.Multiplier), _smallLabelStyle);
                }
            }
        }

        // Icons come straight off the eaten item's own Sprite (same source Hud's vanilla food slots
        // use - see HudPatches.cs `icon.sprite = g.Rep.m_item.GetIcon()`), drawn via texcoord crop
        // rather than GUIContent's whole-texture image slot because Valheim packs item icons into a
        // shared atlas - drawing the whole atlas texture into a small box would show the wrong picture.
        private static bool TryGetIcon(Player.Food food, out Texture2D tex, out Rect uv)
        {
            tex = null;
            uv = default;

            Sprite sprite = food?.m_item?.GetIcon();
            if (sprite == null || sprite.texture == null) return false;

            tex = sprite.texture;
            Rect r = sprite.rect;
            uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            return true;
        }

        private static void DrawIcon(Texture2D tex, Rect uv)
        {
            Rect r = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
            if (tex != null) GUI.DrawTextureWithTexCoords(r, tex, uv);
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
                GUILayout.Label("(no player)", GiltFrameTheme.Note);
                return;
            }

            var counts = MilestoneStore.Load(player);

            // Meat/Sweet/Eitr/Fish alternate reward types across their 3 tiers (tier 1 and 3 flat,
            // tier 2 an alternate perk) - join each earned tier's own award text rather than a single
            // summed number, since tier 2's reward isn't the same unit as tiers 1/3's.
            DrawAlternatingMilestoneLine("Meat", counts, "Meat");
            DrawAlternatingMilestoneLine("Sweet", counts, "Sweet");
            DrawAlternatingMilestoneLine("Eitr", counts, "Eitr");
            DrawAlternatingMilestoneLine("Fish", counts, "Fish");

            // Produce (vegetables + fruit) is the one category that grants the SAME reward type
            // (regen%) on every tier, so a single cumulative total reads better than joining three
            // separate fragments.
            int vegEaten = MilestoneStore.Get(counts, "Produce");
            int vegTiers = FoodMilestones.TiersEarned(vegEaten);
            _sb.Clear();
            _sb.Append("Produce: ").Append(vegEaten).Append(" eaten");
            if (vegTiers > 0)
            {
                float mult = 1f;
                for (int t = 1; t <= vegTiers; t++) mult *= FoodMilestones.RegenForTier(t);
                _sb.Append("  -  tier ").Append(vegTiers).Append(", +").Append(((mult - 1f) * 100f).ToString("0.#")).Append("% regen (health/stamina/eitr)");
            }
            GUILayout.Label(_sb.ToString(), _wrappingValueStyle);

            GUILayout.Space(4f);

            int totalEaten = 0;
            foreach (var kv in counts) totalEaten += kv.Value;

            // Unclassified food used to be invisible here while still counting toward Fatness, so
            // the axis totals never added up to the Fatness line and there was nothing on screen to
            // explain the gap - which is exactly how "berries aren't being tracked" looked from the
            // player's side. Shown only when it is non-zero, since a clean setup has none.
            int unknownEaten = MilestoneStore.Get(counts, CategoryManager.UnknownCategory);
            if (unknownEaten > 0)
            {
                _sb.Clear();
                _sb.Append("Unclassified: ").Append(unknownEaten)
                   .Append(" eaten  -  counts toward Fatness only, no category milestone. Set 'Rebuild Food Data' to re-derive, or edit Fatty_FoodData.json.");
                GUILayout.Label(_sb.ToString(), _wrappingValueStyle);
            }

            DrawAlternatingMilestoneLine("Fatness (all foods)", null, "Fatness", totalEatenOverride: totalEaten,
                tiersOverride: FoodMilestones.FatnessTiersEarned(totalEaten), bold: true);
        }

        // Meat/Sweet/Eitr/Fish/Fatness all alternate between a flat reward (tiers 1/3) and a single
        // alternate perk (tier 2) - see FoodMilestones' header comment. Builds "N eaten - tier T,
        // <tier1 award>, <tier2 award>, ..." by joining FoodMilestones.DescribeTierAward per earned
        // tier, so the display always matches whatever that tier actually grants without duplicating
        // the reward logic here.
        private static void DrawAlternatingMilestoneLine(string label, Dictionary<string, int> counts, string category,
            int totalEatenOverride = -1, int tiersOverride = -1, bool bold = false)
        {
            int eaten = totalEatenOverride >= 0 ? totalEatenOverride : MilestoneStore.Get(counts, category);
            int tiers = tiersOverride >= 0 ? tiersOverride : FoodMilestones.TiersEarned(eaten);

            _sb.Clear();
            _sb.Append(label).Append(": ").Append(eaten).Append(" eaten");
            if (tiers > 0)
            {
                _sb.Append("  -  tier ").Append(tiers).Append(", ");
                for (int t = 1; t <= tiers; t++)
                {
                    if (t > 1) _sb.Append(", ");
                    _sb.Append(FoodMilestones.DescribeTierAward(category, t));
                }
            }
            GUILayout.Label(_sb.ToString(), bold ? _wrappingHeaderStyle : _wrappingValueStyle);
        }

        private static void DrawFeastLogSection()
        {
            if (GUILayout.Button((_feastLogOpen ? "▼ " : "▶ ") + "Lifetime Feast Log", _foldoutStyle))
                _feastLogOpen = !_feastLogOpen;

            if (!_feastLogOpen) return;

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                GUILayout.Label("(no player)", GiltFrameTheme.Note);
                return;
            }

            Dictionary<string, int> counts = FoodLifetimeLog.Load(player);
            if (counts.Count == 0)
            {
                GUILayout.Label("Nothing eaten yet.", GiltFrameTheme.Note);
                return;
            }

            var entries = new List<KeyValuePair<string, int>>(counts);
            entries.Sort((a, b) => b.Value.CompareTo(a.Value));

            EnsureItemCache();
            foreach (var kv in entries)
                GUILayout.Label(ResolveLifetimeDisplayName(kv.Key) + "   x" + kv.Value, _feastLogStyle);
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
        // panel explicitly lacks (fixed WIDTH/HEIGHT consts). Inset by Band so the grip sits just
        // inside the frame's border rather than under the corner flourish.
        private static void HandleResize()
        {
            Rect grip = new Rect(_rect.xMax - GiltFrameTheme.Band - GripSize, _rect.yMax - GiltFrameTheme.Band - GripSize, GripSize, GripSize);
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
        }

        // ---- styles ----------------------------------------------------------

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            GiltFrameTheme.EnsureBuilt();

            _windowStyle = new GUIStyle();

            _closedPillStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = GiltFrameTheme.Header.fontSize,
                fontStyle = FontStyle.Bold,
                font = GiltFrameTheme.Header.font,
                alignment = TextAnchor.MiddleCenter
            };
            _closedPillStyle.normal.textColor = GiltFrameTheme.Gold;
            _closedPillStyle.normal.background = MakeTex(new Color(0.05f, 0.045f, 0.03f, 0.95f));
            _closedPillStyle.hover.textColor = GiltFrameTheme.GoldBright;
            _closedPillStyle.hover.background = _closedPillStyle.normal.background;

            _toggleBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = GiltFrameTheme.Header.fontSize,
                fontStyle = FontStyle.Bold,
                font = GiltFrameTheme.Header.font
            };
            _toggleBtnStyle.normal.textColor = GiltFrameTheme.Gold;
            _toggleBtnStyle.hover.textColor = GiltFrameTheme.GoldBright;

            // A clickable row that reads like plain text, not a button - no background in any state.
            _foldoutStyle = new GUIStyle(GiltFrameTheme.Header)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 2, 2)
            };
            _foldoutStyle.normal.background = null;
            _foldoutStyle.hover.textColor = GiltFrameTheme.GoldBright;
            _foldoutStyle.hover.background = null;
            _foldoutStyle.active.textColor = GiltFrameTheme.GoldBright;
            _foldoutStyle.active.background = null;

            // Per-stack stomach rows. Value's bright parchment, NOT Key's muted grey: the indent and
            // the "1. 2. 3." numbering already say these are sub-detail, so dimming them on top of
            // that just made the expanded breakdown - the densest, most useful text in the panel -
            // the hardest thing on it to read. Same call as the Feast Log below, which was moved off
            // Key for exactly this reason in 1.1.5; leaving these behind is what left the two
            // sections mismatched. Still a size down, which is enough of a hierarchy cue on its own.
            _smallLabelStyle = new GUIStyle(GiltFrameTheme.Value) { fontSize = Mathf.Max(6, GiltFrameTheme.Value.fontSize - 1) };

            // Feast Log rows are a top-level list in their own right, not subordinate detail, so they
            // take Value's bright parchment to match the Getting Fat lines above. Still a size down
            // from the milestone text - this list runs long.
            _feastLogStyle = new GUIStyle(GiltFrameTheme.Value) { fontSize = Mathf.Max(6, GiltFrameTheme.Value.fontSize - 1) };

            _gripStyle = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _gripStyle.normal.background = MakeTex(GiltFrameTheme.Gold);

            // Every GiltFrameTheme text style is wordWrap=false/clipping=Clip (CalcSize measures the
            // string as one unwrapped line). Milestone lines join per-tier award text and DO run past
            // the panel width at tier 3 for Meat/Sweet/Eitr/Fish - a wrapping variant is needed here
            // or they just clip instead of wrapping. Safe inside GUILayout: it re-measures each style's
            // CalcHeight for the actual layout width every frame, so wrapped rows grow correctly.
            _wrappingValueStyle = new GUIStyle(GiltFrameTheme.Value) { wordWrap = true };
            _wrappingHeaderStyle = new GUIStyle(GiltFrameTheme.Header) { wordWrap = true };
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
