using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class MainMenu : MonoBehaviour
{
    private enum MenuMode
    {
        Story,
        Endless,
    }

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle labelStyle;
    private GUIStyle hintStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallButtonStyle;
    private GUIStyle modeTitleStyle;
    private GUIStyle modeMetaStyle;
    [SerializeField] private Texture2D panelTexture;
    [SerializeField] private Texture2D backgroundTexture;
    public TextAsset[] LevelAssets = Array.Empty<TextAsset>();
    private Texture2D whiteTexture;
    private MenuMode selectedMode = MenuMode.Story;
    private int levelShortcutDigit = -1;
    private string menuMessage = string.Empty;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
        if (!EndlessRunState.StoryCompleted)
            selectedMode = MenuMode.Story;
        if (Application.isPlaying && (panelTexture == null || backgroundTexture == null))
        {
            Debug.LogError("Main menu scene is not baked. Run Rogue > Bootstrap All Scenes before entering Play Mode.");
            enabled = false;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!keyboard.lKey.isPressed)
        {
            levelShortcutDigit = -1;
            return;
        }

        int digit = PressedDigit(keyboard);
        if (digit >= 0)
            levelShortcutDigit = digit;

        if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && levelShortcutDigit >= 0)
            TryStartStoryAtLevel($"prototype_0{levelShortcutDigit}");
    }

    private void OnGUI()
    {
        Vector2 guiSize = PixelGui.LogicalSize;
        float screenWidth = guiSize.x;
        float screenHeight = guiSize.y;
        EnsureStyles(screenWidth);
        if (panelTexture == null || backgroundTexture == null || titleStyle == null || buttonStyle == null)
            return;

        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = PixelGui.ScaledMatrix;
        DrawBackground(screenWidth, screenHeight);

        DrawMenu(screenWidth, screenHeight);
        GUI.matrix = previousMatrix;
    }

    private void StartGame()
    {
        if (selectedMode == MenuMode.Endless)
        {
            if (!EndlessRunState.StoryCompleted)
            {
                selectedMode = MenuMode.Story;
                menuMessage = "Бесконечный режим откроется после первого прохождения.";
                return;
            }

            EndlessRunState.StartRun();
            GameMusic.Play();
            SceneManager.LoadScene("Prototype");
            return;
        }

        EndlessRunState.StartStory();
        GameMusic.Stop();
        SceneManager.LoadScene("Intro");
    }

    private void TryStartStoryAtLevel(string levelId)
    {
        string normalized = LevelAssetResolver.NormalizeLevelId(levelId);
        if (LevelAssetResolver.Resolve(normalized, null, LevelAssets, null) == null)
        {
            Debug.Log($"Level shortcut ignored: '{normalized}' was not found.");
            return;
        }

        EndlessRunState.StartStory(normalized);
        GameMusic.Play();
        SceneManager.LoadScene("Prototype");
    }

    private static int PressedDigit(Keyboard keyboard)
    {
        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
            return 0;
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            return 1;
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            return 2;
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            return 3;
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            return 4;
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
            return 5;
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame)
            return 6;
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame)
            return 7;
        if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame)
            return 8;
        if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame)
            return 9;

        return -1;
    }

    private void EnsureStyles(float screenWidth)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EnsureMenuTexturesForEditor();
