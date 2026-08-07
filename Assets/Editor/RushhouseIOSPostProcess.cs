#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using UnityEngine;

/// <summary>
/// Info.plist keys Unity does not write and Apple needs. Runs after Unity generates the Xcode
/// project, so it also runs on the CI runner — the Windows machine can never execute this because
/// it has no iOS module, which is exactly why it must live in the repo rather than in a manual step.
/// </summary>
public static class RushhouseIOSPostProcess
{
    [PostProcessBuild(999)]   // after Google Mobile Ads' own plist processor
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;

        // Answers the export-compliance questionnaire once, in the binary. Without it EVERY
        // TestFlight build lands flagged "Missing Compliance" and is un-installable by testers
        // until someone answers it in the web UI — it does not fail the upload, so it reads as a
        // processing delay. Rushhouse uses only HTTPS via OS-provided TLS, which is exempt.
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

        // Portrait only, matching the Player Settings. Stated here too because Unity writes the
        // supported-orientation arrays from its own settings and a mismatch between the two is
        // one of the ways a build ends up rotating on iPad only.
        var orientations = root.CreateArray("UISupportedInterfaceOrientations");
        orientations.AddString("UIInterfaceOrientationPortrait");
        var padOrientations = root.CreateArray("UISupportedInterfaceOrientations~ipad");
        padOrientations.AddString("UIInterfaceOrientationPortrait");

        // NO NSUserTrackingUsageDescription and no ATT call anywhere in this build. Adding the key
        // without showing the prompt, or showing the prompt without the key, are both rejections;
        // see RushhouseConsent for the full reasoning and what to change if ATT is added later.

        plist.WriteToFile(plistPath);
        Debug.Log("IOS_POSTPROCESS wrote ITSAppUsesNonExemptEncryption=false and portrait orientation to "
            + plistPath);
    }
}
#endif
