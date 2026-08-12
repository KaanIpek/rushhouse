using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Entrance animation for a single UI element.
///
/// The game's UI is immediate-mode: every screen is torn down and rebuilt from code, so there is no
/// persistent hierarchy to hang a timeline off and nothing survives a rebuild. This component is the
/// answer to that -- it is attached at build time, animates itself from its OWN Update, and deletes
/// itself when finished. Nothing else has to know it exists, and a screen that rebuilds simply gets
/// fresh ones.
///
/// It reads its home position in Awake, so it MUST be added AFTER the element has been positioned.
/// </summary>
public class RushhouseUIPop : MonoBehaviour
{
    public float delay;
    public float duration = .3f;
    public Vector2 fromOffset = new Vector2(0f, 26f);
    public float fromScale = .94f;
    public bool fade = true;

    float t;
    Vector2 home;
    RectTransform rt;
    CanvasGroup cg;

    public static void Play(GameObject go, float delay, Vector2 fromOffset, float fromScale = .94f,
                            float duration = .3f)
    {
        if (!go || go.GetComponent<RushhouseUIPop>()) return;
        var p = go.AddComponent<RushhouseUIPop>();
        p.delay = delay; p.fromOffset = fromOffset; p.fromScale = fromScale; p.duration = duration;
    }

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (!rt) { Destroy(this); return; }
        home = rt.anchoredPosition;
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        // Start hidden and displaced so frame zero already looks like the beginning of the move
        // rather than a flash of the finished layout.
        cg.alpha = 0f;
        rt.anchoredPosition = home + fromOffset;
        rt.localScale = Vector3.one * fromScale;
    }

    void Update()
    {
        // UNSCALED: the pause overlay is the one screen most worth animating and it is drawn while
        // the game is frozen, so scaled time would leave it stuck invisible at alpha 0.
        t += Time.unscaledDeltaTime;
        float u = Mathf.Clamp01((t - delay) / Mathf.Max(.0001f, duration));
        // ease-out-back: overshoots ~4% then settles, which is what gives it a bit of snap instead
        // of the dead linear slide that reads as "the layout moved" rather than "the card arrived".
        float e = u >= 1f ? 1f : 1f - Mathf.Pow(1f - u, 3f);
        float overshoot = u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI) * .04f;

        if (fade) cg.alpha = e;
        rt.anchoredPosition = Vector2.LerpUnclamped(home + fromOffset, home, e);
        rt.localScale = Vector3.one * (Mathf.LerpUnclamped(fromScale, 1f, e) + overshoot);

        if (u >= 1f) {
            rt.anchoredPosition = home;
            rt.localScale = Vector3.one;
            if (fade) cg.alpha = 1f;
            // Leave the CanvasGroup (harmless, and re-adding one per rebuild costs more than keeping
            // it) but drop the driver so it stops costing an Update.
            Destroy(this);
        }
    }
}

/// <summary>
/// Press feedback. A button that does not visibly react to the finger feels broken on touch, where
/// there is no cursor and no hover state to tell you the tap registered.
/// </summary>
public class RushhouseUIPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Vector3 baseScale = Vector3.one;
    bool down;
    float t;

    public static void Attach(GameObject go)
    {
        if (go && !go.GetComponent<RushhouseUIPress>()) go.AddComponent<RushhouseUIPress>();
    }

    void Update()
    {
        // Never fight the entrance animation: while a pop is still running it owns localScale, and
        // two components writing the same field produces a visible stutter.
        if (GetComponent<RushhouseUIPop>()) return;
        t = Mathf.MoveTowards(t, down ? 1f : 0f, Time.unscaledDeltaTime * 12f);
        transform.localScale = baseScale * Mathf.Lerp(1f, .95f, t);
    }

    public void OnPointerDown(PointerEventData e) { down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; }
    public void OnPointerExit(PointerEventData e) { down = false; }
}
