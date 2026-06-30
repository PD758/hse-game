using UnityEngine;

public static class PixelGui
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private static Font cachedFont;

    public static Font Font
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<Font>("Fonts/Pixel");
                if (cachedFont == null)
                    cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans Mono", "Liberation Mono", "DejaVu Sans Mono", "Monospace" }, 14);
            }

            return cachedFont;
        }
    }

    public static void Apply(GUIStyle style)
    {
        if (style == null)
            return;

        style.font = Font;
        style.fontStyle = FontStyle.Normal;
        style.richText = false;
    }

    public static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight), 1f, 2f);

    public static Vector2 LogicalSize
    {
        get
        {
            float scale = Scale;
            return new Vector2(Screen.width / scale, Screen.height / scale);
        }
    }

    public static Matrix4x4 ScaledMatrix
    {
        get
        {
            float scale = Scale;
            return Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        }
    }
}
