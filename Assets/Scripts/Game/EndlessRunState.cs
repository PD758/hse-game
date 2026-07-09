using System;
using UnityEngine;

public static class EndlessRunState
{
    private const string StoryCompletedPrefKey = "Adether.StoryCompleted";
    private static bool storyOutroRequested;
    private static bool completionOverlayPending;

    public static bool Enabled { get; private set; }
    public static int Level { get; private set; } = 1;
    public static int RunSeed { get; private set; } = 1;
    public static string StoryStartLevelId { get; private set; } = string.Empty;

    public static string CurrentLevelId => $"endless_{Level:000}";
    public static bool StoryCompleted => PlayerPrefs.GetInt(StoryCompletedPrefKey, 0) == 1;

    public static void StartRun()
    {
        if (!StoryCompleted)
        {
            Enabled = false;
            StoryStartLevelId = string.Empty;
            return;
        }

        Enabled = true;
        Level = 1;
        RunSeed = Environment.TickCount & 0x7fffffff;
        StoryStartLevelId = string.Empty;
        storyOutroRequested = false;
        completionOverlayPending = false;
        NarrativeRunState.Reset();
    }

    public static void StartStory()
    {
        StartStory(string.Empty);
    }

    public static void StartStory(string startLevelId)
    {
        Enabled = false;
        Level = 1;
        RunSeed = 1;
        StoryStartLevelId = LevelAssetResolver.NormalizeLevelId(startLevelId);
        storyOutroRequested = false;
        completionOverlayPending = false;
        NarrativeRunState.Reset();
    }

    public static void RequestStoryOutro()
    {
        Enabled = false;
        StoryStartLevelId = string.Empty;
        storyOutroRequested = true;
        completionOverlayPending = false;
    }

    public static bool ConsumeStoryOutroRequest()
    {
        if (!storyOutroRequested)
            return false;

        storyOutroRequested = false;
        return true;
    }

    public static void CancelStoryOutro()
    {
        storyOutroRequested = false;
        completionOverlayPending = false;
    }

    public static void CompleteStoryAfterOutro()
    {
        storyOutroRequested = false;
        completionOverlayPending = true;
        PlayerPrefs.SetInt(StoryCompletedPrefKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumeCompletionOverlay()
    {
        if (!completionOverlayPending)
            return false;

        completionOverlayPending = false;
        return true;
    }

    public static void ResetRun()
    {
        Level = 1;
        NarrativeRunState.Reset();
    }

    public static void AdvanceLevel()
    {
        if (!Enabled)
            return;

        Level = Math.Max(1, Level + 1);
    }

    public static int SeedForCurrentLevel()
    {
        unchecked
        {
            return (RunSeed ^ (Level * 73856093)) & 0x7fffffff;
        }
    }
}
