using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Fatty.Synergies
{
    // Resolves each synergy's icon: an embedded PNG if one was shipped, otherwise a
    // procedurally-drawn, gold-trimmed fallback so the mod always has a valid icon.
    public static class SynergyIcons
    {
        private const int Size = 64;

        // Valheim's status-effect HUD template draws our sprite over its own icon slot background,
        // which is light/parchment-coloured - our artwork and procedural badges are circular with
        // transparent corners, so those corners were showing that light background through as an ugly
        // white square around the badge. Baking every icon onto this opaque dark backdrop (instead of
        // leaving true transparency) means only OUR colour ever shows, regardless of what Valheim's
        // own template looks like behind it.
        private static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);

        // Returns the embedded PNG for baseName ("BalancedDiet", ...) if present, else the drawn fallback.
        public static Sprite Resolve(string baseName, Func<Sprite> proceduralFallback)
        {
            try
            {
                Sprite embedded = LoadEmbedded(baseName);
                if (embedded != null) return embedded;
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: could not load embedded icon '{baseName}': {ex.Message}");
            }

            try
            {
                return proceduralFallback?.Invoke();
            }
            catch (Exception ex)
            {
                FattyPlugin.Instance.Log.LogWarning($"Fatty: could not draw fallback icon '{baseName}': {ex.Message}");
                return null;
            }
        }

        // Looks for an embedded resource ending in "<baseName>.png" (namespace-agnostic).
        private static Sprite LoadEmbedded(string baseName)
        {
            Assembly asm = typeof(SynergyIcons).Assembly;
            string suffix = "." + baseName + ".png";

            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                using (Stream s = asm.GetManifestResourceStream(res))
                {
                    if (s == null) continue;
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        byte[] data = ms.ToArray();

                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        {
                            wrapMode = TextureWrapMode.Clamp,
                            filterMode = FilterMode.Bilinear,
                            name = "Fatty_" + baseName
                        };

                        if (!LoadPng(tex, data)) // sizes the texture from the PNG
                        {
                            UnityEngine.Object.Destroy(tex);
                            return null;
                        }

                        FlattenAlpha(tex, BackgroundColor);
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            return null;
        }

        // Decodes PNG/JPG bytes into the texture. Called via reflection because
        // UnityEngine.ImageConversionModule can't be referenced directly under net48
        // (it targets a newer netstandard).
        private static MethodInfo _loadImage;
        private static bool LoadPng(Texture2D tex, byte[] data)
        {
            if (_loadImage == null)
            {
                Type t = AccessTools.TypeByName("UnityEngine.ImageConversion");
                _loadImage = t != null
                    ? AccessTools.Method(t, "LoadImage", new[] { typeof(Texture2D), typeof(byte[]) })
                    : null;
            }
            if (_loadImage == null) return false;

            object result = _loadImage.Invoke(null, new object[] { tex, data });
            return result is bool b && b;
        }

        // Composites the (possibly transparent) decoded PNG onto an opaque backdrop, in place, so the
        // final texture has no transparent pixels left for Valheim's own template background to show
        // through.
        private static void FlattenAlpha(Texture2D tex, Color bg)
        {
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                float a = c.a;
                px[i] = new Color(
                    c.r * a + bg.r * (1f - a),
                    c.g * a + bg.g * (1f - a),
                    c.b * a + bg.b * (1f - a),
                    1f);
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
        }

        private static readonly Color Gold = new Color(0.82f, 0.66f, 0.22f, 1f);
        private static readonly Color GoldLight = new Color(1f, 0.90f, 0.45f, 1f);

        private enum Emblem { Equals, Star, Wave, Tree, Rune, Blade, Horn, Hook, Bubbles }

        public static Sprite BalancedDiet() => Build(new Color(0.34f, 0.68f, 0.32f), Emblem.Equals);
        public static Sprite SugarRush() => Build(new Color(0.88f, 0.36f, 0.66f), Emblem.Star);
        public static Sprite Fisherman() => Build(new Color(0.24f, 0.55f, 0.85f), Emblem.Wave);
        public static Sprite Lumberjack() => Build(new Color(0.70f, 0.45f, 0.22f), Emblem.Tree);

        // Drink buffs.
        public static Sprite ArcaneDraught() => Build(new Color(0.45f, 0.35f, 0.78f), Emblem.Rune);
        public static Sprite WarriorsDraught() => Build(new Color(0.72f, 0.24f, 0.22f), Emblem.Blade);
        public static Sprite RevelersDraught() => Build(new Color(0.85f, 0.62f, 0.20f), Emblem.Horn);
        public static Sprite SeafarersDraught() => Build(new Color(0.20f, 0.62f, 0.62f), Emblem.Hook);
        public static Sprite Drunk() => Build(new Color(0.58f, 0.44f, 0.68f), Emblem.Bubbles);

        private static Sprite Build(Color baseColor, Emblem emblem)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Fatty_SynergyIcon"
            };

            var px = new Color[Size * Size];
            float c = (Size - 1) / 2f;
            float rOuter = Size * 0.47f;
            float rRingInner = Size * 0.40f;

            Color inner = Color.Lerp(baseColor, Color.white, 0.30f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    Color col;
                    if (d > rOuter)
                    {
                        col = BackgroundColor; // opaque, not transparent - see BackgroundColor comment
                    }
                    else if (d > rRingInner)
                    {
                        col = Gold; // gold ring
                    }
                    else
                    {
                        float t = Mathf.Clamp01(d / rRingInner);
                        col = Color.Lerp(inner, baseColor, t); // shaded disc

                        float nx = dx / rRingInner;
                        float ny = dy / rRingInner;
                        if (InEmblem(emblem, nx, ny))
                            col = GoldLight;
                    }

                    // Soft 1px edge antialias on the outer circle - blend colour toward the opaque
                    // backdrop instead of fading alpha, since the whole texture stays fully opaque.
                    if (d > rOuter - 1f && d <= rOuter)
                        col = Color.Lerp(BackgroundColor, col, Mathf.Clamp01(rOuter - d));

                    col.a = 1f;
                    px[y * Size + x] = col;
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, false);

            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        // nx/ny are in [-1, 1] within the disc.
        private static bool InEmblem(Emblem emblem, float nx, float ny)
        {
            switch (emblem)
            {
                case Emblem.Equals: // two bars = balance
                    return Mathf.Abs(nx) < 0.52f && (Between(ny, 0.14f, 0.32f) || Between(ny, -0.32f, -0.14f));

                case Emblem.Star: // plus / sparkle
                    return (Mathf.Abs(nx) < 0.14f && Mathf.Abs(ny) < 0.58f) ||
                           (Mathf.Abs(ny) < 0.14f && Mathf.Abs(nx) < 0.58f);

                case Emblem.Wave: // sine wave = water
                    {
                        if (Mathf.Abs(nx) > 0.62f) return false;
                        float target = 0.26f * Mathf.Sin(nx * Mathf.PI * 2f);
                        return Mathf.Abs(ny - target) < 0.11f;
                    }

                case Emblem.Tree: // triangle + trunk
                    {
                        // triangle: widening from top (ny=-0.5) to base (ny=0.30)
                        float top = -0.5f, baseY = 0.30f;
                        if (Between(ny, top, baseY))
                        {
                            float halfWidth = 0.55f * ((ny - top) / (baseY - top));
                            if (Mathf.Abs(nx) <= halfWidth) return true;
                        }
                        // trunk
                        return Mathf.Abs(nx) < 0.10f && Between(ny, 0.30f, 0.52f);
                    }

                case Emblem.Rune: // stave with two diagonal arms
                    {
                        if (Mathf.Abs(nx) < 0.10f && Mathf.Abs(ny) < 0.55f) return true;
                        // upper-right arm and lower-left arm, mirrored about the stave
                        if (Between(ny, -0.42f, -0.02f) && Mathf.Abs(nx - (ny + 0.42f) * 0.95f) < 0.11f) return true;
                        if (Between(ny, 0.02f, 0.42f) && Mathf.Abs(nx + (0.42f - ny) * 0.95f) < 0.11f) return true;
                        return false;
                    }

                case Emblem.Blade: // blade with a crossguard
                    {
                        if (Mathf.Abs(nx) < 0.11f && Between(ny, -0.55f, 0.30f)) return true;  // blade
                        if (Mathf.Abs(nx) < 0.36f && Between(ny, 0.30f, 0.40f)) return true;   // guard
                        return Mathf.Abs(nx) < 0.08f && Between(ny, 0.40f, 0.56f);             // grip
                    }

                case Emblem.Horn: // drinking horn: wedge tapering to the left
                    {
                        if (!Between(nx, -0.55f, 0.45f)) return false;
                        float t = (nx + 0.55f) / 1.0f;              // 0 at tip, 1 at mouth
                        float half = Mathf.Lerp(0.04f, 0.30f, t);
                        float centre = Mathf.Lerp(0.28f, -0.10f, t); // curve upward toward the mouth
                        return Mathf.Abs(ny - centre) < half;
                    }

                case Emblem.Hook: // fishing hook: shank plus a J curve
                    {
                        if (Mathf.Abs(nx - 0.18f) < 0.10f && Between(ny, -0.52f, 0.12f)) return true;
                        float d = Mathf.Sqrt((nx - 0.02f) * (nx - 0.02f) + (ny - 0.20f) * (ny - 0.20f));
                        return Mathf.Abs(d - 0.24f) < 0.10f && ny > 0.06f;
                    }

                case Emblem.Bubbles: // three rising bubbles
                    {
                        if (InCircle(nx, ny, -0.20f, 0.22f, 0.20f)) return true;
                        if (InCircle(nx, ny, 0.18f, -0.02f, 0.16f)) return true;
                        return InCircle(nx, ny, -0.06f, -0.34f, 0.12f);
                    }
            }
            return false;
        }

        private static bool InCircle(float nx, float ny, float cx, float cy, float r)
        {
            float dx = nx - cx, dy = ny - cy;
            return dx * dx + dy * dy < r * r;
        }

        private static bool Between(float v, float lo, float hi) => v >= lo && v <= hi;
    }
}
