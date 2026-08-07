using System;
using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS
using GoogleMobileAds.Ump.Api;
#endif

/// <summary>
/// GDPR consent gate, run once at startup before any ad is requested.
///
/// A Google-certified CMP has been mandatory for EEA/UK traffic since Jan 2024 and Switzerland
/// since Jul 2024. Without one, AdMob does not refuse to run — it quietly drops those users to
/// limited/non-personalised ads, so the integration looks fine and simply earns much less. The
/// User Messaging Platform SDK bundled with Google Mobile Ads v11 is that CMP; there is nothing
/// extra to install.
///
/// The order below is the one Google specifies and it matters:
///   Update() -> LoadAndShowConsentFormIfRequired() -> CanRequestAds() -> MobileAds.Initialize()
/// Initialising the ads SDK first would request ads before consent is known.
///
/// DELIBERATELY NOT DOING: App Tracking Transparency. Showing the ATT prompt requires
/// NSUserTrackingUsageDescription in Info.plist AND declaring "Data Used to Track You" in the App
/// Privacy label — and Apple rejects builds where those three do not agree. A known rejection also
/// comes from configuring BOTH a GDPR and an IDFA message in AdMob, which makes UMP show the ATT
/// prompt even after the user denied consent. So this build ships with no ATT prompt, no tracking
/// declaration, and non-personalised ads on iOS. That costs revenue, not compliance. Adding ATT
/// later means: set the usage-description field in Google Mobile Ads Settings, call ATT from here
/// AFTER this flow completes, update the privacy label, and leave the AdMob IDFA message OFF.
/// </summary>
public static class RushhouseConsent
{
    /// <summary>True once the gate has finished, however it finished.</summary>
    public static bool Resolved { get; private set; }
    /// <summary>True when ads may be requested. False only if consent is required and refused.</summary>
    public static bool CanRequestAds { get; private set; }
    /// <summary>True when a "Privacy options" entry must be offered in settings (GDPR requirement).</summary>
    public static bool PrivacyOptionsRequired { get; private set; }

    static bool started;

    /// <summary>Runs the gate once. `onDone` fires on the main thread whatever happens.</summary>
    public static void Gather(Action onDone = null)
    {
        if (started) { onDone?.Invoke(); return; }
        started = true;

#if UNITY_ANDROID || UNITY_IOS
        var request = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
        ConsentInformation.Update(request, error => {
            if (error != null) {
                // A network failure here must not brick the game. Fall through with whatever
                // consent state is cached; CanRequestAds reflects it honestly.
                Debug.LogWarning("CONSENT update failed: " + error.Message);
                Finish(onDone);
                return;
            }
            ConsentForm.LoadAndShowConsentFormIfRequired(formError => {
                if (formError != null) Debug.LogWarning("CONSENT form failed: " + formError.Message);
                Finish(onDone);
            });
        });
#else
        // Desktop/editor: no CMP exists and no ads are served, so the gate is open.
        CanRequestAds = true;
        Resolved = true;
        onDone?.Invoke();
#endif
    }

    /// <summary>Re-opens the consent form. GDPR requires this to stay reachable after the first run.</summary>
    public static void ShowPrivacyOptions(Action onDone = null)
    {
#if UNITY_ANDROID || UNITY_IOS
        ConsentForm.ShowPrivacyOptionsForm(error => {
            if (error != null) Debug.LogWarning("CONSENT privacy options failed: " + error.Message);
            RefreshFlags();
            onDone?.Invoke();
        });
#else
        onDone?.Invoke();
#endif
    }

#if UNITY_ANDROID || UNITY_IOS
    static void Finish(Action onDone)
    {
        RefreshFlags();
        Resolved = true;
        Debug.Log("CONSENT resolved canRequestAds=" + CanRequestAds
            + " privacyOptionsRequired=" + PrivacyOptionsRequired);
        onDone?.Invoke();
    }

    static void RefreshFlags()
    {
        // Read the SDK's own answer. Parsing the IABTCF_* strings out of preferences to decide
        // this instead is a TCF 3.3 policy violation that only ever surfaces in AdMob reporting.
        CanRequestAds = ConsentInformation.CanRequestAds();
        PrivacyOptionsRequired =
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    }
#endif
}
