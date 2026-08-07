using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Feasibility spike: prove the user's FBX props render as REAL 3D meshes (textured, lit, angled
// camera) before converting the whole game. Run:
//   Unity -batchmode -quit -projectPath <p> -executeMethod Rushhouse3DSpike.Capture
public static class Rushhouse3DSpike
{
    public static void Capture()
    {
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // angled camera (Overcooked-style): looks down at ~52 deg
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.32f, .46f, .28f, 1);
        cam.fieldOfView = 34f;
        camGo.transform.position = new Vector3(0f, 6.4f, -5.2f);
        camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, .97f, .9f);
        lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .57f, .6f);

        // wood floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        var fmat = new Material(Shader.Find("Standard"));
        fmat.color = new Color(.36f, .25f, .16f);
        floor.GetComponent<Renderer>().sharedMaterial = fmat;

        string[] models = { "cooktop", "table", "buns" };
        float[] xs = { -2.2f, 0f, 2.2f };
        for (int i = 0; i < models.Length; i++)
            PlaceModel(models[i], new Vector3(xs[i], 0f, 0f), 2.0f);

        string outPath = Path.Combine(WorkDir(), "z-3dspike.png");
        Render(cam, outPath, 720, 900);
        Debug.Log("SPIKE_DONE " + outPath);
    }

    static void PlaceModel(string name, Vector3 pos, float targetSize)
    {
        var prefab = Resources.Load<GameObject>("Models3D/" + name + "/base");
        if (!prefab) { Debug.LogError("SPIKE_MISSING Models3D/" + name + "/base"); return; }
        var go = (GameObject)Object.Instantiate(prefab);
        go.name = name;

        // diffuse texture -> a lit Standard material on every renderer
        var tex = Resources.Load<Texture2D>("Models3D/" + name + "/texture_diffuse");
        var mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = tex;
        mat.SetFloat("_Glossiness", 0.25f);
        var rends = go.GetComponentsInChildren<Renderer>();
        foreach (var r in rends) { var ms = new Material[r.sharedMaterials.Length]; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }

        // normalise scale by combined bounds, then drop onto the floor
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z, 1e-4f);
        float s = targetSize / maxDim;
        go.transform.localScale = go.transform.localScale * s;

        // recompute bounds after scale, sit base at y=0
        has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        go.transform.position = pos + new Vector3(0, -b.min.y, 0);
        Debug.Log("SPIKE_PLACED " + name + " tex=" + (tex ? "yes" : "NO") + " rends=" + rends.Length + " size=" + b.size);
    }

    static void Render(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        cam.targetTexture = rt; RenderTexture.active = rt;
        cam.Render();
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = prev; cam.targetTexture = null;
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
    }

    static string WorkDir()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string ws = Path.GetFullPath(Path.Combine(projectRoot, "..", ".."));
        string work = Path.Combine(ws, "work");
        Directory.CreateDirectory(work);
        return work;
    }

    // ---- orientation audit: every Models3D folder on one contact sheet, twice ----
    // Row A keeps each prefab root's IMPORTED rotation; row B forces identity. Comparing the two
    // shows which models rely on the FBX axis-correction that the game code was wiping out.
    public static void CaptureAudit()
    {
        AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.16f, .18f, .22f, 1);
        cam.orthographic = true;

        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.15f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .57f, .6f);

        string root = Path.Combine(Application.dataPath, "Resources", "Models3D");
        var folders = new System.Collections.Generic.List<string>();
        foreach (var dir in Directory.GetDirectories(root)) {
            string f = Path.GetFileName(dir);
            if (Resources.Load<GameObject>("Models3D/" + f + "/base")) folders.Add(f);
        }
        folders.Sort();
        Debug.Log("AUDIT_ORDER " + string.Join(",", folders));

        const int cols = 6; const float cell = 3.2f;
        int rows = Mathf.CeilToInt(folders.Count / (float)cols);
        for (int i = 0; i < folders.Count; i++) {
            int cx = i % cols, cz = i / cols;
            Vector3 basePos = new Vector3(cx * cell, 0, -cz * cell);
            PlaceAudit(folders[i], basePos, false);                                    // A: keep import rotation
            PlaceAudit(folders[i], basePos + new Vector3(0, 0, -rows * cell - 4f), true);  // B: identity
        }

        float w = cols * cell, h = rows * cell;
        CaptureGrid(cam, new Vector3(w * .5f - cell * .5f, 0, -h * .5f + cell * .5f), w, h, Path.Combine(WorkDir(), "z-audit-imported.png"));
        CaptureGrid(cam, new Vector3(w * .5f - cell * .5f, 0, -h * .5f + cell * .5f - rows * cell - 4f), w, h, Path.Combine(WorkDir(), "z-audit-identity.png"));
        Debug.Log("AUDIT_DONE");
    }

    static void PlaceAudit(string folder, Vector3 pos, bool forceIdentity)
    {
        var prefab = Resources.Load<GameObject>("Models3D/" + folder + "/base");
        var go = (GameObject)Object.Instantiate(prefab);
        if (forceIdentity) go.transform.rotation = Quaternion.identity;
        var tex = Resources.Load<Texture2D>("Models3D/" + folder + "/texture_diffuse");
        var mat = new Material(Shader.Find("Standard")); mat.mainTexture = tex; mat.SetFloat("_Glossiness", .2f);
        var rends = go.GetComponentsInChildren<Renderer>();
        foreach (var r in rends) { var ms = new Material[r.sharedMaterials.Length]; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms; }
        Bounds b = Cb(rends);
        float s = 2.4f / Mathf.Max(b.size.x, b.size.y, b.size.z, 1e-4f);
        go.transform.localScale = go.transform.localScale * s;
        b = Cb(rends);
        go.transform.position += pos - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    static Bounds Cb(Renderer[] rends)
    {
        Bounds b = new Bounds(); bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        return b;
    }

    static void CaptureGrid(Camera cam, Vector3 center, float w, float h, string path)
    {
        cam.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
        cam.transform.position = center + new Vector3(0, 1f, 0) - cam.transform.forward * 30f;
        cam.orthographicSize = Mathf.Max(h * .62f, w * .62f * (900f / 1400f));
        Render(cam, path, 1400, 900);
        Debug.Log("AUDIT_SHOT " + path);
    }
}
