public enum BranchChoice
{
    None,
    Puzzle,
    Combat,
}

public static class NarrativeRunState
{
    public static int Aggression { get; private set; }
    public static int Clarity { get; private set; }
    public static int Noise { get; private set; }
    public static int EnemiesKilled { get; private set; }
    public static BranchChoice Branch { get; private set; }

    public static void Reset()
    {
        Aggression = 0;
        Clarity = 0;
        Noise = 0;
        EnemiesKilled = 0;
        Branch = BranchChoice.None;
    }

    public static void RecordAttack()
    {
        Aggression += 2;
        Noise += 1;
    }

    public static void RecordKill()
    {
        Aggression += 3;
        EnemiesKilled += 1;
    }

    public static void RecordNoise(int amount)
    {
        Noise += amount;
        if (amount >= 6)
            Aggression += 1;
    }

    public static void RecordSignalInsight()
    {
        Clarity += 2;
    }

    public static void RecordPuzzleSolved()
    {
        Clarity += 4;
    }

    public static void RecordPuzzleReflection()
    {
        Clarity += 2;
        Noise = System.Math.Max(0, Noise - 1);
    }

    public static void RecordTrapMistake()
    {
        Aggression += 1;
    }

    public static void ChooseBranch(BranchChoice branch)
    {
        if (Branch != BranchChoice.None)
            return;

        Branch = branch;
        if (branch == BranchChoice.Puzzle)
            Clarity += 2;
        else if (branch == BranchChoice.Combat)
            Aggression += 2;
    }

    public static bool IsAggressive()
    {
        return Aggression > Clarity;
    }

    public static string ChannelClosingLine()
    {
        if (Aggression >= Clarity + 3)
            return "Эфир запоминает вашу злость. Шум становится похож на аплодисменты.";

        if (Clarity >= Aggression + 3)
            return "На миг картинка выравнивается. За монтажным столом кто-то оставил дверь.";

        return "Канал не уверен, зритель вы или помеха.";
    }

    public static string SignalHint()
    {
        if (Aggression >= Clarity + 3)
            return "Помехи краснеют. Дикторы говорят всё быстрее.";

        if (Clarity >= Aggression + 3)
            return "Сквозь шум проступает монтажный шов.";

        return "Сигнал дрожит, но ещё держит форму.";
    }
}
