using System.Collections.Generic;
using UnityEngine;

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
    public List<LevelDecoration> decorations = new List<LevelDecoration>();
    public List<LevelLight> lights = new List<LevelLight>();
    public List<LevelEvent> events = new List<LevelEvent>();
    public LevelLogic logic = new LevelLogic();
    public List<LevelRegion> regions = new List<LevelRegion>();
}

public struct LevelSize
{
    public int width;
    public int height;
}

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

public sealed class LevelDirection
{
    public float x;
    public float y;

    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
}

public sealed class LevelTileRun
{
    public string tile;
    public int y;
    public int x;
    public int length;
    public int variant = -1;
}

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
    public List<List<object>> requiresStats = new List<List<object>>();
}

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

public sealed class LevelEnemy
{
    public string id;
    public string type;
    public string group;
    public string alertGroup;
    public string branch;
    public int level = 3;
    public int hp = 2;
    public float hearing;
    public float vision;
    public int x;
    public int y;
    public List<List<int>> patrol = new List<List<int>>();
}

public sealed class LevelDecoration
{
    public string id;
    public string texturePath;
    public float x;
    public float y;
    public float scale = 1f;
    public float rotation;
    public int sortingOrder = 4;
    public bool castsShadow;
}

public sealed class LevelLight
{
    public string id;
    public string type = "point";
    public float x;
    public float y;
    public float intensity = 0.7f;
    public float radius = 4f;
    public string color = "#d6f0ff";
    public float rotation;
    public float outerAngle = 65f;
    public float innerAngle = 30f;
}

public sealed class LevelEvent
{
    public string id;
    public bool enabled = true;
    public bool once = true;
    public string trigger;
    public string region;
    public string enemyId;
    public string enemyGroup;
    public List<List<object>> conditions = new List<List<object>>();
    public List<LevelEventAction> actions = new List<LevelEventAction>();
}

public sealed class LevelEventAction
{
    public string type;
    public string id;
    public string group;
    public string tile;
    public string objectType;
    public string effect;
    public string text;
    public int x;
    public int y;
    public int variant = -1;
    public LevelObject obj;
    public LevelEnemy enemy;
    public List<LevelEnemy> enemies = new List<LevelEnemy>();
}

public sealed class LevelLogic
{
    public List<LevelGateRule> gates = new List<LevelGateRule>();
    public List<LevelBranchArea> branchTriggers = new List<LevelBranchArea>();
    public List<LevelBranchArea> branchBlocks = new List<LevelBranchArea>();
    public List<LevelExitRule> exitRules = new List<LevelExitRule>();
}

public sealed class LevelGateRule
{
    public string id;
    public string group;
    public string openWhen;
}

public sealed class LevelBranchArea
{
    public string branch;
    public LevelRect area;
}

public sealed class LevelExitRule
{
    public string branch;
    public string gate;
}

public sealed class LevelRegion
{
    public string id;
    public List<LevelTileRun> runs = new List<LevelTileRun>();
}

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
