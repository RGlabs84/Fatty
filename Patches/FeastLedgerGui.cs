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

        // Closed-pill press tracking - see HandleClosedPillClick.
        private static bool _pillPressed;
        private static bool _pillHovering;
        private static Vector2 _pillPressPos;
        private const float ClickSlop = 4f;   // move further than this and it was a drag, not a click

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

            // Fatty lays every rect out in raw screen pixels, so it needs the identity transform - and
            // in a loaded profile it does not get one. Measured in game from a real session (2560x1440):
            // on MOUSE-EVENT passes something upstream leaves GUI.matrix carrying a 1.1 scale plus a
            // ~143px y translation, while REPAINT passes arrive untransformed. Event.current.mousePosition
            // is reported through that matrix, so on exactly the passes that decide a click - and only
            // those - every control in this file was hit-testing against a position ~10% shrunk and 143px
            // low. GUI.Button's hover, decided on repaint, kept working perfectly, which is precisely the
            // "it highlights but will not click" report; F8 was unaffected because a keyboard poll never
            // touches a coordinate at all. Restored afterwards, because the plugins drawing after this one
            // are entitled to whatever state they were handed.
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;

            // Resetting the matrix is NOT enough on its own, and the reason is worth stating because it
            // cost a round trip: Unity transforms Event.current.mousePosition when the event is DISPATCHED
            // into OnGUI, not when it is read. Clearing GUI.matrix here therefore fixes drawing while
            // leaving the pointer exactly as skewed as it arrived - which is why resetting the matrix made
            // the pill (hit-tested against the hardware cursor) work while every control INSIDE the panel,
            // which goes through GUI/GUILayout and reads Event.current.mousePosition, stayed off.
            //
            // Measured: the pointer arrives divided by 1.100 in a space 1.1x the reported Screen size
            // (2816x1584 against a real 2560x1440), consistent to four decimal places over every sample.
            // Overwriting it with the true screen position fixes every control at once - GUI.Button, the
            // foldouts, the scroll view, the close button - instead of one hit-test at a time.
            //
            // Restored in the finally with the matrix. Event.current is shared with whatever draws after
            // this plugin, and while the value written here is the truthful one, silently reshaping another
            // mod's input is not this mod's call to make.
            Event ev = Event.current;
            Vector2 prevMouse = ev != null ? ev.mousePosition : Vector2.zero;
            if (ev != null) ev.mousePosition = MouseGui();

            try
            {
            // Latched BEFORE the window runs, and every test below uses the latch rather than _open.
            //
            // DrawClosedPill's button assigns _open = true from INSIDE the GUI.Window callback, so by
            // the time control returns here _open no longer describes what was drawn - but `result`
            // still does, and on that frame `result` is the 110x30 closed pill. Testing _open therefore
            // adopted the PILL's size as the panel's size the instant it was clicked: _rect became
            // 110x30, far under MinWidth/MinHeight, and nothing re-clamps it after EnsureInit, so the
            // panel "opened" as a stub whose GiltFrameTheme.Body() works out to a negative height and
            // threw out of GUILayout.BeginArea. A separate bug from the matrix above - it needed the
            // click to land before it could bite - but it would have been the very next failure.
            bool wasOpen = _open;

            Rect drawRect = wasOpen ? _rect : new Rect(_rect.x, _rect.y, ClosedWidth, ClosedHeight);

            // BEFORE GUI.Window, so this sees the raw event whatever the window does with it afterwards.
            if (!wasOpen) HandleClosedPillClick(drawRect);

            Rect result = GUI.Window(WindowId, drawRect, DrawWindow, GUIContent.none, _windowStyle);

            _rect.x = result.x;
            _rect.y = result.y;

            if (wasOpen)
            {
                // Clamped, not adopted raw. HandleResize already clamps while dragging the grip, but
                // this is the only guard on the value that actually persists - and a _rect below
                // MinWidth/MinHeight is not a cosmetic problem, it is the unrecoverable stub above.
                _rect.width = Mathf.Clamp(result.width, MinWidth, MaxWidth);
                _rect.height = Mathf.Clamp(result.height, MinHeight, MaxHeight);
                HandleResize();
            }

            ClampWindowToScreen();
            CommitPositionIfSettled();
            }
            finally
            {
                GUI.matrix = prevMatrix;
                if (ev != null) ev.mousePosition = prevMouse;
            }
        }

        // The pointer position every hit-test in this file uses, taken from the HARDWARE cursor rather
        // than from Event.current.mousePosition.
        //
        // Input.mousePosition is raw device state - no GUI matrix, no clip stack, nothing between it and
        // the driver - so it cannot be skewed by whatever another plugin left on the GUI stack. It is
        // bottom-up where IMGUI is top-down, hence the flip. Resetting GUI.matrix in Draw() above should
        // make Event.current.mousePosition trustworthy again on its own; this is belt and braces, and it
        // is the version the in-game telemetry actually PROVED correct: across every logged click, the
        // hardware position landed inside the pill while Event.current.mousePosition did not.
        private static Vector2 MouseGui()
        {
            Vector3 hw = Input.mousePosition;
            return new Vector2(hw.x, Screen.height - hw.y);
        }

        // Opening the panel no longer depends on DrawClosedPill's GUI.Button return value.
        //
        // That button highlights on hover while never firing, which is exactly the reported symptom -
        // and hover proves much less than it looks like it does: it is applied at REPAINT time purely
        // from "does this rect contain the mouse position", so it demonstrates that the rect and the
        // pointer agree and nothing more. It never needs the click to arrive. GUI.Button, by contrast,
        // only returns true if it received the MouseDown that set GUIUtility.hotControl AND the matching
        // MouseUp; anything that consumes either half, or clears hotControl between those two passes,
        // costs the click with no other visible effect. The profile this runs in carries 17 other
        // plugins, several drawing their own IMGUI, so that is not hypothetical. F8 is unaffected because
        // it is a keyboard poll that never touches IMGUI's mouse path at all - which is precisely the
        // asymmetry that was reported.
        //
        // The press is therefore tracked from Event.current directly - the same way HandleResize below
        // has always done it, and the way BarrkUI's own draggable widgets do it (UI/HudWidgets.cs:758).
        // The event is deliberately NOT consumed: GUI.DragWindow still needs the MouseDown in order to
        // drag the pill, and the click is only claimed on MouseUp if the pointer barely moved, so
        // dragging and clicking stay distinct. DrawClosedPill's own `_open = true` is left in place and
        // is harmless - both paths assign true rather than toggling, so whichever fires first wins and a
        // second fire is a no-op.
        private static void HandleClosedPillClick(Rect pill)
        {
            Event e = Event.current;
            if (e == null) return;

            Vector2 mouse = MouseGui();
            bool inside = pill.Contains(mouse);

            if (e.type == EventType.MouseDown && e.button == 0 && inside)
            {
                _pillPressed = true;
                _pillPressPos = mouse;
            }
            else if (_pillPressed && (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp))
            {
                _pillPressed = false;
                if (inside && (mouse - _pillPressPos).sqrMagnitude <= ClickSlop * ClickSlop)
                    _open = true;
            }

            // Hover transitions are logged as well as clicks, and that is the point of this build.
            // The user reports the pill highlights on hover - and GUI.Button's hover is decided by the
            // same "does this rect contain Event.current.mousePosition" test this method runs, just in
            // window-local coordinates instead of screen ones (a 1-2px difference, the window origin).
            // So hover firing while inside is NEVER true here is a contradiction, and whichever of these
            // two lines shows up in the log resolves it:
            //   "hover ENTER" appears        -> the rect and the pointer DO agree; the click events are
            //                                  the thing going missing, and the fix is in event handling.
            //   only click lines, no hover   -> they never agree, so what highlights is not this pill and
            //                                  the clicks logged were aimed at something else entirely.
            if (inside != _pillHovering)
            {
                _pillHovering = inside;
                LogPill(inside ? "hover ENTER" : "hover EXIT", e, pill, inside);
            }

            LogPillEvent(e, pill, inside);
        }

        // Gated on the existing Debug Mode switch, and the reason it exists: if the pill still will not
        // open, this says WHY without another round of guessing from source.
        //
        //   no MouseDown line at all      -> the events never reach Fatty's OnGUI. The cause is upstream
        //                                    (another plugin, or the cursor state), not in this file.
        //   MouseDown but never MouseUp   -> something claims the press mid-drag; check hotControl.
        //   both, with inside=False       -> the rect and the pointer disagree; a coordinate-space fault.
        //   both, inside=True, still shut -> the toggle is being undone after the fact, look at Draw().
        //
        // cursorVisible is logged because Valheim only frees the pointer while the inventory or the ESC
        // menu is up, which is the one state in which this button can be clicked at all.
        private static void LogPillEvent(Event e, Rect pill, bool inside)
        {
            if (!ConfigManager.debugMode.Value) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseUp &&
                e.rawType != EventType.MouseDown && e.rawType != EventType.MouseUp) return;

            LogPill($"{e.type}/{e.rawType} btn={e.button}", e, pill, inside);
        }

        // Logs IMGUI's mouse position next to the HARDWARE one, which is what makes this conclusive
        // rather than another data point to interpret.
        //
        // Event.current.mousePosition is IMGUI space: origin top-left, y growing DOWNWARD.
        // Input.mousePosition is hardware space: origin bottom-left, y growing UPWARD. Converting one to
        // the other is exactly `y' = Screen.height - y`. So if `imgui` and `flipped` agree, IMGUI's idea
        // of where the pointer is matches the real cursor and any offset is in the rect, not the input.
        // If they DISAGREE, the pointer IMGUI is reasoning about is not the one on screen, and no amount
        // of adjusting this file's rects will fix it - the cause is the display/window/scale setup.
        // Screen dimensions are logged too, since a mismatch against the real desktop resolution is its
        // own answer.
        private static void LogPill(string what, Event e, Rect pill, bool inside)
        {
            if (!ConfigManager.debugMode.Value) return;

            Vector3 hw = Input.mousePosition;
            Vector2 flipped = new Vector2(hw.x, Screen.height - hw.y);

            FattyPlugin.Instance.Log.LogInfo(
                $"Fatty pill [{what}]: imgui={e.mousePosition} input={(Vector2)hw} flipped={flipped} " +
                $"screen={Screen.width}x{Screen.height} pill={pill} inside={inside} " +
                $"pressed={_pillPressed} cursorVisible={Cursor.visible} lockState={Cursor.lockState} " +
                $"hotControl={GUIUtility.hotControl} open={_open}");
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

            // _committedPos is deliberately LEFT at its NaN sentinel here. Seeding it from the
            // post-clamp rect made the recovery above equal to the committed value by construction,
            // so CommitPositionIfSettled could never write it back and the off-screen coordinate
            // survived in the .cfg forever - every launch re-derived the same rescue from the same
            // bad stored value. NaN compares equal to nothing, which is the whole point of the
            // sentinel: the first settled frame persists whatever the clamp recovered. It costs no
            // spurious write, because BepInEx's ConfigEntry.Value setter early-returns when the
            // value is unchanged, so a position that needed no rescuing never touches the disk.
        }

        // Keeps a grabbable part of the window on screen in both axes.
        //
        // This has to measure the rect actually being DRAWN, not _rect. While the panel is closed
        // Draw() renders a ClosedWidth x ClosedHeight pill, but _rect still carries the OPEN size -
        // so clamping x against _rect.width left every x in [72 - 622, -110] legal while putting the
        // whole 110px pill off the left edge. That is exactly the state a player needs rescuing from,
        // because the panel is closed whenever it goes missing: the recovery this method exists for
        // was a no-op precisely when it mattered, and the bad x persisted, so it came back every
        // launch. At MaxWidth the dead band was wider still.
        //
        // The "keep this much visible" figure is capped to the drawn size for the same reason:
        // demanding 72px of a 30px-tall pill is not a constraint the pill can satisfy, and asking
        // for more than the window has just pins it to an edge.
        private static void ClampWindowToScreen()
        {
            // Alt-tab and minimise can report a 0-sized screen for a frame. Clamping to that would
            // drag the panel to a junk coordinate and - now that the commit below is no longer gated
            // on the window being open - persist it.
            if (Screen.width < 1 || Screen.height < 1) return;

            float w = _open ? _rect.width : ClosedWidth;
            float h = _open ? _rect.height : ClosedHeight;

            float band = GiltFrameTheme.Band + GiltFrameTheme.TitleHeight;
            float keepX = Mathf.Min(band, w);
            float keepY = Mathf.Min(band, h);

            // Mathf.Clamp is `if (v < min) v = min; else if (v > max) v = max`, so handing it min > max
            // makes it alternate between the two on successive calls instead of settling. Draw() calls
            // this on every OnGUI pass, so that would flip _rect several times a frame and defeat
            // CommitPositionIfSettled's exact-equality gate - a per-frame .cfg write, the very thing
            // 1.1.8 set out to remove. Ordering the bounds keeps repeated application idempotent.
            float minX = keepX - w;
            _rect.x = Mathf.Clamp(_rect.x, minX, Mathf.Max(minX, Screen.width - keepX));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - keepY));
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

            // Same hardware-derived pointer as the pill, and for the same reason - a grip that cannot be
            // grabbed is the identical failure one corner down.
            Vector2 mouse = MouseGui();

            if (!_resizing && e.type == EventType.MouseDown && e.button == 0 && grip.Contains(mouse))
            {
                _resizing = true;
                _resizeStartMouse = mouse;
                _resizeStartSize = new Vector2(_rect.width, _rect.height);
                e.Use();
            }
            else if (_resizing && e.type == EventType.MouseDrag && e.button == 0)
            {
                Vector2 delta = mouse - _resizeStartMouse;
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

            // Claimed at the END, not up front. Set first, anything throwing partway through left
            // every later frame running on half-null GUIStyles with no way to retry - and the very
            // next call hands a null style to GUI.Window, which takes the whole panel down for the
            // session. Retrying is the cheaper failure: Plugin.OnGUI already logs whatever threw.
            _stylesReady = true;
        }

        // HideAndDontSave for the same reason GiltFrameTheme flags its own textures (see that file's
        // header): without it Unity collects these on the next scene load, and since _stylesReady
        // latches true they are never rebuilt - so after one world reload the closed pill's
        // background and the resize grip were being drawn from destroyed textures for the rest of
        // the session. The pill is the only way back into a closed panel, so it is the last thing
        // that should quietly stop rendering.
        private static Texture2D MakeTex(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }
    }
}
