using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;

public sealed partial class PrototypeGame
{
    public void BakeSceneForEditor()
    {
        ClearBakedSceneObjects();
        SetupCamera();
        EnsurePostProcessing();
        CreateSprites();
        PersistGeneratedSpritesForEditor();
        EnsureHudTextures();
        PersistHudTexturesForEditor();
        BuildLevel();
        CreateViews();
        CreateLighting();
        RedrawAll();
        EditorUtility.SetDirty(this);
    }

    private void ClearBakedSceneObjects()
    {
        DestroySceneObject("Tiles");
        DestroySceneObject("Player");
        DestroySceneObject("Channel Light");
        DestroySceneObject("Player Light");
        DestroySceneObject("Post Processing");

        foreach (Light2D light in GetComponents<Light2D>())
            DestroyImmediate(light);

        foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (obj.scene != gameObject.scene)
                continue;

            if (obj.name.StartsWith("Signal Blocker ", StringComparison.Ordinal) ||
                obj.name.StartsWith("Enemy ", StringComparison.Ordinal))
            {
                DestroyImmediate(obj);
            }
        }
    }

    private static void DestroySceneObject(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
            DestroyImmediate(obj);
    }

    private void PersistGeneratedSpritesForEditor()
    {
        const string folder = "Assets/Generated/Prototype/Sprites";
        floorSprite = PersistSpriteForEditor(folder, "floor_base", floorSprite);
        wallSprite = PersistSpriteForEditor(folder, "wall_horizontal", wallSprite);
        wallVerticalSprite = PersistSpriteForEditor(folder, "wall_vertical", wallVerticalSprite);
        wallCornerSprite = PersistSpriteForEditor(folder, "wall_corner", wallCornerSprite);
        plateSprite = PersistSpriteForEditor(folder, "pressure_plate", plateSprite);
        pressedPlateSprite = PersistSpriteForEditor(folder, "pressure_plate_pressed", pressedPlateSprite);
        gateSprite = PersistSpriteForEditor(folder, "signal_gate_closed", gateSprite);
        openGateSprite = PersistSpriteForEditor(folder, "signal_gate_open", openGateSprite);
        exitSprite = PersistSpriteForEditor(folder, "tv_exit", exitSprite);
        openExitSprite = PersistSpriteForEditor(folder, "tv_exit_open", openExitSprite);
        rubbleSprite = PersistSpriteForEditor(folder, "rubble", rubbleSprite);
        trapSprite = PersistSpriteForEditor(folder, "camera_trap", trapSprite);
        remoteSprite = PersistSpriteForEditor(folder, "remote", remoteSprite);
        storySprite = PersistSpriteForEditor(folder, "story_note", storySprite);
        healSprite = PersistSpriteForEditor(folder, "heal_cassette", healSprite);
        playerSprite = PersistSpriteForEditor(folder, "player_base", playerSprite);
        stoneSprite = PersistSpriteForEditor(folder, "signal_blocker", stoneSprite);
        enemySprite = PersistSpriteForEditor(folder, "enemy_patrol", enemySprite);
        enemyInvestigateSprite = PersistSpriteForEditor(folder, "enemy_investigate", enemyInvestigateSprite);
        enemyHuntSprite = PersistSpriteForEditor(folder, "enemy_hunt", enemyHuntSprite);
        enemyBeamSprite = PersistSpriteForEditor(folder, "enemy_beam", enemyBeamSprite);
        floorSprites = PersistSpriteArrayForEditor(folder, "floor", floorSprites);
        floorDecalSprites = PersistSpriteArrayForEditor(folder, "floor_decal", floorDecalSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_idle", playerIdleSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_walk_1", playerWalkOneSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_walk_2", playerWalkTwoSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_attack", playerAttackSprites);
    }

    private void PersistHudTexturesForEditor()
    {
        const string folder = "Assets/Generated/Prototype/HUD";
        hudTexture = PersistTextureForEditor(folder, "hud_fill", hudTexture);
        hudPanelTexture = PersistTextureForEditor(folder, "hud_panel", hudPanelTexture);
        ratingFrameNeutralTexture = PersistTextureForEditor(folder, "rating_frame_neutral", ratingFrameNeutralTexture);
        ratingFramePuzzleTexture = PersistTextureForEditor(folder, "rating_frame_puzzle", ratingFramePuzzleTexture);
        ratingFrameCombatTexture = PersistTextureForEditor(folder, "rating_frame_combat", ratingFrameCombatTexture);
        ratingFrameCriticalTexture = PersistTextureForEditor(folder, "rating_frame_critical", ratingFrameCriticalTexture);
        whiteTexture = PersistTextureForEditor(folder, "white", whiteTexture);
    }

    private static Sprite[] PersistSpriteArrayForEditor(string folder, string prefix, Sprite[] sprites)
    {
        if (sprites == null)
            return Array.Empty<Sprite>();

        var persisted = new Sprite[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            persisted[i] = PersistSpriteForEditor(folder, $"{prefix}_{i}", sprites[i]);

        return persisted;
    }

    private static void PersistPlayerSpriteArrayForEditor(string folder, string prefix, Sprite[] sprites)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = PersistSpriteForEditor(folder, $"{prefix}_{i}", sprites[i]);
    }

    private static Sprite PersistSpriteForEditor(string folder, string assetName, Sprite sprite)
    {
        if (sprite == null || AssetDatabase.Contains(sprite))
            return sprite;

        Texture2D texture = CopySpriteTexture(sprite);
        Texture2D importedTexture = PersistTextureForEditor(folder, assetName, texture);
        UnityEngine.Object.DestroyImmediate(texture);

        string path = $"{folder}/{assetName}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? Sprite.Create(importedTexture, new Rect(0, 0, importedTexture.width, importedTexture.height), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
    }

    private static Texture2D PersistTextureForEditor(string folder, string assetName, Texture2D texture)
    {
        if (texture == null || AssetDatabase.Contains(texture))
            return texture;

        EnsureAssetFolder(folder);
        string path = $"{folder}/{assetName}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D CopySpriteTexture(Sprite sprite)
    {
        Rect rect = sprite.rect;
        var texture = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = sprite.name,
        };
        texture.SetPixels(sprite.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), texture.width, texture.height));
        texture.Apply(false, false);
        return texture;
    }

    private static void EnsureAssetFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
