#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ProjectBootstrap
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string IntroScenePath = "Assets/Scenes/Intro.unity";
    private const string PrototypeScenePath = "Assets/Scenes/Prototype.unity";
    private delegate void SceneBuildAction();

    [MenuItem("Rogue/Bootstrap All Scenes")]
    public static void CreateAllScenes()
    {
        ConfigureUrp2D();
        ConfigureTextureImports();
        CreateMainMenuScene();
        CreateIntroScene();
        CreatePrototypeScene();
        SaveProjectSceneSettings();
    }

    [MenuItem("Rogue/Force Rebuild All Scenes")]
    public static void ForceRebuildAllScenes()
    {
        ConfigureUrp2D();
        ConfigureTextureImports();
        RebuildMainMenuScene();
        RebuildIntroScene();
        RebuildPrototypeScene();
        SaveProjectSceneSettings();
    }

    [MenuItem("Rogue/Use Main Menu As Play Start")]
    public static void UseMainMenuAsPlayStart()
    {
        SetPlayModeStartScene(MainMenuScenePath);
    }

    [MenuItem("Rogue/Configure URP 2D")]
    public static void ConfigureUrp2D()
    {
        const string settingsRoot = "Assets/Settings";
        const string urpRoot = "Assets/Settings/URP2D";
        EnsureFolder("Assets", "Settings");
        EnsureFolder(settingsRoot, "URP2D");

        var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>($"{urpRoot}/TVRoguelike2DRenderer.asset");
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
            AssetDatabase.CreateAsset(rendererData, $"{urpRoot}/TVRoguelike2DRenderer.asset");
        }
        EnsureRendererPostProcessing(rendererData);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{urpRoot}/TVRoguelikeURP.asset");
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, $"{urpRoot}/TVRoguelikeURP.asset");
        }

        CreateMaterialIfMissing($"{urpRoot}/SpriteLit.mat", "Universal Render Pipeline/2D/Sprite-Lit-Default");
        CreateMaterialIfMissing($"{urpRoot}/SpriteUnlit.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default");

        GraphicsSettings.defaultRenderPipeline = pipeline;
        ApplyPipelineToAllQualityLevels(pipeline);
        QualitySettings.antiAliasing = 0;
        AssetDatabase.SaveAssets();
    }

    private static void EnsureRendererPostProcessing(Renderer2DData rendererData)
    {
        if (rendererData == null)
            return;

        var postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        if (postProcessData == null)
        {
            Debug.LogError("URP default PostProcessData asset was not found; 2D post processing will stay disabled.");
            return;
        }

        var serializedRenderer = new SerializedObject(rendererData);
        SerializedProperty property = serializedRenderer.FindProperty("m_PostProcessData");
        if (property == null)
        {
            Debug.LogError("Renderer2DData.m_PostProcessData was not found; 2D post processing will stay disabled.");
            return;
        }

        if (property.objectReferenceValue == postProcessData)
            return;

        property.objectReferenceValue = postProcessData;
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rendererData);
    }

    [MenuItem("Rogue/Bootstrap Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        EnsureSceneExists(MainMenuScenePath, RebuildMainMenuScene);
    }

    [MenuItem("Rogue/Force Rebuild/Main Menu Scene")]
    public static void RebuildMainMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
        camera.allowMSAA = true;

        var gameObject = new GameObject("Main Menu");
        MainMenu menu = gameObject.AddComponent<MainMenu>();
        menu.BakeSceneForEditor();

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
    }

    [MenuItem("Rogue/Bootstrap Intro Scene")]
    public static void CreateIntroScene()
    {
        EnsureSceneExists(IntroScenePath, RebuildIntroScene);
    }

    [MenuItem("Rogue/Force Rebuild/Intro Scene")]
    public static void RebuildIntroScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.020f, 0.024f, 0.030f);
        camera.allowMSAA = true;

        var gameObject = new GameObject("Intro Cutscene");
        IntroCutscene intro = gameObject.AddComponent<IntroCutscene>();
        intro.IntroAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/intro_wide_1024.jpg");
        intro.BakeSceneForEditor();

        EditorSceneManager.SaveScene(scene, IntroScenePath);
    }

    [MenuItem("Rogue/Bootstrap Prototype Scene")]
    public static void CreatePrototypeScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeScenePath) == null)
        {
            RebuildPrototypeScene();
            return;
        }

        RefreshPrototypeScene();
    }

    [MenuItem("Rogue/Force Rebuild/Prototype Scene")]
    public static void RebuildPrototypeScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.070f, 0.076f, 0.086f);
        camera.allowMSAA = true;
        camera.orthographicSize = 6.0f;
        camera.transform.position = new Vector3(8f, 10f, -10f);

        var gameObject = new GameObject("Prototype Game");
        PrototypeGame game = gameObject.AddComponent<PrototypeGame>();
        ConfigurePrototypeGame(game);
        game.BakeSceneForEditor();

        EditorSceneManager.SaveScene(scene, PrototypeScenePath);
    }

    private static void RefreshPrototypeScene()
    {
        Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        PrototypeGame game = UnityEngine.Object.FindAnyObjectByType<PrototypeGame>(FindObjectsInactive.Include);
        if (game == null)
        {
            RebuildPrototypeScene();
            return;
        }

        ConfigurePrototypeGame(game);
        game.BakeSceneForEditor();
        EditorSceneManager.SaveScene(scene, PrototypeScenePath);
    }

    private static void ConfigurePrototypeGame(PrototypeGame game)
    {
        game.CharacterAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/characters_1024.jpg");
        game.BossAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/boss_512.jpg");
        game.EnemyAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/enemies_1024.png");
        game.EnvironmentAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/environment_v2.png");
        game.WallAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/environment_2_1024.jpg");
        game.HudAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/hud_1024.jpg");
        game.LevelAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Levels/prototype_01.json");
        game.LevelAssets = LoadLevelAssets();
        game.StartingLevelId = "prototype_01";
    }

    private static TextAsset[] LoadLevelAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Levels" });
        var assets = new TextAsset[guids.Length];
        for (int i = 0; i < guids.Length; i++)
            assets[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
        return assets;
    }

    private static void EnsureSceneExists(string scenePath, SceneBuildAction buildScene)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            return;

        buildScene();
    }

    private static void SaveProjectSceneSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(IntroScenePath, true),
            new EditorBuildSettingsScene(PrototypeScenePath, true),
        };
        SetPlayModeStartScene(MainMenuScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void SetPlayModeStartScene(string scenePath)
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (sceneAsset == null)
        {
            Debug.LogWarning($"Play Mode start scene was not set because {scenePath} was not found.");
            return;
        }

        if (EditorSceneManager.playModeStartScene != sceneAsset)
            EditorSceneManager.playModeStartScene = sceneAsset;
    }

    private static void ConfigureTextureImports()
    {
        ConfigureReadableSmoothTexture("Assets/Atlases/characters_1024.jpg");
        ConfigureReadableSmoothTexture("Assets/Atlases/boss_512.jpg");
        ConfigureReadableSmoothTexture("Assets/Atlases/enemies_1024.png");
        ConfigureReadableSmoothTexture("Assets/Atlases/environment_2_1024.jpg");
        ConfigureReadableSmoothTexture("Assets/Atlases/environment_v2.png");
        ConfigureReadableSmoothTexture("Assets/Atlases/hud_1024.jpg");
        ConfigureReadableSmoothTexture("Assets/Atlases/intro_wide_1024.jpg");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void CreateMaterialIfMissing(string path, string shaderName)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            return;

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"Could not find shader {shaderName}; material {path} was not created.");
            return;
        }

        AssetDatabase.CreateAsset(new Material(shader), path);
    }

    private static void ApplyPipelineToAllQualityLevels(RenderPipelineAsset pipeline)
    {
        int current = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(current, false);
    }

    private static void ConfigureReadableSmoothTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
#endif
