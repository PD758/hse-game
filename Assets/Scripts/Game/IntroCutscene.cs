using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class IntroCutscene : MonoBehaviour
{
    private const float Duration = 9.5f;

    private SpriteRenderer screenRenderer;
    private SpriteRenderer glowRenderer;
    private SpriteRenderer beamRenderer;
    private SpriteRenderer fadeRenderer;
    private SpriteRenderer viewerRenderer;
    private SpriteRenderer viewerCastShadowRenderer;
    private Light2D tvLight;
    private Light2D pullLight;
    private float startedAt;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
        NarrativeRunState.Reset();
        if (!BindSceneReferences())
            BuildScene();
        startedAt = Time.time;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) || Time.time - startedAt >= Duration)
            SceneManager.LoadScene("Prototype");

        AnimateScene();
    }

    private void OnGUI()
    {
        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Screen.width < 760 ? 12 : 14,
            normal = { textColor = new Color(0.62f, 0.66f, 0.72f, 0.82f) },
        };
        PixelGui.Apply(hintStyle);
        GUI.Label(new Rect(12, Screen.height - 36, Screen.width - 24, 24), "Space/Enter: пропустить | Esc: меню", hintStyle);
    }

    private void AnimateScene()
    {
        float elapsed = Time.time - startedAt;
        float t = Mathf.Clamp01(elapsed / Duration);
        float pulse = 0.5f + Mathf.Sin(Time.time * 18f) * 0.5f;

        screenRenderer.color = Color.Lerp(new Color(0.36f, 0.48f, 0.64f), new Color(0.82f, 0.92f, 1.00f), pulse * 0.45f + t * 0.25f);
        glowRenderer.color = new Color(0.55f, 0.82f, 1f, Mathf.Lerp(0.04f, 0.14f, t) + pulse * 0.02f);
        Vector3 glowBaseScale = OnePixelScale(9.6f, 6f);
        glowRenderer.transform.localScale = new Vector3(glowBaseScale.x * (1f + pulse * 0.05f), glowBaseScale.y * (1f + t * 0.18f), 1f);
        beamRenderer.color = new Color(0.62f, 0.88f, 1f, Mathf.SmoothStep(0f, 0.12f, Mathf.Clamp01((t - 0.44f) / 0.34f)));
        float castShadow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.58f) / 0.30f));
        viewerCastShadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.42f, castShadow));
        Vector3 shadowBaseScale = OnePixelScale(3f, 4.5f);
        viewerCastShadowRenderer.transform.localScale = new Vector3(shadowBaseScale.x * (1f + castShadow * 0.22f), shadowBaseScale.y * (1f + castShadow * 0.30f), 1f);
        tvLight.intensity = Mathf.Lerp(1.15f, 1.75f, t) + pulse * 0.12f;
        tvLight.pointLightOuterRadius = Mathf.Lerp(5.2f, 6.8f, t);
        pullLight.intensity = Mathf.SmoothStep(0f, 1.55f, Mathf.Clamp01((t - 0.48f) / 0.34f));
        viewerRenderer.transform.position = Vector3.Lerp(new Vector3(0f, -1.82f, 0f), new Vector3(0f, -1.52f, 0f), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.55f) / 0.35f)));
        fadeRenderer.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.96f, Mathf.Clamp01((t - 0.72f) / 0.28f)));

        Camera camera = Camera.main;
        if (camera != null)
            camera.orthographicSize = Mathf.Lerp(5.4f, 4.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.55f)));
    }

    private void BuildScene()
    {
        var root = new GameObject("Intro Art");
        CreateSpriteObject("Room Floor", CreateRoomFloorSprite(), Vector3.zero, OnePixelScale(25f, 10.4f), -10);
        CreateSpriteObject("Window Shadow", CreateSoftRectSprite(new Color(0.015f, 0.018f, 0.022f, 0.46f)), new Vector3(-3.8f, 2.3f, 0f), OnePixelScale(8f, 1f), -8);
        CreateSpriteObject("TV Cabinet", CreateRectSprite(new Color(0.13f, 0.115f, 0.108f)), new Vector3(0f, 1.82f, 0f), OnePixelScale(3.75f, 0.82f), -3);

        SpriteRenderer couchShadow = CreateSpriteObject("Couch Shadow", CreateEllipseSprite(new Color(0f, 0f, 0f, 0.45f)), new Vector3(0f, -2.18f, 0f), Vector3.one, -4);
        couchShadow.transform.localScale = OnePixelScale(7f, 1.35f);
        SpriteRenderer couch = CreateSpriteObject("Couch", CreateCouchSprite(), new Vector3(0f, -2.0f, 0f), OnePixelScale(5f, 2f), 1);
        viewerRenderer = CreateSpriteObject("Viewer", CreateViewerSprite(), new Vector3(0f, -1.82f, 0f), OnePixelScale(1.5f, 2f), 5);
        viewerCastShadowRenderer = CreateSpriteObject("Viewer Cast Shadow", CreateHumanCastShadowSprite(), new Vector3(0f, -2.34f, 0f), OnePixelScale(3f, 4.5f), 2);
        viewerCastShadowRenderer.color = new Color(0f, 0f, 0f, 0f);
        CreateSpriteObject("Viewer Shadow", CreateEllipseSprite(new Color(0f, 0f, 0f, 0.42f)), new Vector3(0f, -1.96f, 0f), OnePixelScale(1.7f, 0.75f), 0);

        SpriteRenderer tvBody = CreateSpriteObject("TV Body", CreateTvBodySprite(), new Vector3(0f, 2.08f, 0f), OnePixelScale(3.5f, 2.25f), 4);
        screenRenderer = CreateSpriteObject("TV Screen", CreateStaticScreenSprite(), new Vector3(0f, 2.09f, 0f), OnePixelScale(3f, 1.75f), 5);
        glowRenderer = CreateSpriteObject("TV Glow", CreateGlowConeSprite(), new Vector3(0f, 0.26f, 0f), OnePixelScale(9.6f, 6f), -2);
        beamRenderer = CreateSpriteObject("Pull Beam", CreateBeamSprite(), new Vector3(0f, 0.42f, 0f), OnePixelScale(6f, 8f), 7);
        fadeRenderer = CreateSpriteObject("Fade", CreateSolidSprite(new Color(0f, 0f, 0f, 1f)), Vector3.zero, OnePixelScale(16f, 10f), 100);
        fadeRenderer.color = new Color(0f, 0f, 0f, 0f);

        SetUnlit(couchShadow, screenRenderer, glowRenderer, beamRenderer, viewerCastShadowRenderer, fadeRenderer);
        Urp2DLighting.AddGlobalLight(gameObject, new Color(0.58f, 0.62f, 0.68f), 0.52f);
        tvLight = Urp2DLighting.AddPointLight(screenRenderer.gameObject, new Color(0.58f, 0.84f, 1.00f), 1.15f, 5.2f, 0.25f);
        pullLight = Urp2DLighting.AddPointLight(beamRenderer.gameObject, new Color(0.70f, 0.92f, 1.00f), 0f, 3.2f, 0.1f);
        Urp2DLighting.AddShadowCaster(couch.gameObject);
        Urp2DLighting.AddShadowCaster(tvBody.gameObject);
        Urp2DLighting.AddShadowCaster(viewerRenderer.gameObject);

        foreach (Transform child in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (child.gameObject.scene == root.scene && IsIntroArtObject(child.gameObject.name))
                child.SetParent(root.transform);
        }
    }

    private bool BindSceneReferences()
    {
        screenRenderer = FindRenderer("TV Screen");
        glowRenderer = FindRenderer("TV Glow");
        beamRenderer = FindRenderer("Pull Beam");
        fadeRenderer = FindRenderer("Fade");
        viewerRenderer = FindRenderer("Viewer");
        viewerCastShadowRenderer = FindRenderer("Viewer Cast Shadow");
        tvLight = screenRenderer == null ? null : screenRenderer.GetComponent<Light2D>();
        pullLight = beamRenderer == null ? null : beamRenderer.GetComponent<Light2D>();

        return screenRenderer != null &&
               glowRenderer != null &&
               beamRenderer != null &&
               fadeRenderer != null &&
               viewerRenderer != null &&
               viewerCastShadowRenderer != null;
    }

    private static SpriteRenderer FindRenderer(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        return obj == null ? null : obj.GetComponent<SpriteRenderer>();
    }

    private static bool IsIntroArtObject(string objectName)
    {
        return objectName is "Room Floor" or
            "Window Shadow" or
            "TV Cabinet" or
            "Couch Shadow" or
            "Couch" or
            "Viewer" or
            "Viewer Cast Shadow" or
            "Viewer Shadow" or
            "TV Body" or
            "TV Screen" or
            "TV Glow" or
            "Pull Beam" or
            "Fade";
    }

    private static SpriteRenderer CreateSpriteObject(string name, Sprite sprite, Vector3 position, Vector3 scale, int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        if (Urp2DLighting.SpriteLitMaterial != null)
            renderer.sharedMaterial = Urp2DLighting.SpriteLitMaterial;
        return renderer;
    }

    private static Vector3 OnePixelScale(float width, float height)
    {
        return new Vector3(width, height, 1f);
    }

    private static void SetUnlit(params SpriteRenderer[] renderers)
    {
        Material material = Urp2DLighting.SpriteUnlitMaterial;
        if (material == null)
            return;

        foreach (SpriteRenderer renderer in renderers)
            renderer.sharedMaterial = material;
    }

    private static Sprite CreateRoomFloorSprite()
    {
        return CreateOnePixelSprite(new Color(0.085f, 0.095f, 0.105f), 1f);
    }

    private static Sprite CreateCouchSprite()
    {
        return CreateOnePixelSprite(new Color(0.23f, 0.20f, 0.21f), 1f);
    }

    private static Sprite CreateViewerSprite()
    {
        return CreateOnePixelSprite(new Color(0.70f, 0.82f, 0.88f), 1f);
    }

    private static Sprite CreateTvBodySprite()
    {
        return CreateOnePixelSprite(new Color(0.08f, 0.09f, 0.10f), 1f);
    }

    private static Sprite CreateStaticScreenSprite()
    {
        return CreateOnePixelSprite(new Color(0.72f, 0.90f, 1.00f), 1f);
    }

    private static Sprite CreateGlowConeSprite()
    {
        return CreateOnePixelSprite(new Color(0.45f, 0.75f, 1.00f, 0.16f), 1f);
    }

    private static Sprite CreateBeamSprite()
    {
        return CreateOnePixelSprite(new Color(0.62f, 0.88f, 1f, 0.58f), 1f);
    }

    private static Sprite CreateHumanCastShadowSprite()
    {
        return CreateOnePixelSprite(new Color(0f, 0f, 0f, 0.46f), 1f);
    }

    private static Sprite CreateSoftRectSprite(Color color)
    {
        return CreateOnePixelSprite(color, 1f);
    }

    private static Sprite CreateEllipseSprite(Color color)
    {
        return CreateOnePixelSprite(color, 1f);
    }

    private static Sprite CreateRectSprite(Color fill)
    {
        return CreateOnePixelSprite(fill, 1f);
    }

    private static Sprite CreateSolidSprite(Color color)
    {
        return CreateOnePixelSprite(color, 1f);
    }

    private static Sprite CreateOnePixelSprite(Color color, float pixelsPerUnit)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

