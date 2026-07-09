using System;
using System.Collections.Generic;
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
    private enum CutsceneMode
    {
        Intro,
        Outro,
    }

    private const float Duration = 10.5f;
    private const float IntroSlideExitFadeDuration = 2.15f;
    private const float OutroFinalZoomDuration = 12.5f;
    private const float OutroFinalFullImageTime = 0.58f;
    private const float OutroFinalEndScale = 0.16f;
    private const float SceneRevealFadeDuration = 1.25f;
    private const int IntroAtlasColumns = 4;
    private const int IntroAtlasRows = 8;
    private const float IntroAtlasPixelsPerUnit = 64f;
    private static readonly Vector3 TvCabinetPosition = new Vector3(0f, 1.50f, 0f);
    private static readonly Vector3 TvBodyPosition = new Vector3(0f, 2.20f, 0f);
    private static readonly Vector3 TvGlowPosition = TvBodyPosition + new Vector3(0f, -1.82f, 0f);
    private static readonly Vector3 TvBeamPosition = TvBodyPosition + new Vector3(0f, -1.66f, 0f);
    private static readonly Vector3 ViewerPosition = new Vector3(0f, -1.82f, 0f);
    private const string IntroTextResourcePath = "Texts/intro_cutscene_ru";
    private const string OutroTextResourcePath = "Texts/outro_cutscene_ru";
    private const string IntroSlideResourcePrefix = "Cutscenes/intro_";
    private const string OutroSlideResourcePrefix = "Cutscenes/outro_";
    private static readonly string[] DefaultThoughtLines =
    {
        "template",
        "template",
        "template"
    };

    public Texture2D IntroAtlas;

    [SerializeField] private SpriteRenderer screenRenderer;
    [SerializeField] private SpriteRenderer staticRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private SpriteRenderer beamRenderer;
    [SerializeField] private SpriteRenderer signalRingRenderer;
    [SerializeField] private SpriteRenderer fadeRenderer;
    [SerializeField] private SpriteRenderer viewerRenderer;
    [SerializeField] private SpriteRenderer viewerCastShadowRenderer;
    [SerializeField] private SpriteRenderer tvCabinetRenderer;
    [SerializeField] private SpriteRenderer tvBodyRenderer;
    [SerializeField] private Light2D tvLight;
    [SerializeField] private Light2D pullLight;
    [SerializeField] private Texture2D hudTexture;
    private Volume postProcessVolume;
    private VolumeProfile postProcessProfile;
    private Vignette postProcessVignette;
    private ColorAdjustments postProcessColor;
    private readonly List<IntroSlide> introSlides = new List<IntroSlide>();
    private CutsceneMode cutsceneMode;
    private float startedAt;
    private float slideStartedAt;
    private float slideExitFadeStartedAt;
    private int currentSlideIndex;
    private bool storySlidesActive;
    private bool slideExitFadeActive;
    private string[] thoughtLines = DefaultThoughtLines;

    private sealed class IntroSlide
    {
        public Texture2D Image;
        public string Text;
        public float Duration;
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
        EnsurePostProcessing();
        cutsceneMode = EndlessRunState.ConsumeStoryOutroRequest() ? CutsceneMode.Outro : CutsceneMode.Intro;
        if (cutsceneMode == CutsceneMode.Intro)
        {
            NarrativeRunState.Reset();
            LoadCutsceneText();
        }
        if (!BindSceneReferences())
        {
            Debug.LogError("Intro scene is not baked. Run Rogue > Bootstrap All Scenes before entering Play Mode.");
            enabled = false;
            return;
        }

        EnsureOptionalIntroLayers();
        EnsureLighting();
        LoadSlides();
        if (cutsceneMode == CutsceneMode.Outro && introSlides.Count == 0)
        {
            EndlessRunState.CompleteStoryAfterOutro();
            SceneManager.LoadScene("Prototype");
            enabled = false;
            return;
        }
        storySlidesActive = introSlides.Count > 0;
        slideStartedAt = Time.time;
        startedAt = storySlidesActive ? float.PositiveInfinity : Time.time;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (cutsceneMode == CutsceneMode.Outro)
                EndlessRunState.CancelStoryOutro();
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (storySlidesActive)
        {
            if (slideExitFadeActive)
            {
                if (Time.time - slideExitFadeStartedAt >= IntroSlideExitFadeDuration)
                    FinishIntroSlides();
                return;
            }

            bool advance = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            if (IsOutroFinalSlide() && Time.time - slideStartedAt < introSlides[currentSlideIndex].Duration)
                advance = false;
            if (advance || Time.time - slideStartedAt >= introSlides[currentSlideIndex].Duration)
            {
                if (currentSlideIndex >= introSlides.Count - 1)
                    StartIntroSlideExitFade();
                else
                    AdvanceIntroSlide();
            }
            return;
        }

        if ((keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) || Time.time - startedAt >= Duration)
        {
            GameMusic.Play();
            SceneManager.LoadScene("Prototype");
        }

        AnimateScene();
    }

    private void OnGUI()
    {
        EnsureHudTexture();
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = PixelGui.ScaledMatrix;
        Vector2 guiSize = PixelGui.LogicalSize;
        float screenWidth = guiSize.x;
        float screenHeight = guiSize.y;

        if (storySlidesActive)
        {
            DrawCutsceneSlide(screenWidth, screenHeight);
            GUI.matrix = previousMatrix;
            return;
        }

        float t = Mathf.Clamp01((Time.time - startedAt) / Duration);
        string thought = ThoughtLine(t);
        if (!string.IsNullOrEmpty(thought))
        {
            float width = Mathf.Min(820f, screenWidth - 48f);
            float height = screenWidth < 760 ? 112f : 132f;
            var panel = new Rect((screenWidth - width) * 0.5f, (screenHeight - height) * 0.5f, width, height);
            GUI.color = new Color(1f, 1f, 1f, TextAlpha(t));
            GUI.DrawTexture(panel, hudTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = screenWidth < 760 ? 20 : 28,
                wordWrap = true,
                normal = { textColor = new Color(0.94f, 0.96f, 0.98f) },
            };
            PixelGui.Apply(style);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 18f, panel.width - 56f, panel.height - 36f), thought, style);
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

        float revealAlpha = SceneRevealFadeAlpha();
        if (revealAlpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, revealAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUI.matrix = previousMatrix;
    }

    private void LoadSlides()
    {
        introSlides.Clear();
        string[] outroTexts = cutsceneMode == CutsceneMode.Outro ? LoadOutroSlideTexts() : Array.Empty<string>();
        string prefix = cutsceneMode == CutsceneMode.Outro ? OutroSlideResourcePrefix : IntroSlideResourcePrefix;
        for (int i = 1; i < 100; i++)
        {
            string key = $"{prefix}{i}";
            TextAsset textAsset = Resources.Load<TextAsset>(key);
            Texture2D image = Resources.Load<Texture2D>(key);
            if (textAsset == null && image == null)
                break;

            string text = SlideTextForIndex(i - 1, outroTexts, textAsset);
            introSlides.Add(new IntroSlide
            {
                Image = image,
                Text = text,
                Duration = SlideDurationFor(text),
            });
        }

        if (cutsceneMode == CutsceneMode.Outro)
        {
            for (int i = 0; i < introSlides.Count; i++)
                introSlides[i].Duration = i == introSlides.Count - 1 ? OutroFinalZoomDuration : Mathf.Max(3.1f, introSlides[i].Duration);
        }
    }

    private static string SlideTextForIndex(int index, string[] outroTexts, TextAsset fallbackTextAsset)
    {
        if (outroTexts.Length > index)
            return outroTexts[index];

        return fallbackTextAsset == null ? string.Empty : fallbackTextAsset.text.Trim('\r', '\n');
    }

    private static string[] LoadOutroSlideTexts()
    {
        TextAsset text = Resources.Load<TextAsset>(OutroTextResourcePath);
        if (text == null || string.IsNullOrWhiteSpace(text.text))
            return Array.Empty<string>();

        string normalized = text.text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalized.Length == 0)
            return Array.Empty<string>();

        string[] blocks = normalized.Split(new[] { "\n\n" }, StringSplitOptions.None);
        var result = new List<string>();
        foreach (string block in blocks)
        {
            string trimmed = block.Trim('\n', '\r', ' ', '\t');
            if (trimmed.Length == 0)
                continue;
            result.Add(trimmed);
        }

        return result.ToArray();
    }

    private void AdvanceIntroSlide()
    {
        currentSlideIndex++;
        if (currentSlideIndex < introSlides.Count)
        {
            slideStartedAt = Time.time;
            return;
        }

        FinishIntroSlides();
    }

    private void StartIntroSlideExitFade()
    {
        slideExitFadeActive = true;
        slideExitFadeStartedAt = Time.time;
    }

    private void FinishIntroSlides()
    {
        if (cutsceneMode == CutsceneMode.Outro)
        {
            EndlessRunState.CompleteStoryAfterOutro();
            SceneManager.LoadScene("Prototype");
            return;
        }

        storySlidesActive = false;
        slideExitFadeActive = false;
        startedAt = Time.time;
    }

    private float SceneRevealFadeAlpha()
    {
        if (startedAt == float.PositiveInfinity)
            return 0f;

        return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((Time.time - startedAt) / SceneRevealFadeDuration));
    }

    private static float SlideDurationFor(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 2.4f;

        int wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return 1.6f + text.Length * 0.018f + wordCount * 0.12f;
    }

    private void DrawCutsceneSlide(float screenWidth, float screenHeight)
    {
        if (cutsceneMode == CutsceneMode.Outro && IsOutroFinalSlide())
            DrawOutroSlide(screenWidth, screenHeight);
        else
            DrawIntroSlide(screenWidth, screenHeight);
    }

    private void DrawOutroSlide(float screenWidth, float screenHeight)
    {
        IntroSlide slide = introSlides[Mathf.Clamp(currentSlideIndex, 0, introSlides.Count - 1)];
        Rect fullScreen = new Rect(0f, 0f, screenWidth, screenHeight);
        GUI.color = Color.black;
        GUI.DrawTexture(fullScreen, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (slide.Image != null)
        {
            if (IsOutroFinalSlide())
                DrawOutroFinalZoom(slide.Image, fullScreen);
            else
                GUI.DrawTexture(fullScreen, slide.Image, ScaleMode.ScaleAndCrop, true);
        }

        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = screenWidth < 760f ? 18 : 22,
            normal = { textColor = new Color(0.55f, 0.58f, 0.64f, 0.72f) },
        };
        PixelGui.Apply(hintStyle);
        string hint = IsOutroFinalSlide() ? "Esc: меню" : "Space/Enter: дальше | Esc: меню";
        GUI.Label(new Rect(12f, screenHeight - 48f, screenWidth - 24f, 36f), hint, hintStyle);

        if (slideExitFadeActive)
        {
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - slideExitFadeStartedAt) / IntroSlideExitFadeDuration));
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(fullScreen, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    private void DrawOutroFinalZoom(Texture2D image, Rect fullScreen)
    {
        float screenAspect = fullScreen.width / Mathf.Max(1f, fullScreen.height);
        Rect start = OutroFinalStartSourceRect(image, screenAspect);
        Rect end = CoverSourceRect(image, screenAspect);
        float t = Mathf.Clamp01((Time.time - slideStartedAt) / OutroFinalZoomDuration);
        float sourceT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / OutroFinalFullImageTime));
        float pullbackT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - OutroFinalFullImageTime) / (1f - OutroFinalFullImageTime)));
        Rect source = LerpRect(start, end, sourceT);
        Rect destination = InsetAroundCenter(fullScreen, Mathf.Lerp(1f, OutroFinalEndScale, pullbackT));
        GUI.DrawTextureWithTexCoords(destination, image, TextureCoordsFromTopRect(image, source), true);
    }

    private bool IsOutroFinalSlide()
    {
        return cutsceneMode == CutsceneMode.Outro &&
               introSlides.Count > 0 &&
               currentSlideIndex == introSlides.Count - 1;
    }

    private static Rect OutroFinalStartSourceRect(Texture2D texture, float targetAspect)
    {
        float sourceHeight = texture.height * 0.44f;
        float sourceWidth = sourceHeight * targetAspect;
        if (sourceWidth > texture.width * 0.58f)
        {
            sourceWidth = texture.width * 0.58f;
            sourceHeight = sourceWidth / Mathf.Max(0.01f, targetAspect);
        }

        float centerX = texture.width * 0.50f;
        float centerY = texture.height * 0.45f;
        return ClampSourceRect(new Rect(centerX - sourceWidth * 0.5f, centerY - sourceHeight * 0.5f, sourceWidth, sourceHeight), texture);
    }

    private static Rect CoverSourceRect(Texture2D texture, float targetAspect)
    {
        float textureAspect = texture.width / Mathf.Max(1f, (float)texture.height);
        if (textureAspect > targetAspect)
        {
            float width = texture.height * targetAspect;
            return new Rect((texture.width - width) * 0.5f, 0f, width, texture.height);
        }

        float height = texture.width / Mathf.Max(0.01f, targetAspect);
        return new Rect(0f, (texture.height - height) * 0.5f, texture.width, height);
    }

    private static Rect ClampSourceRect(Rect rect, Texture2D texture)
    {
        float width = Mathf.Min(rect.width, texture.width);
        float height = Mathf.Min(rect.height, texture.height);
        float x = Mathf.Clamp(rect.x, 0f, texture.width - width);
        float y = Mathf.Clamp(rect.y, 0f, texture.height - height);
        return new Rect(x, y, width, height);
    }

    private static Rect LerpRect(Rect from, Rect to, float t)
    {
        return new Rect(
            Mathf.Lerp(from.x, to.x, t),
            Mathf.Lerp(from.y, to.y, t),
            Mathf.Lerp(from.width, to.width, t),
            Mathf.Lerp(from.height, to.height, t));
    }

    private static Rect InsetAroundCenter(Rect rect, float scale)
    {
        float width = rect.width * Mathf.Clamp01(scale);
        float height = rect.height * Mathf.Clamp01(scale);
        return new Rect(rect.x + (rect.width - width) * 0.5f, rect.y + (rect.height - height) * 0.5f, width, height);
    }

    private static Rect TextureCoordsFromTopRect(Texture2D texture, Rect topRect)
    {
        return new Rect(
            topRect.x / texture.width,
            1f - (topRect.y + topRect.height) / texture.height,
            topRect.width / texture.width,
            topRect.height / texture.height);
    }

    private void DrawIntroSlide(float screenWidth, float screenHeight)
    {
        IntroSlide slide = introSlides[Mathf.Clamp(currentSlideIndex, 0, introSlides.Count - 1)];
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float margin = screenWidth < 760f ? 22f : 40f;
        float imageTop = screenHeight < 620f ? 28f : 42f;
        float maxImageWidth = screenWidth - margin * 2f;
        float maxImageHeight = screenHeight * 0.58f;
        Rect imageBounds = new Rect((screenWidth - maxImageWidth) * 0.5f, imageTop, maxImageWidth, maxImageHeight);
        Rect imageRect = imageBounds;
        if (slide.Image != null)
        {
            imageRect = FitTextureRect(slide.Image, imageBounds);
            GUI.DrawTexture(imageBounds, slide.Image, ScaleMode.ScaleToFit, true);
        }

        float textTop = imageRect.yMax + (screenHeight < 620f ? 18f : 28f);
        float textWidth = Mathf.Min(820f, screenWidth - margin * 2f);
        float availableTextHeight = Mathf.Max(128f, screenHeight - textTop - 58f);
        int fontSize = screenWidth < 760f ? 30 : 36;
        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = fontSize,
            wordWrap = true,
            normal = { textColor = new Color(0.88f, 0.90f, 0.94f) },
        };
        PixelGui.Apply(textStyle);
        GUIContent content = new GUIContent(slide.Text);
        float preferredHeight = textStyle.CalcHeight(content, textWidth);
        while (preferredHeight > availableTextHeight && textStyle.fontSize > 18)
        {
            textStyle.fontSize--;
            preferredHeight = textStyle.CalcHeight(content, textWidth);
        }

        Rect textRect = new Rect((screenWidth - textWidth) * 0.5f, textTop, textWidth, availableTextHeight);
        GUI.Label(textRect, content, textStyle);

        var hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = screenWidth < 760f ? 18 : 22,
            normal = { textColor = new Color(0.55f, 0.58f, 0.64f, 0.82f) },
        };
        PixelGui.Apply(hintStyle);
        GUI.Label(new Rect(12f, screenHeight - 48f, screenWidth - 24f, 36f), "Space/Enter: дальше | Esc: меню", hintStyle);

        if (slideExitFadeActive)
        {
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - slideExitFadeStartedAt) / IntroSlideExitFadeDuration));
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    private static Rect FitTextureRect(Texture2D texture, Rect bounds)
    {
        float aspect = texture.width / Mathf.Max(1f, (float)texture.height);
        float width = bounds.width;
        float height = width / Mathf.Max(0.01f, aspect);
        if (height > bounds.height)
        {
            height = bounds.height;
            width = height * aspect;
        }

        return new Rect(bounds.x + (bounds.width - width) * 0.5f, bounds.y, width, height);
    }

    private void LoadCutsceneText()
    {
        TextAsset text = Resources.Load<TextAsset>(IntroTextResourcePath);
        if (text == null || string.IsNullOrWhiteSpace(text.text))
            return;

        string[] rawLines = text.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var lines = new System.Collections.Generic.List<string>();
        foreach (string rawLine in rawLines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;
            lines.Add(line);
        }

        if (lines.Count >= 3)
            thoughtLines = lines.GetRange(0, 3).ToArray();
    }

    private string ThoughtLine(float t)
    {
        if (t < 0.26f)
            return thoughtLines[0];
        if (t < 0.56f)
            return thoughtLines[1];
        if (t < 0.86f)
            return thoughtLines[2];
        return string.Empty;
    }

    private static float TextAlpha(float t)
    {
        return Mathf.Max(
            BeatAlpha(t, 0.06f, 0.26f),
            Mathf.Max(BeatAlpha(t, 0.35f, 0.56f), BeatAlpha(t, 0.65f, 0.86f)));
    }

    private static float BeatAlpha(float t, float start, float end)
    {
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - start) / 0.055f));
        float fadeOut = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - (end - 0.055f)) / 0.055f));
        return fadeIn * fadeOut;
    }

    private static float SleepFadeAlpha(float t)
    {
        float beats = Mathf.Max(
            BeatAlpha(t, 0.02f, 0.30f),
            Mathf.Max(BeatAlpha(t, 0.31f, 0.60f), BeatAlpha(t, 0.61f, 0.90f)));
        float finalFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.90f) / 0.09f));
        return Mathf.Clamp01(beats * 0.58f + finalFade);
    }

    private void AnimateScene()
    {
        float elapsed = Time.time - startedAt;
        float t = Mathf.Clamp01(elapsed / Duration);
        float pulse = 0.5f + Mathf.Sin(Time.time * 18f) * 0.5f;
        float drowsy = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.12f) / 0.58f));
        float sleep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.54f) / 0.32f));
        float screenFade = SleepFadeAlpha(t);
        float staticPulse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.08f) / 0.36f));
        float shake = (0.004f + drowsy * 0.010f) * (0.45f + pulse * 0.55f);
        Vector3 cameraShake = new Vector3(Mathf.Sin(Time.time * 11f) * shake, Mathf.Cos(Time.time * 9f) * shake * 0.55f, 0f);
        Vector3 tvShake = new Vector3(Mathf.Sin(Time.time * 31f) * 0.006f * drowsy, Mathf.Cos(Time.time * 29f) * 0.004f * drowsy, 0f);

        screenRenderer.color = Color.Lerp(new Color(0.22f, 0.29f, 0.42f), new Color(0.70f, 0.88f, 1.00f), pulse * 0.32f + staticPulse * 0.28f);
        screenRenderer.transform.position = TvBodyPosition + tvShake;
        screenRenderer.transform.localScale = Vector3.one * (1f + pulse * 0.018f + drowsy * 0.025f);
        if (staticRenderer != null)
        {
            staticRenderer.color = new Color(0.78f, 0.93f, 1f, Mathf.Lerp(0.04f, 0.26f, staticPulse) + pulse * 0.05f + screenFade * 0.10f);
            staticRenderer.transform.position = TvBodyPosition + tvShake + new Vector3(Mathf.Sin(Time.time * 43f) * 0.012f, Mathf.Cos(Time.time * 37f) * 0.008f, 0f);
            staticRenderer.transform.localScale = Vector3.one * (1.02f + pulse * 0.035f);
        }

        if (tvCabinetRenderer != null)
            tvCabinetRenderer.transform.position = TvCabinetPosition;
        if (tvBodyRenderer != null)
            tvBodyRenderer.transform.position = TvBodyPosition + tvShake;

        glowRenderer.color = new Color(0.55f, 0.82f, 1f, 0.08f + pulse * 0.06f + drowsy * 0.18f + screenFade * 0.08f);
        glowRenderer.transform.position = TvGlowPosition;
        glowRenderer.transform.localScale = new Vector3(1.0f + drowsy * 0.42f + pulse * 0.06f, 1.0f + drowsy * 0.62f + pulse * 0.08f, 1f);
        beamRenderer.color = new Color(0.62f, 0.88f, 1f, Mathf.SmoothStep(0f, 0.18f, drowsy) * (1f - sleep * 0.45f));
        beamRenderer.transform.position = TvBeamPosition;
        beamRenderer.transform.localScale = new Vector3(0.78f + drowsy * 0.22f + pulse * 0.04f, 0.74f + drowsy * 0.18f, 1f);
        if (signalRingRenderer != null)
        {
            signalRingRenderer.color = new Color(0.76f, 0.94f, 1f, Mathf.SmoothStep(0f, 0.24f, drowsy) * (1f - sleep * 0.65f));
            signalRingRenderer.transform.position = TvBodyPosition + tvShake;
            signalRingRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.54f, 1.45f, drowsy + pulse * 0.08f);
            signalRingRenderer.transform.Rotate(0f, 0f, (32f + drowsy * 74f) * Time.deltaTime);
        }

        float castShadow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.22f) / 0.46f));
        float shadowAlpha = Mathf.Lerp(0f, 0.58f, castShadow) * GameLightingSettings.IntroShadowAlphaMultiplier;
        viewerCastShadowRenderer.color = new Color(0f, 0f, 0f, shadowAlpha);
        viewerCastShadowRenderer.transform.localScale = new Vector3(1f + castShadow * 0.42f, 1f + castShadow * 0.24f, 1f);
        if (tvLight != null)
        {
            tvLight.intensity = Mathf.Lerp(1.05f, 1.95f, drowsy) + pulse * 0.18f + screenFade * 0.20f;
            tvLight.pointLightOuterRadius = Mathf.Lerp(5.2f, 6.6f, drowsy);
        }

        if (pullLight != null)
            pullLight.intensity = Mathf.SmoothStep(0f, 0.42f, drowsy) * (1f - sleep * 0.55f);

        viewerRenderer.transform.position = ViewerPosition;
        viewerRenderer.transform.localScale = Vector3.one;
        viewerRenderer.transform.localRotation = Quaternion.identity;
        viewerRenderer.color = Color.Lerp(Color.white, new Color(0.68f, 0.76f, 0.86f, 0.88f), sleep * 0.52f);
        fadeRenderer.color = new Color(0f, 0f, 0f, SleepFadeAlpha(t));

        if (postProcessColor != null)
        {
            postProcessColor.contrast.Override(Mathf.Lerp(10f, 24f, drowsy));
            postProcessColor.saturation.Override(Mathf.Lerp(-4f, -30f, sleep));
            postProcessColor.postExposure.Override(-0.04f + GameLightingSettings.IntroExposureOffset - screenFade * 0.34f);
        }
        if (postProcessVignette != null)
        {
            float vignettePulse = Mathf.Max(BeatAlpha(t, 0.30f, 0.42f), Mathf.Max(BeatAlpha(t, 0.58f, 0.70f), BeatAlpha(t, 0.78f, 0.90f)));
            postProcessVignette.intensity.Override(Mathf.Lerp(0.18f, 0.34f, drowsy) + vignettePulse * 0.22f + screenFade * 0.10f);
            postProcessVignette.smoothness.Override(Mathf.Lerp(0.58f, 0.76f, drowsy));
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            float zoomPulse = Mathf.Max(BeatAlpha(t, 0.30f, 0.42f), Mathf.Max(BeatAlpha(t, 0.58f, 0.70f), BeatAlpha(t, 0.78f, 0.90f)));
            camera.orthographicSize = Mathf.Lerp(5.4f, 4.65f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.20f) / 0.60f))) - zoomPulse * 0.28f;
            camera.transform.position = new Vector3(0f, Mathf.Lerp(0f, -0.12f, sleep), -10f) + cameraShake;
        }
    }

    private void BuildScene()
    {
        ThrowIfPlayingBake("BuildScene");
        new GameObject("Intro Art");
        CreateSpriteObject("Room Floor", IntroSpriteOrFallback(0, 3, "intro_floor", CreateRoomFloorSprite()), Vector3.zero, new Vector3(2.5f, 1.6f, 1f), -10);
        CreateSpriteObject("Window Shadow", IntroSpriteOrFallback(3, 0, "intro_window_shadow", CreateSoftRectSprite(128, 32, new Color(0.015f, 0.018f, 0.022f, 0.46f))), new Vector3(-3.8f, 2.3f, 0f), new Vector3(2.0f, 1f, 1f), -8);
        SpriteRenderer tvCabinet = CreateSpriteObject("TV Cabinet", IntroSpriteOrFallback(1, 0, "intro_tv_cabinet", CreateRectSprite(96, 26, new Color(0.13f, 0.115f, 0.108f), new Color(0.060f, 0.054f, 0.052f))), TvCabinetPosition, new Vector3(1.25f, 1f, 1f), -3);
        tvCabinetRenderer = tvCabinet;

        SpriteRenderer couchShadow = CreateSpriteObject("Couch Shadow", IntroSpriteOrFallback(0, 1, "intro_couch_shadow", CreateEllipseSprite(160, 48, new Color(0f, 0f, 0f, 0.45f))), new Vector3(0f, -2.18f, 0f), Vector3.one, -4);
        couchShadow.transform.localScale = new Vector3(1.4f, 0.9f, 1f);
        SpriteRenderer couch = CreateSpriteObject("Couch", IntroSpriteOrFallback(0, 0, "intro_couch", CreateCouchSprite()), new Vector3(0f, -2.0f, 0f), Vector3.one, 1);
        viewerRenderer = CreateSpriteObject("Viewer", IntroSpriteOrFallback(2, 0, "intro_viewer_seated", CreateViewerSprite()), ViewerPosition, Vector3.one, 5);
        viewerCastShadowRenderer = CreateSpriteObject("Viewer Cast Shadow", CreateHumanCastShadowSprite(), new Vector3(0f, -2.34f, 0f), Vector3.one, 2);
        viewerCastShadowRenderer.color = new Color(0f, 0f, 0f, 0f);
        CreateSpriteObject("Viewer Shadow", CreateEllipseSprite(54, 24, new Color(0f, 0f, 0f, 0.42f)), new Vector3(0f, -1.96f, 0f), Vector3.one, 0);

        SpriteRenderer tvBody = CreateSpriteObject("TV Body", IntroAtlas != null ? null : CreateTvBodySprite(), TvBodyPosition, Vector3.one, 4);
        tvBodyRenderer = tvBody;
        screenRenderer = CreateSpriteObject("TV Screen", IntroSpriteOrFallback(1, 1, "intro_tv_screen", CreateStaticScreenSprite()), TvBodyPosition, Vector3.one, 5);
        staticRenderer = CreateSpriteObject("TV Static Overlay", IntroSpriteOrFallback(1, 1, "intro_tv_static_overlay", CreateStaticScreenSprite()), TvBodyPosition, Vector3.one, 6);
        glowRenderer = CreateSpriteObject("TV Glow", CreateGlowConeSprite(), TvGlowPosition, new Vector3(1.2f, 1f, 1f), -2);
        beamRenderer = CreateSpriteObject("Pull Beam", CreateBeamSprite(), TvBeamPosition, Vector3.one, 7);
        signalRingRenderer = CreateSpriteObject("Signal Ring", CreateSignalRingSprite(), TvBodyPosition, new Vector3(0.52f, 0.52f, 1f), 8);
        fadeRenderer = CreateSpriteObject("Fade", CreateSolidSprite(new Color(0f, 0f, 0f, 1f), 16, 10), Vector3.zero, Vector3.one, 100);
        fadeRenderer.color = new Color(0f, 0f, 0f, 0f);

        SetUnlit(couchShadow, screenRenderer, staticRenderer, glowRenderer, beamRenderer, signalRingRenderer, viewerCastShadowRenderer, fadeRenderer);
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
        tvCabinetRenderer = FindRenderer("TV Cabinet");
        tvBodyRenderer = FindRenderer("TV Body");
        screenRenderer = FindRenderer("TV Screen");
        staticRenderer = FindRenderer("TV Static Overlay");
        glowRenderer = FindRenderer("TV Glow");
        beamRenderer = FindRenderer("Pull Beam");
        signalRingRenderer = FindRenderer("Signal Ring");
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
                light.intensity = GameLightingSettings.IntroGlobalIntensity(0.52f);
                hasGlobalLight = true;
            }
        }
        if (!hasGlobalLight)
            Urp2DLighting.AddGlobalLight(gameObject, new Color(0.58f, 0.62f, 0.68f), GameLightingSettings.IntroGlobalIntensity(0.52f));

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

    private void EnsureOptionalIntroLayers()
    {
        if (staticRenderer == null && screenRenderer != null)
            staticRenderer = CreateRuntimeSpriteObject("TV Static Overlay", screenRenderer.sprite, TvBodyPosition, Vector3.one, 6);
        if (signalRingRenderer == null)
            signalRingRenderer = CreateRuntimeSpriteObject("Signal Ring", CreateSignalRingSprite(), TvBodyPosition, new Vector3(0.52f, 0.52f, 1f), 8);

        SetUnlit(staticRenderer, signalRingRenderer);
    }

    private static SpriteRenderer CreateRuntimeSpriteObject(string name, Sprite sprite, Vector3 position, Vector3 scale, int sortingOrder)
    {
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
        {
            if (renderer != null)
                renderer.sharedMaterial = material;
        }
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

    private static Sprite CreateSignalRingSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float distance = delta.magnitude / (size * 0.5f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.62f) * 18f);
                float outer = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.92f) * 24f) * 0.45f;
                float spoke = Mathf.Abs(Mathf.Sin(Mathf.Atan2(delta.y, delta.x) * 6f)) > 0.93f ? 0.28f : 0f;
                float alpha = Mathf.Clamp01(Mathf.Max(ring, outer) + spoke * Mathf.Clamp01(1f - distance)) * Mathf.SmoothStep(1f, 0f, distance);
                texture.SetPixel(x, y, new Color(0.70f, 0.92f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
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

        postProcessVignette = postProcessProfile.Add<Vignette>(true);
        postProcessVignette.color.Override(new Color(0.008f, 0.010f, 0.016f));
        postProcessVignette.center.Override(new Vector2(0.5f, 0.5f));
        postProcessVignette.intensity.Override(0.18f);
        postProcessVignette.smoothness.Override(0.58f);
        postProcessVignette.rounded.Override(true);

        postProcessColor = postProcessProfile.Add<ColorAdjustments>(true);
        postProcessColor.postExposure.Override(-0.04f);
        postProcessColor.contrast.Override(12f);
        postProcessColor.saturation.Override(-6f);
        postProcessColor.colorFilter.Override(new Color(0.88f, 0.95f, 1f));

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
        ApplyLightingSettings();
    }

    private void ApplyLightingSettings()
    {
        if (postProcessColor != null)
            postProcessColor.postExposure.Override(-0.04f + GameLightingSettings.IntroExposureOffset);
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
        EnsureSingleAudioListener(camera);

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.dithering = true;
    }

    private static void EnsureSingleAudioListener(Camera camera)
    {
        if (camera == null)
            return;

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
            listener = camera.gameObject.AddComponent<AudioListener>();
        listener.enabled = true;

        foreach (AudioListener other in FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            if (other == null || other == listener || other.gameObject.scene != camera.gameObject.scene)
                continue;

            other.enabled = false;
        }
    }
}
