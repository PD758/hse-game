using System;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

internal static class LevelAssetResolver
{
    public static TextAsset Resolve(string levelId, TextAsset primaryLevelAsset, TextAsset[] levelAssets, string fallbackLevelId)
    {
        string key = NormalizeLevelId(levelId);
        if (string.IsNullOrEmpty(key))
            key = NormalizeLevelId(primaryLevelAsset != null ? primaryLevelAsset.name : fallbackLevelId);

        TextAsset direct = FindInList(key, levelAssets);
        if (direct != null)
            return direct;

        if (primaryLevelAsset != null && Matches(primaryLevelAsset, key))
            return primaryLevelAsset;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Levels" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextAsset candidate = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (candidate != null && Matches(candidate, key))
                return candidate;
        }
#endif

        return null;
    }

    public static string NormalizeLevelId(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            return string.Empty;

        string value = levelId.Trim().Replace('\\', '/');
        int slash = value.LastIndexOf('/');
        if (slash >= 0)
            value = value.Substring(slash + 1);
        if (value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(0, value.Length - 5);
        return value;
    }

    private static TextAsset FindInList(string key, TextAsset[] levelAssets)
    {
        if (levelAssets == null)
            return null;

        foreach (TextAsset asset in levelAssets)
        {
            if (asset != null && Matches(asset, key))
                return asset;
        }

        return null;
    }

    private static bool Matches(TextAsset asset, string key)
    {
        if (asset == null || string.IsNullOrEmpty(key))
            return false;

        if (NormalizeLevelId(asset.name) == key)
            return true;

        try
        {
            LevelDefinition definition = JsonConvert.DeserializeObject<LevelDefinition>(asset.text);
            return NormalizeLevelId(definition?.id) == key;
        }
        catch
        {
            return false;
        }
    }
}
