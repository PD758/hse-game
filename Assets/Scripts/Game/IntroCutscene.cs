using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class IntroCutscene : MonoBehaviour
{
    private const float Duration = 9.5f;
    private const int IntroAtlasColumns = 4;
    private const int IntroAtlasRows = 8;
    private const float IntroAtlasPixelsPerUnit = 64f;

    public Texture2D IntroAtlas;

    [SerializeField] private SpriteRenderer screenRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private SpriteRenderer beamRenderer;
    [SerializeField] private SpriteRenderer fadeRenderer;
    [SerializeField] private SpriteRenderer viewerRenderer;
    [SerializeField] private SpriteRenderer viewerCastShadowRenderer;
    [SerializeField] private Light2D tvLight;
    [SerializeField] private Light2D pullLight;
    [SerializeField] private Texture2D hudTexture;
    private Volume postProcessVolume;
    private VolumeProfile postProcessProfile;
    private float startedAt;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
        EnsurePostProcessing();
        NarrativeRunState.Reset();
        if (!BindSceneReferences())
        {
            Debug.LogError("Intro scene is not baked. Run Rogue > Bootstrap All Scenes before entering Play Mode.");
            enabled = false;
            return;
        }

        EnsureLighting();
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
        EnsureHudTexture();
        float t = Mathf.Clamp01((Time.time - startedAt) / Duration);
        string thought = ThoughtLine(t);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = PixelGui.ScaledMatrix;
        Vector2 guiSize = PixelGui.LogicalSize;
        float screenWidth = guiSize.x;
        float screenHeight = guiSize.y;

        if (!string.IsNullOrEmpty(thought))
        {
            float width = Mathf.Min(760f, screenWidth - 40f);
            var panel = new Rect((screenWidth - width) * 0.5f, 22f, width, 86f);
            GUI.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 8f, 1f)));
            GUI.DrawTexture(panel, hudTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = screenWidth < 760 ? 15 : 18,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.90f, 0.94f) },
            };
            PixelGui.Apply(style);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 22f), thought, style);
            GUI.color = Color.white;
        }

        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = screenWidth < 760 ? 12 : 14,
            normal = { textColor = new Color(0.62f, 0.66f, 0.72f, 0.82f) },
        };
        PixelGui.Apply(hintStyle);
        GUI.Label(new Rect(12, screenHeight - 36, screenWidth - 24, 24), "Space/Enter: пропустить | Esc: меню", hintStyle);
        GUI.matrix = previousMatrix;
    }

    private static string ThoughtLine(float t)
    {
        if (t < 0.24f)
            return "Почему все делают вид, что этот канал единственный?";
        if (t < 0.48f)
            return "Почему тех, кто не смотрит, будто выносят за дверь?";
        if (t < 0.72f)
            return "Экран становится ближе, хотя комната не двигается.";
        if (t < 0.90f)
            return "сон проваливается внутрь эфира";
        return string.Empty;
    }

    private void AnimateScene()
    {
        float elapsed = Time.time - startedAt;
        float t = Mathf.Clamp01(elapsed / Duration);
        float pulse = 0.5f + Mathf.Sin(Time.time * 18f) * 0.5f;

        screenRenderer.color = Color.Lerp(new Color(0.36f, 0.48f, 0.64f), new Color(0.82f, 0.92f, 1.00f), pulse * 0.45f + t * 0.25f);
        glowRenderer.color = new Color(0.55f, 0.82f, 1f, Mathf.Lerp(0.04f, 0.14f, t) + pulse * 0.02f);
        glowRenderer.transform.localScale = new Vector3(1f + pulse * 0.05f, 1f + t * 0.18f, 1f);
        beamRenderer.color = new Color(0.62f, 0.88f, 1f, Mathf.SmoothStep(0f, 0.12f, Mathf.Clamp01((t - 0.44f) / 0.34f)));
        float castShadow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.58f) / 0.30f));
        viewerCastShadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.42f, castShadow));
        viewerCastShadowRenderer.transform.localScale = new Vector3(1f + castShadow * 0.22f, 1f + castShadow * 0.30f, 1f);
        if (tvLight != null)
        {
            tvLight.intensity = Mathf.Lerp(1.15f, 1.75f, t) + pulse * 0.12f;
            tvLight.pointLightOuterRadius = Mathf.Lerp(5.2f, 6.8f, t);
        }

        if (pullLight != null)
            pullLight.intensity = Mathf.SmoothStep(0f, 1.55f, Mathf.Clamp01((t - 0.48f) / 0.34f));
        viewerRenderer.transform.position = Vector3.Lerp(new Vector3(0f, -1.82f, 0f), new Vector3(0f, -1.52f, 0f), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.55f) / 0.35f)));
        fadeRenderer.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.96f, Mathf.Clamp01((t - 0.72f) / 0.28f)));

        Camera camera = Camera.main;
        if (camera != null)
            camera.orthographicSize = Mathf.Lerp(5.4f, 4.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.55f)));
    }

    private void BuildScene()
    {
        ThrowIfPlayingBake("BuildScene");
        new GameObject("Intro Art");
        CreateSpriteObject("Room Floor", IntroSpriteOrFallback(0, 3, "intro_floor", CreateRoomFloorSprite()), Vector3.zero, new Vector3(2.5f, 1.6f, 1f), -10);
        CreateSpriteObject("Window Shadow", IntroSpriteOrFallback(3, 0, "intro_window_shadow", CreateSoftRectSprite(128, 32, new Color(0.015f, 0.018f, 0.022f, 0.46f))), new Vector3(-3.8f, 2.3f, 0f), new Vector3(2.0f, 1f, 1f), -8);
        SpriteRenderer tvCabinet = CreateSpriteObject("TV Cabinet", IntroSpriteOrFallback(1, 0, "intro_tv_cabinet", CreateRectSprite(96, 26, new Color(0.13f, 0.115f, 0.108f), new Color(0.060f, 0.054f, 0.052f))), new Vector3(0f, 1.82f, 0f), new Vector3(1.25f, 1f, 1f), -3);

        SpriteRenderer couchShadow = CreateSpriteObject("Couch Shadow", IntroSpriteOrFallback(0, 1, "intro_couch_shadow", CreateEllipseSprite(160, 48, new Color(0f, 0f, 0f, 0.45f))), new Vector3(0f, -2.18f, 0f), Vector3.one, -4);
        couchShadow.transform.localScale = new Vector3(1.4f, 0.9f, 1f);
        SpriteRenderer couch = CreateSpriteObject("Couch", IntroSpriteOrFallback(0, 0, "intro_couch", CreateCouchSprite()), new Vector3(0f, -2.0f, 0f), Vector3.one, 1);
        viewerRenderer = CreateSpriteObject("Viewer", IntroSpriteOrFallback(2, 0, "intro_viewer_seated", CreateViewerSprite()), new Vector3(0f, -1.82f, 0f), Vector3.one, 5);
        viewerCastShadowRenderer = CreateSpriteObject("Viewer Cast Shadow", CreateHumanCastShadowSprite(), new Vector3(0f, -2.34f, 0f), Vector3.one, 2);
        viewerCastShadowRenderer.color = new Color(0f, 0f, 0f, 0f);
        CreateSpriteObject("Viewer Shadow", CreateEllipseSprite(54, 24, new Color(0f, 0f, 0f, 0.42f)), new Vector3(0f, -1.96f, 0f), Vector3.one, 0);

        SpriteRenderer tvBody = CreateSpriteObject("TV Body", IntroAtlas != null ? null : CreateTvBodySprite(), new Vector3(0f, 2.08f, 0f), Vector3.one, 4);
        screenRenderer = CreateSpriteObject("TV Screen", IntroSpriteOrFallback(1, 1, "intro_tv_screen", CreateStaticScreenSprite()), new Vector3(0f, 2.09f, 0f), Vector3.one, 5);
        glowRenderer = CreateSpriteObject("TV Glow", CreateGlowConeSprite(), new Vector3(0f, 0.26f, 0f), new Vector3(1.2f, 1f, 1f), -2);
        beamRenderer = CreateSpriteObject("Pull Beam", CreateBeamSprite(), new Vector3(0f, 0.42f, 0f), Vector3.one, 7);
        fadeRenderer = CreateSpriteObject("Fade", CreateSolidSprite(new Color(0f, 0f, 0f, 1f), 16, 10), Vector3.zero, Vector3.one, 100);
        fadeRenderer.color = new Color(0f, 0f, 0f, 0f);

        SetUnlit(couchShadow, screenRenderer, glowRenderer, beamRenderer, viewerCastShadowRenderer, fadeRenderer);
        Urp2DLighting.AddGlobalLight(gameObject, new Color(0.58f, 0.62f, 0.68f), 0.52f);
        tvLight = Urp2DLighting.AddPointLight(screenRenderer.gameObject, new Color(0.58f, 0.84f, 1.00f), 1.15f, 5.2f, 0.25f);
        pullLight = Urp2DLighting.AddPointLight(beamRenderer.gameObject, new Color(0.70f, 0.92f, 1.00f), 0f, 3.2f, 0.1f);
        EnsureLighting();
        Urp2DLighting.AddShadowCaster(couch.gameObject);
        if (IntroAtlas != null)
            Urp2DLighting.AddShadowCaster(tvCabinet.gameObject);
        else
            Urp2DLighting.AddShadowCaster(tvBody.gameObject);
        Urp2DLighting.AddShadowCaster(viewerRenderer.gameObject);

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
               viewerCastShadowRenderer != null &&
               hudTexture != null;
    }

    private void EnsureLighting()
    {
        bool hasGlobalLight = false;
        foreach (Light2D light in GetComponents<Light2D>())
        {
            if (light.lightType == Light2D.LightType.Global)
            {
                light.color = new Color(0.58f, 0.62f, 0.68f);
                light.intensity = 0.52f;
                hasGlobalLight = true;
            }
        }
        if (!hasGlobalLight)
            Urp2DLighting.AddGlobalLight(gameObject, new Color(0.58f, 0.62f, 0.68f), 0.52f);

        if (tvLight != null)
            Urp2DLighting.ConfigurePointLightShadows(tvLight, 0.72f, 0.48f, 0.64f);
        if (pullLight != null)
            Urp2DLighting.ConfigurePointLightShadows(pullLight, 0.45f, 0.62f, 0.70f);
    }

    private static SpriteRenderer FindRenderer(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        return obj == null ? null : obj.GetComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR
    public void BakeSceneForEditor()
    {
        DestroySceneObject("Intro Art");
        DestroySceneObject("Post Processing");
        foreach (Light2D light in GetComponents<Light2D>())
            DestroyImmediate(light);

        SetupCamera();
        EnsurePostProcessing();
        BuildScene();
        EnsureHudTexture();
        PersistGeneratedSpritesForEditor();
        hudTexture = PersistTextureForEditor("Assets/Generated/Intro/HUD", "intro_hud_panel", hudTexture);
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
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? Sprite.Create(importedTexture, new Rect(0, 0, importedTexture.width, importedTexture.height), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
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
            importer.filterMode = FilterMode.Bilinear;
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
            filterMode = FilterMode.Bilinear,
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

    private SpriteRenderer CreateSpriteObject(string name, Sprite sprite, Vector3 position, Vector3 scale, int sortingOrder)
    {
        ThrowIfPlayingBake("CreateSpriteObject");
        var obj = new GameObject(name);
        Transform root = GameObject.Find("Intro Art")?.transform;
        if (root != null)
            obj.transform.SetParent(root);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        if (Urp2DLighting.SpriteLitMaterial != null)
            renderer.sharedMaterial = Urp2DLighting.SpriteLitMaterial;
        return renderer;
    }

    private static void SetUnlit(params SpriteRenderer[] renderers)
    {
        Material material = Urp2DLighting.SpriteUnlitMaterial;
        if (material == null)
            return;

        foreach (SpriteRenderer renderer in renderers)
            renderer.sharedMaterial = material;
    }

    private Sprite IntroSpriteOrFallback(int row, int column, string spriteName, Sprite fallback)
    {
        if (IntroAtlas == null)
            return fallback;

        try
        {
            return CreateIntroAtlasSprite(row, column, spriteName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Intro atlas cell {column},{row} could not be sliced, using fallback {spriteName}: {ex.Message}");
            return fallback;
        }
    }

    private Sprite CreateIntroAtlasSprite(int row, int column, string spriteName)
    {
        ThrowIfPlayingBake("CreateIntroAtlasSprite");
        if (row < 0 || row >= IntroAtlasRows || column < 0 || column >= IntroAtlasColumns)
            throw new InvalidOperationException($"Intro atlas cell {column},{row} is outside 4x8 grid.");

        int cellWidth = IntroAtlas.width / IntroAtlasColumns;
        int cellHeight = IntroAtlas.height / IntroAtlasRows;
        int sourceX = column * cellWidth;
        int sourceY = IntroAtlas.height - (row + 1) * cellHeight;
        Color[] pixels = IntroAtlas.GetPixels(sourceX, sourceY, cellWidth, cellHeight);
        var texture = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            name = spriteName,
        };

        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % cellWidth;
            int y = i / cellWidth;
            bool atlasEdge = x <= 1 || y <= 1 || x >= cellWidth - 2 || y >= cellHeight - 2;
            if (atlasEdge || IsChromaGreen(pixels[i]))
                pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, cellWidth, cellHeight), new Vector2(0.5f, 0.5f), IntroAtlasPixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    private static bool IsChromaGreen(Color color)
    {
        float maxOther = Mathf.Max(color.r, color.b);
        bool isStandardGreen = color.g > 0.22f && color.g - maxOther > 0.10f && color.r < 0.50f && color.b < 0.50f;
        bool isLimeGreen = color.g > 0.50f && color.g - color.b > 0.30f && color.r > 0.50f && color.r < 0.85f && color.b < 0.50f;
        return isStandardGreen || isLimeGreen;
    }

    private static Sprite CreateRoomFloorSprite()
    {
        ThrowIfPlayingBake("CreateRoomFloorSprite");
        const int width = 320;
        const int height = 208;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Color baseColor = new Color(0.088f, 0.094f, 0.102f);
        Color seamColor = new Color(0.128f, 0.138f, 0.150f);
        Color dustColor = new Color(0.112f, 0.120f, 0.132f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool seam = x % 32 == 0 || y % 32 == 0;
                bool dust = (x * 17 + y * 29) % 211 == 0;
                texture.SetPixel(x, y, seam ? seamColor : dust ? dustColor : baseColor);
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateCouchSprite()
    {
        ThrowIfPlayingBake("CreateCouchSprite");
        var texture = new Texture2D(160, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawRect(texture, 10, 16, 140, 34, new Color(0.230f, 0.200f, 0.214f), true);
        DrawRect(texture, 4, 20, 20, 38, new Color(0.165f, 0.145f, 0.158f), true);
        DrawRect(texture, 136, 20, 20, 38, new Color(0.165f, 0.145f, 0.158f), true);
        DrawRect(texture, 14, 12, 132, 10, new Color(0.285f, 0.250f, 0.265f), true);
        DrawRect(texture, 11, 16, 138, 34, new Color(0.085f, 0.076f, 0.084f), false);
        DrawLine(texture, 80, 18, 80, 48, new Color(0.120f, 0.108f, 0.116f));
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateViewerSprite()
    {
        ThrowIfPlayingBake("CreateViewerSprite");
        var texture = new Texture2D(48, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        Color hair = new Color(0.015f, 0.014f, 0.015f);
        Color face = new Color(0.58f, 0.67f, 0.74f);
        Color shirt = new Color(0.100f, 0.125f, 0.145f);
        DrawRect(texture, 16, 8, 16, 12, hair, true);
        DrawRect(texture, 17, 16, 14, 10, face, true);
        DrawRect(texture, 14, 28, 20, 22, shirt, true);
        DrawRect(texture, 8, 34, 10, 18, shirt, true);
        DrawRect(texture, 30, 34, 10, 18, shirt, true);
        DrawRect(texture, 17, 50, 6, 10, new Color(0.030f, 0.035f, 0.040f), true);
        DrawRect(texture, 25, 50, 6, 10, new Color(0.030f, 0.035f, 0.040f), true);
        SetSafe(texture, 20, 20, new Color(0.82f, 0.92f, 1f));
        SetSafe(texture, 27, 20, new Color(0.82f, 0.92f, 1f));
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateTvBodySprite()
    {
        ThrowIfPlayingBake("CreateTvBodySprite");
        var texture = new Texture2D(112, 72, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawRect(texture, 10, 10, 92, 52, new Color(0.075f, 0.082f, 0.090f), true);
        DrawRect(texture, 16, 16, 70, 38, new Color(0.018f, 0.024f, 0.030f), true);
        DrawRect(texture, 91, 20, 6, 6, new Color(0.25f, 0.27f, 0.28f), true);
        DrawRect(texture, 91, 34, 6, 6, new Color(0.18f, 0.20f, 0.21f), true);
        DrawRect(texture, 10, 10, 92, 52, new Color(0.020f, 0.023f, 0.027f), false);
        DrawLine(texture, 32, 8, 22, 0, new Color(0.18f, 0.20f, 0.22f));
        DrawLine(texture, 78, 8, 90, 0, new Color(0.18f, 0.20f, 0.22f));
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateStaticScreenSprite()
    {
        ThrowIfPlayingBake("CreateStaticScreenSprite");
        const int width = 70;
        const int height = 38;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool band = y % 9 == 0;
                bool speck = (x * 31 + y * 17) % 23 < 8;
                Color color = band ? new Color(0.56f, 0.66f, 0.78f) : speck ? new Color(0.25f, 0.34f, 0.48f) : new Color(0.07f, 0.11f, 0.16f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateGlowConeSprite()
    {
        ThrowIfPlayingBake("CreateGlowConeSprite");
        const int width = 192;
        const int height = 176;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                float center = Mathf.Abs(x - width * 0.5f) / (width * Mathf.Lerp(0.18f, 0.62f, 1f - ny));
                float alpha = Mathf.Clamp01(1f - center) * Mathf.Clamp01(1f - ny * 0.84f) * 0.22f;
                texture.SetPixel(x, y, new Color(0.45f, 0.78f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.72f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateBeamSprite()
    {
        ThrowIfPlayingBake("CreateBeamSprite");
        const int width = 96;
        const int height = 160;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float nx = Mathf.Abs(x - width * 0.5f) / (width * 0.42f);
                float ny = y / (float)(height - 1);
                bool scan = y % 9 == 0;
                float alpha = Mathf.Clamp01(1f - nx) * Mathf.SmoothStep(0f, 1f, ny) * (scan ? 0.08f : 0.04f);
                texture.SetPixel(x, y, new Color(0.72f, 0.90f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.82f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateHumanCastShadowSprite()
    {
        ThrowIfPlayingBake("CreateHumanCastShadowSprite");
        const int width = 92;
        const int height = 138;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float nx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                float ny = y / (float)(height - 1);
                float torso = Mathf.Clamp01(1f - (nx * nx * 4.2f + Mathf.Pow(ny - 0.42f, 2f) * 4.8f));
                float head = Mathf.Clamp01(1f - (nx * nx * 8.0f + Mathf.Pow(ny - 0.78f, 2f) * 20.0f));
                float shoulders = Mathf.Clamp01(1f - (nx * nx * 2.2f + Mathf.Pow(ny - 0.58f, 2f) * 18.0f));
                float fade = Mathf.SmoothStep(0f, 1f, ny) * Mathf.SmoothStep(1f, 0f, Mathf.Abs(nx) * 0.82f);
                float alpha = Mathf.Max(Mathf.Max(torso, head), shoulders) * fade * 0.82f;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.92f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateSoftRectSprite(int width, int height, Color color)
    {
        ThrowIfPlayingBake("CreateSoftRectSprite");
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float edge = Mathf.Min(Mathf.Min(x, width - 1 - x) / 10f, Mathf.Min(y, height - 1 - y) / 10f);
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(edge)));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateEllipseSprite(int width, int height, Color color)
    {
        ThrowIfPlayingBake("CreateEllipseSprite");
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float nx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                float ny = (y + 0.5f - height * 0.5f) / (height * 0.5f);
                float distance = nx * nx + ny * ny;
                float alpha = Mathf.Clamp01(1f - distance);
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateRectSprite(int width, int height, Color fill, Color edge)
    {
        ThrowIfPlayingBake("CreateRectSprite");
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Fill(texture, fill);
        DrawRect(texture, 0, 0, width, height, edge, false);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateSolidSprite(Color color, int worldWidth, int worldHeight)
    {
        ThrowIfPlayingBake("CreateSolidSprite");
        var texture = new Texture2D(worldWidth * 32, worldHeight * 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Fill(texture, color);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
    }

    private void EnsureHudTexture()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (hudTexture != null)
            return;

        hudTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        hudTexture.SetPixel(0, 0, new Color(0.025f, 0.030f, 0.038f, 0.78f));
        hudTexture.Apply();
#endif
    }

    private static void ThrowIfPlayingBake(string method)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException($"{method} is editor-bake only and must not run in Play Mode.");
    }

    private static void Fill(Texture2D texture, Color color)
    {
        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
                texture.SetPixel(x, y, color);
        }
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color, bool filled)
    {
        for (int ix = x; ix < x + width; ix++)
        {
            for (int iy = y; iy < y + height; iy++)
            {
                if (filled || ix == x || iy == y || ix == x + width - 1 || iy == y + height - 1)
                    SetSafe(texture, ix, iy, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            SetSafe(texture, x0, y0, color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && y >= 0 && x < texture.width && y < texture.height)
            texture.SetPixel(x, y, color);
    }

    private void EnsurePostProcessing()
    {
        GameObject volumeObject = GameObject.Find("Post Processing");
        if (volumeObject == null)
            volumeObject = new GameObject("Post Processing");

        postProcessVolume = volumeObject.GetComponent<Volume>();
        if (postProcessVolume == null)
            postProcessVolume = volumeObject.AddComponent<Volume>();

        postProcessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        postProcessProfile.name = "Intro Runtime Post Processing";

        Vignette vignette = postProcessProfile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.008f, 0.010f, 0.016f));
        vignette.center.Override(new Vector2(0.5f, 0.5f));
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.58f);
        vignette.rounded.Override(true);

        ColorAdjustments color = postProcessProfile.Add<ColorAdjustments>(true);
        color.postExposure.Override(-0.04f);
        color.contrast.Override(12f);
        color.saturation.Override(-6f);
        color.colorFilter.Override(new Color(0.88f, 0.95f, 1f));

        ChromaticAberration chromaticAberration = postProcessProfile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.Override(0.035f);

        FilmGrain filmGrain = postProcessProfile.Add<FilmGrain>(true);
        filmGrain.type.Override(FilmGrainLookup.Thin1);
        filmGrain.intensity.Override(0.12f);
        filmGrain.response.Override(0.78f);

        LensDistortion lensDistortion = postProcessProfile.Add<LensDistortion>(true);
        lensDistortion.intensity.Override(-0.025f);
        lensDistortion.center.Override(new Vector2(0.5f, 0.5f));
        lensDistortion.scale.Override(1.01f);

        postProcessVolume.isGlobal = true;
        postProcessVolume.priority = 0f;
        postProcessVolume.weight = 1f;
        postProcessVolume.sharedProfile = postProcessProfile;
    }

    private static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Intro scene is missing a baked Main Camera.");
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.020f, 0.024f, 0.030f);
        camera.allowMSAA = true;
        camera.orthographic = true;
        camera.orthographicSize = 5.4f;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.dithering = true;
    }
}
