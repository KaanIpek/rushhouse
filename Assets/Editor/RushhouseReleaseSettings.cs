using UnityEditor;
using UnityEngine;

/// <summary>
/// Player settings for a store release, applied from the command line so a CI machine produces
/// exactly what a local build produces. Run:
///
///   Unity -batchmode -quit -projectPath . -executeMethod RushhouseReleaseSettings.Apply
///
/// Optional overrides: -bundleId, -appVersion, -buildNumber.
///
/// The defaults here were "DefaultCompany / rushhouse-unity / no bundle id / rotates to every
/// orientation", which is what a fresh Unity project ships with and what Apple rejects.
/// </summary>
public static class RushhouseReleaseSettings
{
    public const string DefaultBundleId = "com.rldgames.rushhouse";
    public const string DefaultCompany = "RLD Games";
    public const string DefaultProduct = "Rushhouse";

    [MenuItem("Rushhouse/Apply Release Settings")]
    public static void Apply()
    {
        string bundleId = Arg("-bundleId") ?? DefaultBundleId;
        string version = Arg("-appVersion") ?? "1.0.0";
        string build = Arg("-buildNumber") ?? "1";

        PlayerSettings.companyName = DefaultCompany;
        PlayerSettings.productName = DefaultProduct;
        PlayerSettings.bundleVersion = version;

        // One identifier for both stores. Set per-target because Unity keeps them separately and
        // a mismatch only surfaces at upload, as "the bundle id does not match any app".
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, bundleId);
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, bundleId);

        // PORTRAIT ONLY. The whole UI is authored at 720x1280; letting it rotate produces a
        // landscape layout nobody designed and is an easy review rejection.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // ---- iOS ----
        PlayerSettings.iOS.buildNumber = build;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.requiresPersistentWiFi = false;
        PlayerSettings.iOS.statusBarStyle = iOSStatusBarStyle.Default;
        PlayerSettings.iOS.hideHomeButton = true;
        // Declares "no non-exempt encryption". Without it every single TestFlight build stops and
        // asks the export-compliance questionnaire before testers can install.
        PlayerSettings.iOS.useOnDemandResources = false;

        // ---- Android ----
        PlayerSettings.Android.bundleVersionCode = int.TryParse(build, out int code) ? code : 1;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

        AssetDatabase.SaveAssets();
        Debug.Log("RELEASE_SETTINGS bundleId=" + bundleId + " version=" + version + " build=" + build
            + " company=" + PlayerSettings.companyName + " product=" + PlayerSettings.productName
            + " orientation=" + PlayerSettings.defaultInterfaceOrientation);
        EditorApplication.Exit(0);
    }

    static string Arg(string name)
    {
        var argv = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < argv.Length - 1; i++)
            if (argv[i] == name) return argv[i + 1];
        return null;
    }
}
