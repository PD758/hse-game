using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LevelDefinition
{
    public int version;
    public string id;
    public LevelSize size;
    public LevelPoint playerStart;
    public LevelPoint? exit;
    public List<LevelExit> exits = new List<LevelExit>();
    public List<LevelTileRun> tiles = new List<LevelTileRun>();
    public List<LevelPoint> walls = new List<LevelPoint>();
    public List<LevelObject> objects = new List<LevelObject>();
    public List<LevelEnemy> enemies = new List<LevelEnemy>();
    public LevelLogic logic = new LevelLogic();
    public List<LevelRegion> regions = new List<LevelRegion>();
}

[Serializable]
public struct LevelSize
{
    public int width;
    public int height;
}

[Serializable]
public struct LevelPoint
{
    public int x;
    public int y;

    public LevelPoint(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }
}

[Serializable]
public sealed class LevelDirection
{
    public float x;
    public float y;

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
}

[Serializable]
public sealed class LevelTileRun
{
    public string tile;
    public int y;
    public int x;
    public int length;
    public int variant = -1;
}

[Serializable]
public sealed class LevelObject
{
    public string type;
    public string id;
    public string group;
    public string branch;
    public string frame;
    public string text;
    public int x;
    public int y;
    public int variant = -1;
    public LevelDirection direction;
    public List<string> requiresPlates = new List<string>();
    public List<string> requiresStories = new List<string>();
    public List<string> requiresEnemies = new List<string>();
}

[Serializable]
public sealed class LevelExit
{
    public string id;
    public string branch;
    public string requiresGate;
    public string targetLevel;
    public int x;
    public int y;
    public int variant = -1;

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }
}

[Serializable]
public sealed class LevelEnemy
{
    public string id;
    public string type;
    public string branch;
    public int level = 3;
    public int x;
    public int y;
    public List<List<int>> patrol = new List<List<int>>();
}

[Serializable]
public sealed class LevelLogic
{
    public List<LevelGateRule> gates = new List<LevelGateRule>();
    public List<LevelBranchArea> branchTriggers = new List<LevelBranchArea>();
    public List<LevelBranchArea> branchBlocks = new List<LevelBranchArea>();
    public List<LevelExitRule> exitRules = new List<LevelExitRule>();
}

[Serializable]
public sealed class LevelGateRule
{
    public string id;
    public string group;
    public string openWhen;
}

[Serializable]
public sealed class LevelBranchArea
{
    public string branch;
    public LevelRect area;
}

[Serializable]
public sealed class LevelExitRule
{
    public string branch;
    public string gate;
}

[Serializable]
public sealed class LevelRegion
{
    public string id;
    public List<LevelTileRun> runs = new List<LevelTileRun>();
}

[Serializable]
public struct LevelRect
{
    public int x1;
    public int y1;
    public int x2;
    public int y2;

    public bool Contains(Vector2Int cell)
    {
        return cell.x >= x1 && cell.x <= x2 && cell.y >= y1 && cell.y <= y2;
    }
}
