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
        CreateMainMenuScene();
        CreatePrototypeScene();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
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
        gameObject.AddComponent<PrototypeGame>();

        const string scenePath = "Assets/Scenes/Prototype.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
    }
}
#endif
