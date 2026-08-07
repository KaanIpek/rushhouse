using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Api;
#endif

/// <summary>
/// Rewarded ads only. Every ad in Rushhouse is one the player chose to watch in exchange for
/// something concrete — no banners, no interstitials between shifts, nothing that interrupts a
/// service. That is a design decision, not an oversight: a timing game punished by a surprise
/// full-screen ad loses the player, and rewarded video pays several times better per impression.
///
/// The whole surface is <see cref="Show"/>: it hands back true/false through a callback and never
/// throws. On desktop, in the editor, or whenever the SDK has no fill, it falls back to a
/// simulated watch so the entire reward loop stays playable and testable without an Android build.
/// </summary>
public class RushhouseAds : MonoBehaviour
{
    // ---- Ad unit ids -------------------------------------------------------------------------
    // These are GOOGLE'S OFFICIAL TEST IDS. They always fill and never earn money, which is what
    // we want until the app exists in AdMob. Replace with the real unit ids from
    // apps.admob.com once "Rushhouse" is added there — and never ship the real ids while
    // testing, because clicking your own live ads is what gets an AdMob account banned.
    const string TestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
    const string TestRewardediOS = "ca-app-pub-3940256099942544/1712485313";

    // Set from AdMob once the app is registered. Empty = keep using the test unit above.
    public static string LiveRewardedAndroid = "";
    public static string LiveRewardediOS = "";

    public static RushhouseAds Instance { get; private set; }

    /// <summary>True once the SDK reported back, whether or not an ad is loaded.</summary>
    public bool Initialised { get; private set; }
    /// <summary>A real ad is loaded and ready. False means Show falls back to the simulation.</summary>
    public bool Ready { get; private set; }
    /// <summary>An ad (real or simulated) is on screen right now.</summary>
    public bool Showing { get; private set; }

    /// <summary>Raised while a simulated ad plays so the game can draw its own overlay.</summary>
    public Action<float> onSimulatedProgress;

    float retryDelay = 2f;

#if UNITY_ANDROID || UNITY_IOS
    RewardedAd rewarded;
#endif

    static string UnitId()
    {
#if UNITY_ANDROID
        return string.IsNullOrEmpty(LiveRewardedAndroid) ? TestRewardedAndroid : LiveRewardedAndroid;
#elif UNITY_IOS
        return string.IsNullOrEmpty(LiveRewardediOS) ? TestRewardediOS : LiveRewardediOS;
#else
        return "";
#endif
    }

    public static RushhouseAds Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("RushhouseAds");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<RushhouseAds>();
        Instance.Init();
        return Instance;
    }

    void Init()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Initialisation is asynchronous and can fail (no network, no consent yet). Either way the
        // callback fires, and if it does not, Ready simply stays false and Show simulates.
        MobileAds.Initialize(_ => { Initialised = true; Load(); });
#else
        Initialised = true;   // desktop/editor: simulation only, and that is the intended path
#endif
    }

    void Load()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (rewarded != null) { rewarded.Destroy(); rewarded = null; }
        RewardedAd.Load(UnitId(), new AdRequest(), (ad, error) => {
            if (error != null || ad == null) {
                Ready = false;
                // Back off exponentially, capped. Hammering a failing unit is how you get
                // throttled by the network, and the game is perfectly playable without ads.
                retryDelay = Mathf.Min(retryDelay * 2f, 64f);
                if (isActiveAndEnabled) StartCoroutine(RetryAfter(retryDelay));
                return;
            }
            retryDelay = 2f;
            rewarded = ad;
            Ready = true;
            ad.OnAdFullScreenContentClosed += () => { Ready = false; Load(); };
            ad.OnAdFullScreenContentFailed += _ => { Ready = false; Load(); };
        });
#endif
    }

    IEnumerator RetryAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Load();
    }

    /// <summary>
    /// Show a rewarded ad. <paramref name="onDone"/> gets true only if the reward was actually
    /// earned — a player who closes the ad early gets false and must not be paid.
    /// </summary>
    public void Show(Action<bool> onDone)
    {
        if (Showing) { onDone?.Invoke(false); return; }
        Showing = true;
#if UNITY_ANDROID || UNITY_IOS
        if (Ready && rewarded != null && rewarded.CanShowAd()) {
            bool earned = false;
            bool finished = false;
            // BOTH exits must call back. Subscribing only to Closed leaves the caller waiting
            // forever when the ad fails to present (no fill mid-show, process interruption), and
            // the offer sheet that opened it would sit on screen with no way out.
            Action<bool> finish = ok => {
                if (finished) return;
                finished = true;
                Showing = false;
                onDone?.Invoke(ok);
            };
            rewarded.OnAdFullScreenContentClosed += () => finish(earned);
            rewarded.OnAdFullScreenContentFailed += _ => finish(false);
            rewarded.Show(_ => earned = true);
            return;
        }
#endif
        StartCoroutine(Simulate(onDone));
    }

    // The stand-in: a timed "ad" the game draws itself. Deliberately long enough (4s) that the
    // reward still feels bought, so balance tuned against the simulation holds on device.
    IEnumerator Simulate(Action<bool> onDone)
    {
        const float duration = 4f;
        float t = 0f;
        while (t < duration) {
            t += Time.unscaledDeltaTime;
            onSimulatedProgress?.Invoke(Mathf.Clamp01(t / duration));
            yield return null;
        }
        onSimulatedProgress?.Invoke(1f);
        Showing = false;
        onDone?.Invoke(true);
    }
}
