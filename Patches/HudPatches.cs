using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Fatty.Configuration;

namespace Fatty.Patches
{
    [HarmonyPatch]
    public static class HudPatches
    {
        private const int TargetSlots = 5; // 4 food + 1 drink

        internal static readonly Color GoldTrim = new Color(0.82f, 0.66f, 0.22f, 1f);
        internal static readonly Color GoldText = new Color(1f, 0.86f, 0.35f, 1f);

        // When true, our prefix takes over food drawing (STEADY colours + stack grouping). This is
        // set whenever the vanilla food arrays are usable - even if the 5-slot expansion or styling
        // only partly succeeded - so the food icons never do vanilla's Sin-wave "can eat again" pulse
        // (Hud.UpdateFood sets icon/time alpha from Mathf.Sin(Time.time...), which reads as flashing).
        private static bool _customDrawReady;
        private static TMP_Text[] _countLabels;   // per-slot stack badge (entries may be null)

        // One food slot's group of same-prefab stack entries.
        //
        // MaxTime is what the bar shows, and it is the only honest number there: vanilla's
        // Player.UpdateFood decays EVERY entry in m_foods simultaneously (`food.m_time -= 1f` across
        // the whole list), so a stack does not drain one item at a time - the slot survives until its
        // longest-remaining entry expires. TotalTime (the sum) is kept for the breakdown panel, where
        // it is labelled as "banked" rather than presented as a countdown, because summing parallel
        // timers describes no real moment.
        internal class FoodGroup
        {
            public Player.Food Rep;
            public int Count;
            public float MaxTime;
            public float TotalTime;
            public readonly List<PlayerPatches.FoodContribution> Members = new List<PlayerPatches.FoodContribution>();
        }

        // Pooled across frames - both the FoodGroup instances and their Members lists are reused
        // rather than reallocated every Hud.UpdateFood tick.
        internal static readonly List<FoodGroup> Groups = new List<FoodGroup>();
        internal static int GroupCount;
        private static readonly Dictionary<string, int> _groupIndex = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _stackSeen = new Dictionary<string, int>();

        // ------------------------------------------------------------------
        // Awake: best-effort expand to 5 slots + style them, but ALWAYS enable our steady custom
        // draw as long as the food arrays are usable. Every early-out logs a reason (never silent).
        // ------------------------------------------------------------------
        [HarmonyPatch(typeof(Hud), "Awake")]
        [HarmonyPostfix]
        public static void Hud_Awake_Postfix(Hud __instance,
            ref Image[] ___m_foodIcons, ref Image[] ___m_foodBars, ref TMP_Text[] ___m_foodTime,
            RectTransform ___m_foodBarRoot)
        {
            _customDrawReady = false;
            _countLabels = null;

            try
            {
                if (___m_foodIcons == null || ___m_foodBars == null || ___m_foodTime == null)
                {
                    Bail("food icon/bar/time arrays are null");
                    return;
                }

                int existing = ___m_foodIcons.Length;
                if (existing < 1 || ___m_foodBars.Length != existing || ___m_foodTime.Length != existing)
                {
                    Bail($"food arrays empty or mismatched (icons={___m_foodIcons.Length}, bars={___m_foodBars.Length}, time={___m_foodTime.Length})");
                    return;
                }

                // Best-effort expansion. On failure we still take over drawing the existing slots
                // (steady, no pulse) - we just won't have the two extra slots.
                if (existing < TargetSlots)
                    TryExpand(ref ___m_foodIcons, ref ___m_foodBars, ref ___m_foodTime, existing);

                // Best-effort styling + stack badges on whatever slots we now have.
                int slots = ___m_foodIcons.Length;
                var labels = new TMP_Text[slots];
                TMP_Text labelTemplate = FirstNonNull(___m_foodTime);
                for (int i = 0; i < slots; i++)
                {
                    if (___m_foodIcons[i] == null) continue;
                    StyleIcon(___m_foodIcons[i]);
                    labels[i] = CreateCountLabel(___m_foodIcons[i].transform, labelTemplate);
                }

                _countLabels = labels;
                _customDrawReady = true;
                FattyPlugin.Instance.Log.LogInfo($"Fatty: food HUD ready with {slots} slot(s); custom steady draw active.");

                // Isolated on purpose: a failure building the breakdown/feast-log extras must never
                // take down the passive food HUD above, which is already fully working at this point.
                try
                {
                    FoodBreakdownUI.BuildSlotExtras(__instance, ___m_foodIcons, labelTemplate);
                }
                catch (System.Exception ex)
                {
                    FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food Breakdown/Feast Log UI. Reason: {ex}");
                }
            }
            catch (System.Exception ex)
            {
                _customDrawReady = false;
                _countLabels = null;
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food HUD Setup. Reason: {ex}");
            }
        }

