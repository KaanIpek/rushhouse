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
        // The property names are GoogleMobileAdsAndroidAppId / GoogleMobileAdsIOSAppId, NOT
        // AdMobAndroidAppId (that is the private FIELD). A first version used the field names,
        // and because GetProperty returns null for a name that does not exist, the null-conditional
        // call did nothing and this method still reported success — the asset stayed empty and
        // would have failed the iOS build on CI with an error pointing at AdMob.
        var androidProp = settingsType.GetProperty("GoogleMobileAdsAndroidAppId", anyInstance);
        var iosProp = settingsType.GetProperty("GoogleMobileAdsIOSAppId", anyInstance);
        if (androidProp == null || iosProp == null) {
            Debug.LogError("ADMOB_SETUP app-id properties not found on " + settingsType.FullName);
            EditorApplication.Exit(1);
            return;
        }
        androidProp.SetValue(instance, android);
        iosProp.SetValue(instance, ios);
        EditorUtility.SetDirty((Object)instance);
        AssetDatabase.SaveAssets();

        // Read the values BACK off the asset. Reporting what we intended to write is how the
        // previous version claimed success while writing nothing.
        string wroteAndroid = (string)androidProp.GetValue(instance);
        string wroteIOS = (string)iosProp.GetValue(instance);
        bool ok = wroteAndroid == android && wroteIOS == ios;
        bool isTest = android == TestAppIdAndroid;
        Debug.Log("ADMOB_SETUP android=" + wroteAndroid + " ios=" + wroteIOS
            + " mode=" + (isTest ? "TEST" : "LIVE") + " verified=" + ok);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static string Arg(string name)
    {
        var argv = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < argv.Length - 1; i++)
            if (argv[i] == name) return argv[i + 1];
        return null;
    }
}
