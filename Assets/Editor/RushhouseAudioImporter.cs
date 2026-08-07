using UnityEditor;
using UnityEngine;

// Music ships as 32-second stereo WAV (~5.6 MB each). Left on Unity's defaults that is ~21 MB of
// build and a fully decompressed clip resident in memory. These are long, looping, never-retriggered
// tracks -- the textbook case for streaming Vorbis, which is what this enforces on import so the
// setting survives a fresh clone or a Library wipe.
public class RushhouseAudioImporter : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .6f;
        settings.preloadAudioData = false;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
    }

    // One-shot reimport for clips already in the Library with the old settings.
    [MenuItem("Rushhouse/Reimport Music")]
    public static void ReimportMusic()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Music" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("MUSIC_REIMPORTED");
    }
}