#if UNITY_EDITOR
    public void BakeSceneForEditor()
    {
        DestroySceneObject("Intro Art");
        foreach (Light2D light in GetComponents<Light2D>())
            DestroyImmediate(light);

        SetupCamera();
        BuildScene();
        PersistGeneratedSpritesForEditor();
        BindSceneReferences();
        EditorUtility.SetDirty(this);
    }

    private static void DestroySceneObject(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
            DestroyImmediate(obj);
    }

    private static void PersistGeneratedSpritesForEditor()
    {
        const string folder = "Assets/Generated/Intro/Sprites";
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude))
        {
            if (renderer.sprite != null && !AssetDatabase.Contains(renderer.sprite))
                renderer.sprite = PersistSpriteForEditor(folder, renderer.gameObject.name, renderer.sprite);
        }
    }

    private static Sprite PersistSpriteForEditor(string folder, string assetName, Sprite sprite)
    {
        Texture2D texture = CopySpriteTexture(sprite);
        Texture2D importedTexture = PersistTextureForEditor(folder, assetName, texture);
        DestroyImmediate(texture);

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

        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
               Sprite.Create(importedTexture, new Rect(0, 0, importedTexture.width, importedTexture.height), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
    }

    private static Texture2D PersistTextureForEditor(string folder, string assetName, Texture2D texture)
    {
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
#endif

    private static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.020f, 0.024f, 0.030f);
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }
}
