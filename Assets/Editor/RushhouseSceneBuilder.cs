using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RushhouseSceneBuilder
{
    public static void BuildMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5.15f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.03f, .035f, .05f, 1);
        cameraObject.transform.position = new Vector3(0, -0.34f, -10);

        var game = new GameObject("Rushhouse Unity Game");
        game.AddComponent<RushhouseUnityGame>();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("Rushhouse Unity main scene built at Assets/Scenes/Main.unity");
    }

    public static void BuildWindowsPlayer()
    {
        BuildMainScene();

        const string outputDir = "Builds/Windows";
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = Path.Combine(outputDir, "RushhouseUnity.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded) {
            throw new System.Exception("Rushhouse Windows build failed: " + report.summary.result);
        }

        Debug.Log("Rushhouse Windows build at " + options.locationPathName);
    }

    /// <summary>
    /// Emits the Xcode project that the macOS half of the CI job archives. Never produces an
    /// .ipa itself -- Unity cannot sign, and signing is deliberately deferred to `-exportArchive`
    /// so no certificate ever has to exist on the build machine.
    ///
    /// This CANNOT run on the Windows dev box: without the iOS module Unity refuses the target.
    /// It runs inside unityci/editor:ubuntu-6000.4.8f1-ios-*, which has it.
    /// </summary>
    public static void BuildIOSProject()
    {
        // Same editor launch, so the licence seat is claimed once.
        if (!RushhouseReleaseSettings.ApplySettings())
            throw new System.Exception("release settings failed; see RELEASE_SETTINGS above");
        BuildMainScene();

        const string outputDir = "Builds/iOS";
        // Unity APPENDS to an existing iOS project by default, which quietly keeps stale
        // generated sources from a previous editor version. CI starts clean every time.
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        Directory.CreateDirectory(outputDir);

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            throw new System.Exception(
                "iOS build support is not installed in this editor. Run this in the GameCI "
                + "ubuntu-<version>-ios image, or add the iOS module via Unity Hub.");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = outputDir,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Rushhouse iOS project build failed: " + report.summary.result);

        // Say out loud whether the ads SDK left a Podfile. Without one the macOS job builds the
        // .xcodeproj instead of the .xcworkspace and fails at link time with missing GAD symbols,
        // an error that names neither CocoaPods nor AdMob.
        bool podfile = File.Exists(Path.Combine(outputDir, "Podfile"));
        Debug.Log("IOS_PROJECT built at " + outputDir + " podfile=" + podfile
            + " sizeMB=" + (report.summary.totalSize / (1024 * 1024)));
    }
}
