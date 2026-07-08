using System;

public static class EndlessRunState
{
    public static bool Enabled { get; private set; }
    public static int Level { get; private set; } = 1;
    public static int RunSeed { get; private set; } = 1;
    public static string StoryStartLevelId { get; private set; } = string.Empty;

    public static string CurrentLevelId => $"endless_{Level:000}";

    public static void StartRun()
    {
        Enabled = true;
        Level = 1;
        RunSeed = Environment.TickCount & 0x7fffffff;
        StoryStartLevelId = string.Empty;
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
        NarrativeRunState.Reset();
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
