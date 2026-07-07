using System;
using System.Collections.Generic;
using UnityEngine;

internal readonly struct GateStatSnapshot
{
    public readonly int EnemiesKilled;
    public readonly int EnemiesKilledOnLevel;
    public readonly int CamerasBroken;
    public readonly int CamerasTriggered;
    public readonly float CurrentRating;

    public GateStatSnapshot(int enemiesKilled, int enemiesKilledOnLevel, int camerasBroken, int camerasTriggered, float currentRating)
    {
        EnemiesKilled = enemiesKilled;
        EnemiesKilledOnLevel = enemiesKilledOnLevel;
        CamerasBroken = camerasBroken;
        CamerasTriggered = camerasTriggered;
        CurrentRating = currentRating;
    }
}

internal static class GateConditionEvaluator
{
    public static bool StatConditionMet(List<object> condition, GateStatSnapshot stats)
    {
        if (condition == null || condition.Count < 3)
            return false;

        string op = Convert.ToString(condition[0]);
        string valueName = Convert.ToString(condition[1]);
        if (!TryReadFloat(condition[2], out float targetValue) || !TryReadStat(valueName, stats, out float currentValue))
            return false;

        return op switch
        {
            "gt" => currentValue > targetValue,
            "lt" => currentValue < targetValue,
            "ge" => currentValue >= targetValue,
            "le" => currentValue <= targetValue,
            "eq" => Mathf.Approximately(currentValue, targetValue),
            "ne" => !Mathf.Approximately(currentValue, targetValue),
            _ => false,
        };
    }

    private static bool TryReadStat(string valueName, GateStatSnapshot stats, out float value)
    {
        switch (valueName)
        {
            case "enemiesKilled":
                value = stats.EnemiesKilled;
                return true;
            case "enemiesKilledOnLevel":
                value = stats.EnemiesKilledOnLevel;
                return true;
            case "camerasBroken":
                value = stats.CamerasBroken;
                return true;
            case "camerasTriggered":
                value = stats.CamerasTriggered;
                return true;
            case "currentRating":
                value = stats.CurrentRating;
                return true;
            default:
                value = 0f;
                return false;
        }
    }

    private static bool TryReadFloat(object raw, out float value)
    {
        switch (raw)
        {
            case null:
                value = 0f;
                return false;
            case float floatValue:
                value = floatValue;
                return true;
            case double doubleValue:
                value = (float)doubleValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            default:
                return float.TryParse(Convert.ToString(raw), out value);
        }
    }
}
