using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class Urp2DLighting
{
    private static Material spriteLitMaterial;
    private static Material spriteUnlitMaterial;

    public static Material SpriteLitMaterial
    {
        get
        {
            if (spriteLitMaterial == null)
                spriteLitMaterial = CreateMaterial("Universal Render Pipeline/2D/Sprite-Lit-Default", "Runtime Sprite Lit");
            return spriteLitMaterial;
        }
    }

    public static Material SpriteUnlitMaterial
    {
        get
        {
            if (spriteUnlitMaterial == null)
                spriteUnlitMaterial = CreateMaterial("Universal Render Pipeline/2D/Sprite-Unlit-Default", "Runtime Sprite Unlit");
            return spriteUnlitMaterial;
        }
    }

    public static Light2D AddGlobalLight(GameObject owner, Color color, float intensity)
    {
        var light = owner.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = color;
        light.intensity = GameLightingSettings.GameplayGlobalIntensity(intensity);
        return light;
    }

    public static Light2D AddPointLight(GameObject owner, Color color, float intensity, float outerRadius, float innerRadius = 0f)
    {
        var light = owner.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = outerRadius;
        light.pointLightInnerRadius = innerRadius;
        return light;
    }

    public static Light2D AddConeLight(GameObject owner, Color color, float intensity, float outerRadius, float innerRadius, float outerAngle, float innerAngle, Vector2 direction)
    {
        Light2D light = AddPointLight(owner, color, intensity, outerRadius, innerRadius);
        ConfigureConeLight(light, color, intensity, outerRadius, innerRadius, outerAngle, innerAngle, direction);
        return light;
    }

    public static ShadowCaster2D AddShadowCaster(GameObject owner)
    {
        ShadowCaster2D caster = owner.GetComponent<ShadowCaster2D>();
        if (caster == null)
            caster = owner.AddComponent<ShadowCaster2D>();
        ConfigureShadowCaster(caster, true);
        return caster;
    }

    public static void ConfigurePointLightShadows(Light2D light, float intensity, float softness, float softnessFalloff = 0.5f)
    {
        if (light == null)
            return;

        bool shadowsEnabled = intensity > 0f && GameLightingSettings.ShadowsEnabled;
        light.shadowsEnabled = shadowsEnabled;
        light.shadowIntensity = shadowsEnabled ? Mathf.Clamp01(intensity) : 0f;
        light.shadowSoftness = Mathf.Max(0f, softness);
        light.shadowSoftnessFalloffIntensity = Mathf.Clamp01(softnessFalloff);
    }

    public static void ConfigureConeLight(Light2D light, Color color, float intensity, float outerRadius, float innerRadius, float outerAngle, float innerAngle, Vector2 direction)
    {
        if (light == null)
            return;

        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = outerRadius;
        light.pointLightInnerRadius = innerRadius;
        light.pointLightOuterAngle = outerAngle;
        light.pointLightInnerAngle = innerAngle;
        RotateToward(light.transform, direction);
    }

    public static void RotateToward(Transform transform, Vector2 direction)
    {
        if (transform == null)
            return;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.up;

        direction.Normalize();
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    public static void ConfigureShadowCaster(ShadowCaster2D caster, bool enabled)
    {
        if (caster == null)
            return;

        bool shadowsEnabled = enabled && GameLightingSettings.ShadowsEnabled;
        caster.castsShadows = shadowsEnabled;
        caster.selfShadows = false;
        caster.alphaCutoff = 0.02f;
        caster.enabled = shadowsEnabled;
    }

    private static Material CreateMaterial(string shaderName, string materialName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            return null;

        return new Material(shader)
        {
            name = materialName,
        };
    }
}
