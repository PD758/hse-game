using UnityEngine;

public static class GameLightingSettings
{
    private const string NormalLightingKey = "settings.normalLighting";

    public static bool NormalLighting
    {
        get => PlayerPrefs.GetInt(NormalLightingKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(NormalLightingKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ShadowsEnabled => !NormalLighting;

    public static float MenuDarkOverlayAlpha => NormalLighting ? 0.14f : 0.36f;

    public static float GameplayGlobalIntensity(float baseIntensity)
    {
        return NormalLighting ? Mathf.Max(baseIntensity, 0.92f) : baseIntensity;
    }

    public static float IntroGlobalIntensity(float baseIntensity)
    {
        return NormalLighting ? Mathf.Max(baseIntensity, 0.88f) : baseIntensity;
    }

    public static float GameplayExposureOffset => NormalLighting ? 0.34f : 0f;

    public static float IntroExposureOffset => NormalLighting ? 0.30f : 0f;

    public static float GameplayVignetteMultiplier => NormalLighting ? 0.48f : 1f;

    public static float IntroShadowAlphaMultiplier => NormalLighting ? 0f : 1f;
}