        // Custom draw: group stacks of the same food into one styled slot with a "xN" badge, and
        // draw everything with STEADY colours (this is what removes the vanilla flashing/pulsing).
        [HarmonyPatch(typeof(Hud), "UpdateFood")]
        [HarmonyPrefix]
        public static bool Hud_UpdateFood_Prefix(Hud __instance, Player player,
            Image[] ___m_foodIcons, Image[] ___m_foodBars, TMP_Text[] ___m_foodTime,
            RectTransform ___m_foodBarRoot, RectTransform ___m_foodBaseBar)
        {
            if (!_customDrawReady || player == null) return true;
            if (___m_foodIcons == null || ___m_foodBars == null || ___m_foodTime == null) return true;

            try
            {
                var foods = player.GetFoods();

                GroupCount = 0;
                _groupIndex.Clear();
                _stackSeen.Clear();

                bool noDecay = ConfigManager.noFoodDecay.Value;
                float durationMultiplier = Compat.FoodDuration.GetMultiplier();
                float hpBenefit = Compat.FoodDuration.GetHealthBenefit();
                float staminaBenefit = Compat.FoodDuration.GetStaminaBenefit();
                float eitrBenefit = Compat.FoodDuration.GetEitrBenefit();

                if (foods != null)
                {
                    foreach (Player.Food f in foods)
                    {
                        if (f == null || f.m_item == null || f.m_item.m_shared == null) continue;
                        string key = PlayerPatches.ResolvePrefabName(f);
                        if (string.IsNullOrEmpty(key)) key = "?";

                        PlayerPatches.FoodContribution c = PlayerPatches.ComputeContribution(
                            f, _stackSeen, noDecay, durationMultiplier, hpBenefit, staminaBenefit, eitrBenefit);

                        FoodGroup g;
                        if (_groupIndex.TryGetValue(key, out int gi))
                        {
                            g = Groups[gi];
                            g.Count++;
                            g.TotalTime += f.m_time;
                            if (f.m_time > g.MaxTime) g.MaxTime = f.m_time;
                        }
                        else
                        {
                            gi = GroupCount++;
                            if (gi < Groups.Count)
                            {
                                g = Groups[gi];
                                g.Members.Clear();
                            }
                            else
                            {
                                g = new FoodGroup();
                                Groups.Add(g);
                            }
                            g.Rep = f;
                            g.Count = 1;
                            g.TotalTime = f.m_time;
                            g.MaxTime = f.m_time;
                            _groupIndex[key] = gi;
                        }
                        g.Members.Add(c);
                    }
                }

                if (___m_foodBaseBar != null)
                    ___m_foodBaseBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, player.GetBaseFoodHP() / 25f * 32f);

                int slots = ___m_foodIcons.Length;
                for (int i = 0; i < slots; i++)
                {
                    Image icon = i < ___m_foodIcons.Length ? ___m_foodIcons[i] : null;
                    Image bar = i < ___m_foodBars.Length ? ___m_foodBars[i] : null;
                    TMP_Text time = i < ___m_foodTime.Length ? ___m_foodTime[i] : null;
                    TMP_Text count = (_countLabels != null && i < _countLabels.Length) ? _countLabels[i] : null;

                    bool filled = i < GroupCount;

                    // Toggle icon/bar/time per slot (works for both grouped and parallel layouts).
                    if (icon != null) SetActiveSafe(icon.transform, filled);
                    if (bar != null) SetActiveSafe(bar.transform, filled);
                    if (time != null) SetActiveSafe(time.transform, filled);

                    if (filled)
                    {
                        FoodGroup g = Groups[i];

                        if (icon != null && g.Rep != null && g.Rep.m_item != null)
                        {
                            icon.sprite = g.Rep.m_item.GetIcon();
                            icon.color = Color.white; // STEADY - no Sin-wave pulse
                        }

                        if (time != null)
                        {
                            // When this slot actually empties - see FoodGroup's comment on why this
                            // is MaxTime and not the sum of the stack's timers.
                            float t = g.MaxTime;
                            time.text = t >= 60f ? Mathf.CeilToInt(t / 60f) + "m" : Mathf.FloorToInt(t) + "s";
                            time.color = Color.white; // STEADY
                        }

                        if (count != null)
                        {
                            // Segmented bar and "xN" badge show the same information - stacking is
                            // an either/or display choice, not both at once.
                            if (g.Count > 1 && !ConfigManager.useSegmentedStackBar.Value)
                            {
                                count.text = "x" + g.Count;
                                SetActiveSafe(count.transform, true);
                            }
                            else
                            {
                                SetActiveSafe(count.transform, false);
                            }
                        }
                    }
                    else if (count != null)
                    {
                        SetActiveSafe(count.transform, false);
                    }
                }

                if (___m_foodBarRoot != null)
                    ___m_foodBarRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(player.GetMaxHealth() / 25f * 32f));

