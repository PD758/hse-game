using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MainMenu : MonoBehaviour
{
    private enum MenuMode
    {
        Offline,
        Server,
        Client,
    }

    private MenuMode mode = MenuMode.Server;
    private string serverPort = NetworkSessionConfig.DefaultPort.ToString();
    private string clientAddress = NetworkSessionConfig.DefaultAddress;
    private string clientPort = NetworkSessionConfig.DefaultPort.ToString();
    private string validationMessage = string.Empty;

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

        GUILayout.Label("Rogue Sync", titleStyle);
        GUILayout.Label("Handcrafted floors, tactical noise, multiplayer shell", hintStyle);
        GUILayout.Space(20f);

        GUILayout.Label("Mode", labelStyle);
        GUILayout.BeginHorizontal();
        DrawModeButton("Offline", MenuMode.Offline);
        DrawModeButton("Server", MenuMode.Server);
        DrawModeButton("Client", MenuMode.Client);
        GUILayout.EndHorizontal();

        GUILayout.Space(18f);
        if (mode == MenuMode.Server)
        {
            GUILayout.Label("Starts a local TCP listener. Gameplay sync comes in the next networking pass.", hintStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Server port", labelStyle);
            serverPort = GUILayout.TextField(serverPort, GUILayout.Height(34f));
        }
        else if (mode == MenuMode.Client)
        {
            GUILayout.Label("Connects to a running server instance.", hintStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Server address", labelStyle);
            clientAddress = GUILayout.TextField(clientAddress, GUILayout.Height(34f));
            GUILayout.Space(10f);
            GUILayout.Label("Server port", labelStyle);
            clientPort = GUILayout.TextField(clientPort, GUILayout.Height(34f));
        }
        else
        {
            GUILayout.Label("Local run without network listener.", hintStyle);
        }

        GUILayout.Space(18f);
        if (!string.IsNullOrEmpty(validationMessage))
            GUILayout.Label(validationMessage, hintStyle);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Start", buttonStyle, GUILayout.Height(44f)))
            StartGame();

        GUILayout.Space(12f);
        GUILayout.Label("Controls in game: WASD/arrows move, push stones onto plates, R restarts, Esc returns here.", hintStyle);
        GUILayout.EndArea();
    }

    private void DrawModeButton(string title, MenuMode target)
    {
        bool selected = mode == target;
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = selected ? new Color(0.20f, 0.58f, 0.70f) : new Color(0.20f, 0.22f, 0.25f);
        if (GUILayout.Button(title, buttonStyle, GUILayout.Height(36f)))
            mode = target;
        GUI.backgroundColor = previous;
    }

    private void StartGame()
    {
        validationMessage = string.Empty;

        if (mode == MenuMode.Offline)
        {
            NetworkSessionConfig.SetOffline();
        }
        else if (mode == MenuMode.Server)
        {
            if (!TryReadPort(serverPort, out int port))
                return;
            NetworkSessionConfig.SetServer(port);
        }
        else
        {
            if (!TryReadPort(clientPort, out int port))
                return;
            NetworkSessionConfig.SetClient(clientAddress, port);
        }

        SceneManager.LoadScene("Prototype");
    }

    private bool TryReadPort(string text, out int port)
    {
        if (!int.TryParse(text, out port) || port < 1 || port > 65535)
        {
            validationMessage = "Port must be a number from 1 to 65535.";
            return false;
        }

        return true;
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
