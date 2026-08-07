using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes the AdMob App ID into GoogleMobileAdsSettings so an Android build embeds it in the
/// manifest. Without it the app crashes on start — the SDK treats a missing App ID as fatal.
///
/// The default below is GOOGLE'S OFFICIAL TEST APP ID. It always fills, never earns, and cannot
/// get an account banned. Swap it for the real one only after "Rushhouse" exists in AdMob:
///
///   Unity -batchmode -quit -projectPath . -executeMethod RushhouseAdMobSetup.Apply \
///     -adMobAndroidAppId ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY
///
/// The per-placement rewarded UNIT ids live in RushhouseAds.LiveRewardedAndroid/iOS; the App ID
/// here and the unit ids there are different things and AdMob shows both.
/// </summary>
public static class RushhouseAdMobSetup
{
    public const string TestAppIdAndroid = "ca-app-pub-3940256099942544~3347511713";
    public const string TestAppIdiOS = "ca-app-pub-3940256099942544~1458002511";

    [MenuItem("Rushhouse/Apply AdMob Settings")]
    public static void Apply()
    {
        string android = Arg("-adMobAndroidAppId") ?? TestAppIdAndroid;
        string ios = Arg("-adMobIOSAppId") ?? TestAppIdiOS;

        // The settings class is internal and lives in its own asmdef, so it is reached by
        // reflection with the assembly-qualified name rather than a direct reference.
        var settingsType = System.Type.GetType(
            "GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
        if (settingsType == null) {
            Debug.LogError("ADMOB_SETUP GoogleMobileAdsSettings type not found — is the SDK imported?");
            EditorApplication.Exit(1);
            return;
        }
        const System.Reflection.BindingFlags anyStatic = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var instance = settingsType.GetMethod("LoadInstance", anyStatic)?.Invoke(null, null);
        if (instance == null) {
            Debug.LogError("ADMOB_SETUP could not load the settings asset");
            EditorApplication.Exit(1);
            return;
        }
        const System.Reflection.BindingFlags anyInstance = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        settingsType.GetProperty("AdMobAndroidAppId", anyInstance)?.SetValue(instance, android);
        settingsType.GetProperty("AdMobIOSAppId", anyInstance)?.SetValue(instance, ios);
        EditorUtility.SetDirty((Object)instance);
        AssetDatabase.SaveAssets();

        bool isTest = android == TestAppIdAndroid;
        Debug.Log("ADMOB_SETUP android=" + android + " ios=" + ios + " mode=" + (isTest ? "TEST" : "LIVE"));
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
