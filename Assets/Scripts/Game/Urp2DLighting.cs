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
        light.intensity = intensity;
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

    public static ShadowCaster2D AddShadowCaster(GameObject owner)
    {
        return owner.AddComponent<ShadowCaster2D>();
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
