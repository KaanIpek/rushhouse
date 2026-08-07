using UnityEditor;

// Keep the runtime 3D set appropriate for a mobile game.  The source art is
// much larger than these props ever appear on screen, so importing it at 2K
// wastes memory and build space without improving the image.
public sealed class RushhouseMobileAssetOptimizer : AssetPostprocessor
{
    public static void ForceReimportRuntimeTextures()
    {
        string[] folders = {
            "Assets/Resources/Models3D",
            "Assets/Resources/Art/CharactersRigged",
            "Assets/Resources/Art/CharactersDirectional"
        };
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
    }

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        var importer = (TextureImporter)assetImporter;
        if (path.Contains("/Resources/Models3D/")) {
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 65;
            importer.crunchedCompression = true;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            return;
        }

        // These are the original animated sprite frames (not replacement 3D characters).  They are
        // drawn at roughly 35-60 px in portrait gameplay, so 128 px retains the art while avoiding
        // hundreds of MB of runtime texture memory across 1,376 individual animation frames.
        if (path.Contains("/Resources/Art/CharactersRigged/")) {
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 72;
            importer.crunchedCompression = true;
            importer.mipmapEnabled = false;
            return;
        }

        if (path.Contains("/Resources/Art/CharactersDirectional/")) {
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 72;
            importer.crunchedCompression = true;
            importer.mipmapEnabled = false;
        }
    }
}
