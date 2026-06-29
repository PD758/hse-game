#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectBootstrap
{
    [MenuItem("Rogue/Bootstrap All Scenes")]
    public static void CreateAllScenes()
    {
        ConfigureTextureImports();
        CreateMainMenuScene();
        CreateIntroScene();
        CreatePrototypeScene();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Intro.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Prototype.unity", true),
        };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Rogue/Bootstrap Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f);

        var gameObject = new GameObject("Main Menu");
        gameObject.AddComponent<MainMenu>();

        const string scenePath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    [MenuItem("Rogue/Bootstrap Intro Scene")]
    public static void CreateIntroScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        var gameObject = new GameObject("Intro Cutscene");
        gameObject.AddComponent<IntroCutscene>();

        const string scenePath = "Assets/Scenes/Intro.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    [MenuItem("Rogue/Bootstrap Prototype Scene")]
    public static void CreatePrototypeScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
        camera.transform.position = new Vector3(12f, 8f, -10f);

        var gameObject = new GameObject("Prototype Game");
        PrototypeGame game = gameObject.AddComponent<PrototypeGame>();
        game.SpriteAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Atlases/1.jpg");

        const string scenePath = "Assets/Scenes/Prototype.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void ConfigureTextureImports()
    {
        ConfigureReadablePointTexture("Assets/Atlases/1.jpg");
    }

    private static void ConfigureReadablePointTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
#endif
