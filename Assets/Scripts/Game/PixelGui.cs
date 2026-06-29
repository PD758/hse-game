using UnityEngine;

public static class PixelGui
{
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
}