                if (ConfigManager.breakdownToggleKey.Value.IsDown() && CanAcceptHotkey())
                    FeastLedgerGui.Toggle();

                FoodBreakdownUI.SyncFrame(slots);

                return false; // skip vanilla drawing (and its pulse)
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Food HUD Draw. Reason: {ex}");
                return true; // fall back to vanilla
            }
        }

        // Vanilla's own UpdateStatusEffects (decompiled) only ever touches "Icon", "Cooldown"
        // (SetActive by m_cooldownIcon), "Name" and "TimeText" on each cloned status-effect row - it
        // NEVER touches "TimeBar" (which itself contains "bkg"/"bar" children). TimeBar keeps whatever
        // colour/visibility it was authored with in the prefab, and our synergy icons are circular with
        // dark corners (see SynergyIcons.FlattenAlpha) - smaller than TimeBar's own panel - so its
        // light default colour showed through around the badge as a white square. Hide TimeBar (and
        // Cooldown, defensively) for our 4 synergy effects specifically, identified by name hash, so
        // every vanilla and other-mod status icon is completely untouched.
        [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
        [HarmonyPostfix]
        public static void Hud_UpdateStatusEffects_Postfix(List<StatusEffect> statusEffects, List<RectTransform> ___m_statusEffects)
        {
            try
            {
                if (statusEffects == null || ___m_statusEffects == null) return;

                int count = Mathf.Min(statusEffects.Count, ___m_statusEffects.Count);
                for (int i = 0; i < count; i++)
                {
                    StatusEffect se = statusEffects[i];
                    if (se == null) continue;

                    int hash = se.NameHash();
                    bool isOurs = hash == Fatty.Synergies.SynergyEffects.BalancedDietHash
                        || hash == Fatty.Synergies.SynergyEffects.SugarRushHash
                        || hash == Fatty.Synergies.SynergyEffects.FishermanHash
                        || hash == Fatty.Synergies.SynergyEffects.LumberjackHash;

                    if (!isOurs)
                    {
                        foreach (int drinkHash in Fatty.Synergies.DrinkBuffs.AllHashes)
                        {
                            if (hash != drinkHash) continue;
                            isOurs = true;
                            break;
                        }
                    }

                    if (!isOurs) continue;

                    RectTransform row = ___m_statusEffects[i];
                    if (row == null) continue;

                    Transform timeBar = row.Find("TimeBar");
                    if (timeBar != null) timeBar.gameObject.SetActive(false);

                    Transform cooldown = row.Find("Cooldown");
                    if (cooldown != null) cooldown.gameObject.SetActive(false);
                }
            }
            catch (System.Exception ex)
            {
                FattyPlugin.Instance.Log.LogError($"Fatty Mod: Feature Lost - Synergy Status Icon Cleanup. Reason: {ex}");
            }
        }

        // ---- expansion -------------------------------------------------------

        private static void TryExpand(ref Image[] icons, ref Image[] bars, ref TMP_Text[] times, int existing)
        {
            if (existing < 2)
            {
                Bail("need >=2 existing slots to derive the layout spacing; keeping current count");
                return;
            }
            if (icons[0] == null || icons[1] == null || bars[0] == null || bars[1] == null || times[0] == null || times[1] == null)
            {
                Bail("slot 0/1 has a null component; keeping current count");
                return;
            }

            var newIcons = Grow(icons, TargetSlots);
            var newBars = Grow(bars, TargetSlots);
            var newTimes = Grow(times, TargetSlots);
            var created = new List<GameObject>();

            // IMPORTANT: in Valheim the three arrays live in DIFFERENT parts of the hierarchy - the
            // food bars are sibling Images under m_foodBarRoot ("Food"), while each food icon (and its
            // time text) sits in its own per-slot container elsewhere. So expand the bars and the
            // icon/time slots INDEPENDENTLY; a single "shared container" assumption is what failed
            // before (it looked for a bar inside the icon container and never found one).
            bool barsOk = ExpandArray(bars, newBars, existing, created);

            // Icons grouped (each under its own container) vs parallel (all under one parent).
            bool iconsGrouped = icons[0].transform.parent != icons[1].transform.parent;
            bool iconsOk = iconsGrouped
                ? ExpandIconGroups(icons, times, newIcons, newTimes, existing, created)
                : (ExpandArray(icons, newIcons, existing, created) && ExpandArray(times, newTimes, existing, created));

            if (!barsOk || !iconsOk)
            {
                DestroyClones(created);
                Bail($"could not build extra slots (bars ok: {barsOk}, icons ok: {iconsOk}); keeping {existing} slot(s)");
                return;
            }

            icons = newIcons;
            bars = newBars;
            times = newTimes;
            FattyPlugin.Instance.Log.LogInfo($"Fatty: expanded food HUD {existing} -> {TargetSlots} slots (icons {(iconsGrouped ? "grouped" : "parallel")}).");
        }

        // Clone a parallel array whose elements share a parent: each new element is element[0] cloned
        // under that parent, offset by the per-slot delta (element[1] - element[0]) * slot.
        private static bool ExpandArray<T>(T[] src, T[] dst, int existing, List<GameObject> created) where T : Component
        {
            for (int i = existing; i < TargetSlots; i++)
            {
                T c = CloneComponent(src[0], src[1], i, created);
                if (c == null) return false;
                dst[i] = c;
            }
            return true;
        }

        private static T CloneComponent<T>(T template, T next, int slot, List<GameObject> created) where T : Component
        {
            if (template == null) return null;
            GameObject clone = Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = template.gameObject.name + "_Fatty" + slot;
            created.Add(clone);

            T comp = clone.GetComponent<T>();
            if (comp == null) return null;

            if (template.transform is RectTransform trt && next.transform is RectTransform nrt && clone.transform is RectTransform crt)
            {
                Vector2 delta = nrt.anchoredPosition - trt.anchoredPosition;
                crt.anchoredPosition = trt.anchoredPosition + delta * slot;
            }
            return comp;
        }

        // Icons/time are grouped: each icon (and, usually, its time text) lives in its own per-slot
        // container. Clone the whole container so icon+time keep their relative layout, offset by the
        // container spacing, then re-resolve the icon (and time, if it's inside) by name. If the time
        // text lives in a separate array instead, expand that array in parallel.
        private static bool ExpandIconGroups(Image[] icons, TMP_Text[] times,
            Image[] newIcons, TMP_Text[] newTimes, int existing, List<GameObject> created)
        {
            Transform c0 = icons[0].transform.parent;
            Transform c1 = icons[1].transform.parent;
            if (c0 == null || c1 == null) return false;

            string iconName = icons[0].name;
            string timeName = times[0].name;
            bool timeInContainer = times[0].transform.IsChildOf(c0);

            Vector2 p0 = (c0 is RectTransform r0) ? r0.anchoredPosition : Vector2.zero;
            Vector2 delta = (c1 is RectTransform r1) ? r1.anchoredPosition - p0 : Vector2.zero;
            if (delta == Vector2.zero) return false; // can't derive slot spacing

            for (int i = existing; i < TargetSlots; i++)
            {
                GameObject clone = Object.Instantiate(c0.gameObject, c0.parent);
                clone.name = "Fatty_FoodSlot" + i;
                created.Add(clone);

                if (clone.transform is RectTransform crt)
                    crt.anchoredPosition = p0 + delta * i;

                Image ni = FindComp<Image>(clone.transform, iconName);
                if (ni == null) return false;
                newIcons[i] = ni;

                if (timeInContainer)
                {
                    TMP_Text nt = FindComp<TMP_Text>(clone.transform, timeName);
                    if (nt == null) return false;
                    newTimes[i] = nt;
                }
            }

            // Time text isn't in the icon container - it's its own parallel array; clone it directly.
            if (!timeInContainer && !ExpandArray(times, newTimes, existing, created))
                return false;

            return true;
        }

        // ---- styling helpers -------------------------------------------------

        // Adds a gold trim to the food icon. (Kept simple - an Outline on the icon graphic - to
        // avoid fragile hierarchy edits; a background panel can be added once the dump confirms layout.)
        private static void StyleIcon(Image icon)
        {
            if (icon == null) return;
            if (icon.gameObject.GetComponent<Outline>() != null) return;

            var outline = icon.gameObject.AddComponent<Outline>();
            outline.effectColor = GoldTrim;
            outline.effectDistance = new Vector2(2f, 2f);
        }

        // The ONLY way Fatty is allowed to make text. A TMP component added to a bare GameObject has
        // no font asset unless TMP_Settings happens to supply a default, which is how an entire panel
        // can build "successfully" and render completely blank. Cloning a live vanilla TMP_Text
        // inherits its font, material and sizing, so it always draws.
        internal static TMP_Text CloneText(Transform parent, TMP_Text template, string name)
        {
            if (parent == null || template == null) return null;

            GameObject go = Object.Instantiate(template.gameObject, parent);
            go.name = name;

            var txt = go.GetComponent<TMP_Text>();
            if (txt == null)
            {
                Object.Destroy(go);
                return null;
            }

            txt.text = string.Empty;
            txt.enableAutoSizing = false;
            return txt;
        }

        private static TMP_Text CreateCountLabel(Transform iconRoot, TMP_Text template)
        {
            TMP_Text txt = CloneText(iconRoot, template, "Fatty_StackCount");
            if (txt == null) return null;
            GameObject go = txt.gameObject;

            txt.color = GoldText;
            txt.fontStyle = FontStyles.Bold;
            // Top-right: the food timer sits at the bottom-right of the icon, so anchor the stack
            // badge to the opposite corner to keep them from overlapping.
            txt.alignment = TextAlignmentOptions.TopRight;
            txt.raycastTarget = false;

            if (go.transform is RectTransform rt)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-1f, -1f);
            }

            go.transform.SetAsLastSibling(); // render on top
            go.SetActive(false);
            return txt;
        }

        // Same gate vanilla itself uses before acting on a bare keypress (see e.g. Player.Update's
        // interact-key check in the decompile) - without it, F8 would also fire while typing in
        // chat/console or with a menu open.
        private static bool CanAcceptHotkey()
        {
            return (Chat.instance == null || !Chat.instance.HasFocus())
                && !Console.IsVisible()
                && !TextInput.IsVisible()
                && !Menu.IsVisible()
                && !InventoryGui.IsVisible();
        }

        private static void SetActiveSafe(Transform t, bool active)
        {
            if (t != null && t.gameObject.activeSelf != active)
                t.gameObject.SetActive(active);
        }

        // ---- misc helpers ----------------------------------------------------

        private static void Bail(string reason)
        {
            FattyPlugin.Instance.Log.LogWarning($"Fatty Mod: Food HUD expansion skipped - {reason}.");
        }

        private static T[] Grow<T>(T[] src, int length)
        {
            var dst = new T[length];
            for (int i = 0; i < src.Length && i < length; i++) dst[i] = src[i];
            return dst;
        }

        private static T FirstNonNull<T>(T[] arr) where T : class
        {
            if (arr == null) return null;
            foreach (var e in arr) if (e != null) return e;
            return null;
        }

        private static void DestroyClones(List<GameObject> objs)
        {
            foreach (var o in objs)
                if (o != null) Object.Destroy(o);
        }

        private static T FindComp<T>(Transform root, string name) where T : Component
        {
            if (root.name == name)
            {
                var c = root.GetComponent<T>();
                if (c != null) return c;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindComp<T>(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

    }
}
