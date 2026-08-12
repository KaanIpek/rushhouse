using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The primitives a modern mobile UI is actually made of.
///
/// The old interface was assembled from ornate frame ARTWORK -- menu_panel, title_plaque,
/// shop_panel, tab_gold -- so every surface arrived pre-decorated with gold filigree and hard
/// bevelled borders, and nesting them produced frames inside frames. That look dates a game
/// instantly, and it cannot be fixed by re-arranging the same sprites: the decoration is baked into
/// them.
///
/// Current casual games (Royal Match, Monopoly Go and everything shaped like them) build surfaces
/// from soft rounded rectangles lifted off the background by a shadow, with contrast and spacing
/// doing the work the borders used to do. Rounded corners also matter for touch: they enlarge the
/// apparent target and read as pressable.
///
/// So this generates those shapes procedurally instead of shipping more art:
///   Rounded(r)  a 9-sliceable rounded rectangle, tinted per use
///   Shadow(r)   the same shape with a soft outward falloff, drawn behind for elevation
/// Both are cached, and both are pure alpha so a single texture serves every colour in the palette.
/// </summary>
public static class RushhouseUIKit
{
    // ---- palette -------------------------------------------------------------------------------
    // Cool near-black ground so warm food art and the accent colours pop, matching the direction the
    // app icon already went in. One accent for "go", one for money, one for danger; nothing else.
    public static readonly Color Bg = new Color32(0x0F, 0x14, 0x1A, 0xFF);
    public static readonly Color Surface = new Color32(0x1A, 0x22, 0x2B, 0xFF);
    public static readonly Color SurfaceHi = new Color32(0x24, 0x2E, 0x39, 0xFF);
    public static readonly Color Line = new Color32(0x33, 0x3F, 0x4C, 0xFF);
    public static readonly Color Primary = new Color32(0xE4, 0x57, 0x3D, 0xFF);   // diner red — the CTA
    public static readonly Color Teal = new Color32(0x17, 0xA8, 0x9B, 0xFF);   // secondary action
    public static readonly Color Gold = new Color32(0xF2, 0xB2, 0x3E, 0xFF);   // currency only
    public static readonly Color Ink = new Color32(0xF3, 0xF6, 0xF8, 0xFF);
    public static readonly Color Muted = new Color32(0x8B, 0x99, 0xA7, 0xFF);
    public static readonly Color Danger = new Color32(0xD8, 0x43, 0x43, 0xFF);

    static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
    static readonly Dictionary<int, Sprite> shadows = new Dictionary<int, Sprite>();

    /// <summary>White rounded rectangle, 9-sliced so one texture stretches to any size.</summary>
    public static Sprite Rounded(int radius)
    {
        radius = Mathf.Clamp(radius, 2, 64);
        if (rounded.TryGetValue(radius, out var cached) && cached) return cached;

        int size = radius * 2 + 8;                       // corners + a few middle pixels to stretch
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                // Distance past the corner arc, sampled with a 1px soft edge so the curve is not
                // stair-stepped at the sizes these get stretched to.
                float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + .5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                               SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        rounded[radius] = sp;
        return sp;
    }

    /// <summary>
    /// Soft drop shadow for the same shape. Drawn as a slightly larger sibling behind a surface —
    /// this is what separates a card from the background now that nothing has a border.
    /// </summary>
    public static Sprite Shadow(int radius)
    {
        radius = Mathf.Clamp(radius, 2, 64);
        if (shadows.TryGetValue(radius, out var cached) && cached) return cached;

        int feather = 16;
        int size = (radius + feather) * 2 + 8;
        int inner = radius + feather;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = Mathf.Max(inner - x, x - (size - 1 - inner), 0f);
                float dy = Mathf.Max(inner - y, y - (size - 1 - inner), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Quadratic falloff reads as a diffuse shadow; linear looks like a hard vignette.
                float t = Mathf.Clamp01(1f - d / feather);
                px[y * size + x] = new Color32(0, 0, 0, (byte)(t * t * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                               SpriteMeshType.FullRect, new Vector4(inner, inner, inner, inner));
        shadows[radius] = sp;
        return sp;
    }
}
