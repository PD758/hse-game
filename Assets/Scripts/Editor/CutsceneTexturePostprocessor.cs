using UnityEditor;

public sealed class CutsceneTexturePostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string normalizedPath = assetPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith("Assets/Resources/Cutscenes/"))
            return;
        if (!normalizedPath.EndsWith(".jpg") && !normalizedPath.EndsWith(".jpeg") && !normalizedPath.EndsWith(".png"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.sRGBTexture = true;
    }
}
