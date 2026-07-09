using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class Urp2DLighting
{
    private static Material spriteLitMaterial;
    private static Material spriteUnlitMaterial;
    private static readonly FieldInfo ShadowShapePathField = typeof(ShadowCaster2D).GetField("m_ShapePath", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ShadowShapePathHashField = typeof(ShadowCaster2D).GetField("m_ShapePathHash", BindingFlags.Instance | BindingFlags.NonPublic);

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

    public static void ConfigureShadowCaster(ShadowCaster2D caster, bool enabled, bool selfShadows = false)
    {
        if (caster == null)
            return;

        bool shadowsEnabled = enabled && GameLightingSettings.ShadowsEnabled;
        caster.castsShadows = shadowsEnabled;
        caster.selfShadows = shadowsEnabled && selfShadows;
        caster.alphaCutoff = 0.02f;
        caster.enabled = shadowsEnabled;
    }

    public static void ConfigureBoxShadowShape(ShadowCaster2D caster, float width, float height)
    {
        if (caster == null || ShadowShapePathField == null || ShadowShapePathHashField == null)
            return;

        float halfWidth = Mathf.Max(0.01f, width) * 0.5f;
        float halfHeight = Mathf.Max(0.01f, height) * 0.5f;
        var shape = new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f),
        };

        ShadowShapePathField.SetValue(caster, shape);
        ShadowShapePathHashField.SetValue(caster, unchecked((int)2166136261 ^ shape.Length ^ Mathf.RoundToInt(width * 1000f) ^ Mathf.RoundToInt(height * 1000f)));
        if (caster.enabled)
        {
            caster.enabled = false;
            caster.enabled = GameLightingSettings.ShadowsEnabled;
        }
    }

    public static void ConfigureIsometricBoxShadowShape(ShadowCaster2D caster)
    {
        if (caster == null || ShadowShapePathField == null || ShadowShapePathHashField == null)
            return;

        var shape = new[]
        {
            new Vector3(0f, 0.30f, 0f),
            new Vector3(0.29f, 0.13f, 0f),
            new Vector3(0.29f, -0.20f, 0f),
            new Vector3(0f, -0.34f, 0f),
            new Vector3(-0.29f, -0.20f, 0f),
            new Vector3(-0.29f, 0.13f, 0f),
        };

        ShadowShapePathField.SetValue(caster, shape);
        ShadowShapePathHashField.SetValue(caster, unchecked((int)2166136261 ^ 0x49534F ^ shape.Length));
        if (caster.enabled)
        {
            caster.enabled = false;
            caster.enabled = GameLightingSettings.ShadowsEnabled;
        }
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
