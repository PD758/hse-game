#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class NetSyncPluginSetup
{
    private const string PluginPath = "Assets/Plugins/Linux/x86_64/libnetsync_unity.so";

    [MenuItem("Rogue/Configure NetSync Plugin")]
    public static void Configure()
    {
        AssetDatabase.ImportAsset(PluginPath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(PluginPath) is not PluginImporter importer)
        {
            Debug.LogWarning($"NetSync plugin importer was not available for {PluginPath}.");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithEditor(true);
        importer.SetEditorData("OS", "Linux");
        importer.SetEditorData("CPU", "x86_64");
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, true);
        importer.SetPlatformData(BuildTarget.StandaloneLinux64, "CPU", "x86_64");
        importer.SaveAndReimport();

        Debug.Log("NetSync Linux plugin configured for Editor and StandaloneLinux64.");
    }
}
#endif