#endif

        if (panelTexture == null || backgroundTexture == null)
            return;

        EnsureWhiteTexture();

        titleStyle ??= new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = screenWidth < 760 ? 30 : 48;
        titleStyle.normal.textColor = Color.white;
        PixelGui.Apply(titleStyle);

        subtitleStyle ??= new GUIStyle(GUI.skin.label);
        subtitleStyle.fontSize = screenWidth < 760 ? 13 : 16;
        subtitleStyle.wordWrap = true;
        subtitleStyle.normal.textColor = new Color(0.76f, 0.84f, 0.90f);
        PixelGui.Apply(subtitleStyle);

        labelStyle ??= new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = screenWidth < 760 ? 14 : 17;
        labelStyle.normal.textColor = new Color(0.86f, 0.88f, 0.90f);
        PixelGui.Apply(labelStyle);

        hintStyle ??= new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = screenWidth < 760 ? 11 : 13;
        hintStyle.wordWrap = true;
        hintStyle.normal.textColor = new Color(0.67f, 0.74f, 0.78f);
        PixelGui.Apply(hintStyle);

        buttonStyle ??= new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = screenWidth < 760 ? 15 : 18;
        buttonStyle.alignment = TextAnchor.MiddleCenter;
        PixelGui.Apply(buttonStyle);

        smallButtonStyle ??= new GUIStyle(GUI.skin.button);
        smallButtonStyle.fontSize = screenWidth < 760 ? 12 : 14;
        smallButtonStyle.alignment = TextAnchor.MiddleCenter;
        PixelGui.Apply(smallButtonStyle);

        modeTitleStyle ??= new GUIStyle(GUI.skin.label);
        modeTitleStyle.fontSize = screenWidth < 760 ? 18 : 23;
        modeTitleStyle.normal.textColor = Color.white;
        PixelGui.Apply(modeTitleStyle);

        modeMetaStyle ??= new GUIStyle(GUI.skin.label);
        modeMetaStyle.fontSize = screenWidth < 760 ? 10 : 12;
        modeMetaStyle.normal.textColor = new Color(0.56f, 0.78f, 0.86f);
        PixelGui.Apply(modeMetaStyle);

    }

#if UNITY_EDITOR
    private void EnsureMenuTexturesForEditor()
    {
        if (panelTexture == null)
        {
            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.97f));
            panelTexture.Apply();
        }

        if (backgroundTexture == null)
        {
            const int size = 32;
            backgroundTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
            };

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool line = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool mote = (x * 7 + y * 11) % 31 == 0;
                    Color color = line ? new Color(0.11f, 0.13f, 0.15f) : mote ? new Color(0.12f, 0.18f, 0.20f) : new Color(0.04f, 0.05f, 0.06f);
                    backgroundTexture.SetPixel(x, y, color);
                }
            }

            backgroundTexture.Apply();
        }
    }
