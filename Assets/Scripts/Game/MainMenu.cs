using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenu : MonoBehaviour
{
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle hintStyle;
    private GUIStyle buttonStyle;
    private Texture2D panelTexture;
    private Texture2D backgroundTexture;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawBackground();

        float panelWidth = Mathf.Min(580f, Screen.width - 32f);
        float panelHeight = 500f;
        var panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);

        GUI.DrawTexture(panel, panelTexture);
        GUILayout.BeginArea(new Rect(panel.x + 28f, panel.y + 24f, panel.width - 56f, panel.height - 48f));

        GUILayout.Label("Канал", titleStyle);
        GUILayout.Label("Рогалик о человеке, которого затягивает в обязательный эфир.", hintStyle);
        GUILayout.Space(18f);
        GUILayout.Label("Один игрок", labelStyle);
        GUILayout.Label("Первый канал: новости, рейтинг зрительского внимания, развилка между разбором сигнала и агрессивным эфиром.", hintStyle);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Смотреть", buttonStyle, GUILayout.Height(44f)))
            StartGame();

        GUILayout.Space(12f);
        GUILayout.Label("В игре: WASD/стрелки движение, Space/ЛКМ атака, E взаимодействие, R перезапуск, Esc меню.", hintStyle);
        GUILayout.EndArea();
    }

    private void StartGame()
    {
        SceneManager.LoadScene("Intro");
    }

    private void EnsureStyles()
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
                filterMode = FilterMode.Point,
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

        titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 38,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };
        labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.86f, 0.88f, 0.90f) },
        };
        hintStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true,
            normal = { textColor = new Color(0.68f, 0.72f, 0.76f) },
        };
        buttonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
        };
    }

    private void DrawBackground()
    {
        for (int x = 0; x < Screen.width; x += backgroundTexture.width)
        {
            for (int y = 0; y < Screen.height; y += backgroundTexture.height)
                GUI.DrawTexture(new Rect(x, y, backgroundTexture.width, backgroundTexture.height), backgroundTexture);
        }
    }

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
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
    }
}
