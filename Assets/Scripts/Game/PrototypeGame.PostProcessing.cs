using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class PrototypeGame
{
    private void EnsurePostProcessing()
    {
        GameObject volumeObject = GameObject.Find("Post Processing");
        if (volumeObject == null)
            volumeObject = new GameObject("Post Processing");

        postProcessVolume = volumeObject.GetComponent<Volume>();
        if (postProcessVolume == null)
            postProcessVolume = volumeObject.AddComponent<Volume>();

        postProcessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        postProcessProfile.name = "Prototype Runtime Post Processing";

        postProcessVignette = postProcessProfile.Add<Vignette>(true);
        postProcessVignette.color.Override(new Color(0.01f, 0.014f, 0.022f));
        postProcessVignette.center.Override(new Vector2(0.5f, 0.5f));
        postProcessVignette.rounded.Override(true);

        postProcessColor = postProcessProfile.Add<ColorAdjustments>(true);
        postProcessColor.postExposure.Override(-0.02f);
        postProcessColor.contrast.Override(12f);
        postProcessColor.saturation.Override(-4f);
        postProcessColor.colorFilter.Override(new Color(0.90f, 0.97f, 1f));

        postProcessChromaticAberration = postProcessProfile.Add<ChromaticAberration>(true);
        postProcessChromaticAberration.intensity.Override(0.035f);

        postProcessFilmGrain = postProcessProfile.Add<FilmGrain>(true);
        postProcessFilmGrain.type.Override(FilmGrainLookup.Thin1);
        postProcessFilmGrain.intensity.Override(0.12f);
        postProcessFilmGrain.response.Override(0.80f);

        postProcessLensDistortion = postProcessProfile.Add<LensDistortion>(true);
        postProcessLensDistortion.intensity.Override(-0.030f);
        postProcessLensDistortion.xMultiplier.Override(1f);
        postProcessLensDistortion.yMultiplier.Override(1f);
        postProcessLensDistortion.center.Override(new Vector2(0.5f, 0.5f));
        postProcessLensDistortion.scale.Override(1.012f);

        postProcessBloom = postProcessProfile.Add<Bloom>(true);
        postProcessBloom.threshold.Override(0.96f);
        postProcessBloom.intensity.Override(0.22f);
        postProcessBloom.scatter.Override(0.52f);
        postProcessBloom.tint.Override(new Color(0.82f, 0.94f, 1f));
        postProcessBloom.highQualityFiltering.Override(false);
        postProcessBloom.filter.Override(BloomFilterMode.Kawase);
        postProcessBloom.downscale.Override(BloomDownscaleMode.Quarter);
        postProcessBloom.maxIterations.Override(4);

        postProcessVolume.isGlobal = true;
        postProcessVolume.priority = 0f;
        postProcessVolume.weight = 1f;
        postProcessVolume.sharedProfile = postProcessProfile;

        UpdatePostProcessing();
    }

    private void UpdatePostProcessing()
    {
        if (postProcessVignette == null)
            return;

        float ratingDanger = 1f - Mathf.Clamp01(viewerRating / 100f);
        float criticalPressure = viewerRating <= RatingCritical ? Mathf.InverseLerp(RatingCritical, 0f, viewerRating) : 0f;
        float vignetteIntensity = Mathf.Lerp(0.16f, 0.48f, ratingDanger) + criticalPressure * 0.08f;

        postProcessVignette.intensity.Override(Mathf.Clamp01(vignetteIntensity * GameLightingSettings.GameplayVignetteMultiplier));
        postProcessVignette.smoothness.Override(Mathf.Lerp(0.48f, 0.76f, ratingDanger));

        if (postProcessColor != null)
        {
            postProcessColor.postExposure.Override(Mathf.Lerp(-0.02f, -0.10f, ratingDanger) + GameLightingSettings.GameplayExposureOffset);
            postProcessColor.contrast.Override(Mathf.Lerp(12f, 24f, ratingDanger));
            postProcessColor.saturation.Override(Mathf.Lerp(-4f, -14f, ratingDanger));
        }

        if (postProcessChromaticAberration != null)
            postProcessChromaticAberration.intensity.Override(Mathf.Lerp(0.035f, 0.12f, criticalPressure));
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Prototype scene is missing a baked Main Camera.");
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = GameplayCameraSize;
        camera.transform.position = new Vector3(8f, 10f, -10f);
        camera.backgroundColor = new Color(0.070f, 0.076f, 0.086f);
        camera.allowMSAA = true;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.dithering = true;
    }
}
