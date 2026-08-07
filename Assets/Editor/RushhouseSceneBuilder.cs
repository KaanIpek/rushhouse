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
}
