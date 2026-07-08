using UnityEngine;

public static class GameMusic
{
    private const string MusicResourcePath = "music/Atmospheric Horror Broadcast Tension";
    private const string VolumePrefsKey = "music_volume";
    private const float DefaultVolume = 0.55f;

    private static AudioSource source;

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumePrefsKey, DefaultVolume);
        set
        {
            float clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePrefsKey, clamped);
            if (source != null)
                source.volume = clamped;
        }
    }

    public static void Play()
    {
        EnsureSource();
        if (source == null || source.clip == null)
            return;

        source.volume = Volume;
        if (!source.isPlaying)
            source.Play();
    }

    public static void Stop()
    {
        if (source != null)
            source.Stop();
    }

    private static void EnsureSource()
    {
        if (source != null)
            return;

        GameObject musicObject = new GameObject("Game Music");
        Object.DontDestroyOnLoad(musicObject);

        source = musicObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = Volume;
        source.clip = Resources.Load<AudioClip>(MusicResourcePath);

        if (source.clip == null)
            Debug.LogWarning($"Music clip not found in Resources: {MusicResourcePath}");
    }
}