#endif

    private void DrawBackground(float screenWidth, float screenHeight)
    {
        for (int x = 0; x < screenWidth; x += backgroundTexture.width)
        {
            for (int y = 0; y < screenHeight; y += backgroundTexture.height)
                GUI.DrawTexture(new Rect(x, y, backgroundTexture.width, backgroundTexture.height), backgroundTexture);
        }

        DrawRect(new Rect(0f, 0f, screenWidth, screenHeight), new Color(0.005f, 0.008f, 0.012f, GameLightingSettings.MenuDarkOverlayAlpha));

        float scanlineAlpha = 0.055f;
        for (float y = 0f; y < screenHeight; y += 8f)
            DrawRect(new Rect(0f, y, screenWidth, 1f), new Color(0.70f, 0.92f, 1f, scanlineAlpha));
    }

    private void DrawMenu(float screenWidth, float screenHeight)
    {
        bool compact = screenWidth < 820f;
        float margin = compact ? 16f : 28f;
        float panelWidth = Mathf.Min(compact ? 620f : 980f, screenWidth - margin * 2f);
        float panelHeight = Mathf.Min(compact ? 650f : 620f, screenHeight - margin * 2f);
        Rect panel = PixelRect(new Rect((screenWidth - panelWidth) * 0.5f, (screenHeight - panelHeight) * 0.5f, panelWidth, panelHeight));

        DrawPanel(panel, new Color(0.018f, 0.024f, 0.032f, 0.94f), new Color(0.48f, 0.68f, 0.74f, 0.38f));

        Rect headerRect = new Rect(panel.x + 28f, panel.y + 24f, panel.width - 56f, compact ? 118f : 128f);
        GUI.Label(new Rect(headerRect.x, headerRect.y, headerRect.width, 56f), "Adether.", titleStyle);

        float cardsTop = headerRect.y + (compact ? 74f : 88f);
        float controlsTop = panel.yMax - (compact ? 132f : 120f);
        float cardsHeight = controlsTop - cardsTop - 18f;
        Rect storyRect;
        Rect endlessRect;
        if (compact)
        {
            float cardHeight = Mathf.Max(132f, (cardsHeight - 12f) * 0.5f);
            storyRect = new Rect(panel.x + 24f, cardsTop, panel.width - 48f, cardHeight);
            endlessRect = new Rect(panel.x + 24f, storyRect.yMax + 12f, panel.width - 48f, cardHeight);
        }
        else
        {
            float cardWidth = (panel.width - 72f) * 0.5f;
            storyRect = new Rect(panel.x + 24f, cardsTop, cardWidth, cardsHeight);
            endlessRect = new Rect(storyRect.xMax + 24f, cardsTop, cardWidth, cardsHeight);
        }

        DrawModeCard(storyRect, MenuMode.Story, "Сюжетный", "3 уровня", "Прохождение истории главного героя.");
        bool endlessUnlocked = EndlessRunState.StoryCompleted;
        DrawModeCard(
            endlessRect,
            MenuMode.Endless,
            "Бесконечный",
            endlessUnlocked ? "5 комнат на уровень" : "Заблокирован",
            endlessUnlocked
                ? "Случайные комнаты, обязательная зачистка перед выходом и бесконечный рост силы врагов."
                : "Откроется после первого прохождения истории.",
            !endlessUnlocked);

        Rect playRect = new Rect(panel.x + 28f, panel.yMax - 104f, Mathf.Min(270f, panel.width - 56f), 50f);
        if (!compact)
            playRect.x = panel.xMax - playRect.width - 28f;

        if (GUI.Button(playRect, "Играть", buttonStyle))
            StartGame();

        if (!string.IsNullOrEmpty(menuMessage))
        {
            Rect messageRect = new Rect(panel.x + 28f, panel.yMax - 48f, panel.width - 56f, 24f);
            GUI.Label(messageRect, menuMessage, hintStyle);
        }

    }

    private void DrawModeCard(Rect rect, MenuMode mode, string title, string meta, string description, bool locked = false)
    {
        bool selected = selectedMode == mode && !locked;
        Color fill = selected ? new Color(0.045f, 0.083f, 0.096f, 0.96f) : new Color(0.018f, 0.024f, 0.032f, 0.86f);
        Color border = selected ? new Color(0.56f, 0.86f, 0.92f, 0.78f) : new Color(0.34f, 0.45f, 0.50f, 0.42f);
        if (locked)
        {
            fill = new Color(0.014f, 0.018f, 0.024f, 0.82f);
            border = new Color(0.30f, 0.34f, 0.38f, 0.48f);
        }
        DrawPanel(rect, fill, border);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            if (locked)
                menuMessage = "Бесконечный режим откроется после первого прохождения.";
            else
            {
                selectedMode = mode;
                menuMessage = string.Empty;
            }
        }

        GUI.Label(new Rect(rect.x + 18f, rect.y + 22f, rect.width - 36f, 34f), title, modeTitleStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 58f, rect.width - 36f, 24f), meta, labelStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 94f, rect.width - 36f, Mathf.Max(52f, rect.height - 114f)), description, hintStyle);
    }

    private void DrawPanel(Rect rect, Color fill, Color border)
    {
        DrawRect(rect, fill);
        DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
        DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
        DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
        DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
    }

    private void DrawRect(Rect rect, Color color)
    {
        EnsureWhiteTexture();
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(PixelRect(rect), whiteTexture);
        GUI.color = previous;
    }

    private void EnsureWhiteTexture()
    {
        if (whiteTexture != null)
            return;

        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }

    private static Rect PixelRect(Rect rect)
    {
        return new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), Mathf.Round(rect.width), Mathf.Round(rect.height));
    }

    private static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Main menu scene is missing a baked Main Camera.");
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
        camera.allowMSAA = true;
        EnsureSingleAudioListener(camera);
    }

    private static void EnsureSingleAudioListener(Camera camera)
    {
        if (camera == null)
            return;

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
            listener = camera.gameObject.AddComponent<AudioListener>();
        listener.enabled = true;

        foreach (AudioListener other in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (other == null || other == listener || other.gameObject.scene != camera.gameObject.scene)
                continue;

            other.enabled = false;
        }
    }

#if UNITY_EDITOR
    public void BakeSceneForEditor()
    {
        EnsureMenuTexturesForEditor();
        panelTexture = PersistTextureForEditor("Assets/Generated/MainMenu", "menu_panel", panelTexture);
        backgroundTexture = PersistTextureForEditor("Assets/Generated/MainMenu", "menu_background", backgroundTexture);
        EditorUtility.SetDirty(this);
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
}
