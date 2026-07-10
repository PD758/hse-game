using System.Collections.Generic;
using UnityEngine;

public sealed partial class PrototypeGame
{
    private const int EndlessRoomsPerLevel = 5;

    private readonly struct EndlessRoom
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public EndlessRoom(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public Vector2Int Center => new Vector2Int((MinX + MaxX) / 2, (MinY + MaxY) / 2);
    }

    private LevelDefinition GenerateEndlessLevelDefinition()
    {
        var rng = new System.Random(EndlessRunState.SeedForCurrentLevel());
        var floors = new bool[Width, Height];
        var rooms = BuildEndlessRooms(rng);

        foreach (EndlessRoom room in rooms)
            CarveEndlessRoom(floors, room);

        for (int i = 1; i < rooms.Count; i++)
            CarveEndlessCorridor(floors, rooms[i - 1].Center, rooms[i].Center, rng.Next(0, 2) == 0);

        var occupied = new HashSet<Vector2Int>();
        Vector2Int start = rooms[0].Center;
        Vector2Int exit = new Vector2Int(Mathf.Max(rooms[rooms.Count - 1].MinX + 1, rooms[rooms.Count - 1].MaxX - 1), rooms[rooms.Count - 1].Center.y);
        occupied.Add(start);
        occupied.Add(exit);

        var level = new LevelDefinition
        {
            version = 1,
            id = EndlessRunState.CurrentLevelId,
            size = new LevelSize { width = Width, height = Height },
            playerStart = new LevelPoint(start.x, start.y),
            exits = new List<LevelExit>
            {
                new LevelExit
                {
                    id = "endless_exit",
                    branch = "none",
                    x = exit.x,
                    y = exit.y,
                },
            },
            tiles = BuildEndlessFloorRuns(floors),
            objects = new List<LevelObject>(),
            enemies = new List<LevelEnemy>(),
            lights = new List<LevelLight>(),
        };

        AddEndlessObjects(level, rooms, occupied, rng);
        AddEndlessEnemies(level, rooms, occupied, rng);
        AddEndlessLights(level, rooms);
        return level;
    }

    private List<EndlessRoom> BuildEndlessRooms(System.Random rng)
    {
        var rooms = new List<EndlessRoom>(EndlessRoomsPerLevel);
        for (int i = 0; i < EndlessRoomsPerLevel; i++)
        {
            float t = EndlessRoomsPerLevel <= 1 ? 0f : i / (float)(EndlessRoomsPerLevel - 1);
            int centerX = Mathf.RoundToInt(Mathf.Lerp(5f, Width - 6f, t)) + rng.Next(-2, 3);
            int centerY = rng.Next(5, Height - 5);
            int roomWidth = rng.Next(5, 9);
            int roomHeight = rng.Next(5, 9);

            int minX = Mathf.Clamp(centerX - roomWidth / 2, 1, Width - roomWidth - 2);
            int minY = Mathf.Clamp(centerY - roomHeight / 2, 1, Height - roomHeight - 2);
            rooms.Add(new EndlessRoom(minX, minY, minX + roomWidth - 1, minY + roomHeight - 1));
        }

        return rooms;
    }

    private static void CarveEndlessRoom(bool[,] floors, EndlessRoom room)
    {
        for (int x = room.MinX; x <= room.MaxX; x++)
        {
            for (int y = room.MinY; y <= room.MaxY; y++)
                floors[x, y] = true;
        }
    }

    private static void CarveEndlessCorridor(bool[,] floors, Vector2Int from, Vector2Int to, bool horizontalFirst)
    {
        if (horizontalFirst)
        {
            CarveEndlessLine(floors, from.x, to.x, from.y, true);
            CarveEndlessLine(floors, from.y, to.y, to.x, false);
            return;
        }

        CarveEndlessLine(floors, from.y, to.y, from.x, false);
        CarveEndlessLine(floors, from.x, to.x, to.y, true);
    }

    private static void CarveEndlessLine(bool[,] floors, int from, int to, int fixedAxis, bool horizontal)
    {
        int start = Mathf.Min(from, to);
        int end = Mathf.Max(from, to);
        for (int value = start; value <= end; value++)
        {
            int x = horizontal ? value : fixedAxis;
            int y = horizontal ? fixedAxis : value;
            if (x > 0 && x < Width - 1 && y > 0 && y < Height - 1)
                floors[x, y] = true;
        }
    }

    private static List<LevelTileRun> BuildEndlessFloorRuns(bool[,] floors)
    {
        var runs = new List<LevelTileRun>();
        for (int y = 0; y < Height; y++)
        {
            int x = 0;
            while (x < Width)
            {
                while (x < Width && !floors[x, y])
                    x++;

                int start = x;
                while (x < Width && floors[x, y])
                    x++;

                int length = x - start;
                if (length > 0)
                {
                    runs.Add(new LevelTileRun
                    {
                        tile = "floor",
                        x = start,
                        y = y,
                        length = length,
                        variant = -1,
                    });
                }
            }
        }

        return runs;
    }

    private void AddEndlessObjects(LevelDefinition level, List<EndlessRoom> rooms, HashSet<Vector2Int> occupied, System.Random rng)
    {
        AddEndlessAbilityPickup(level, rooms, occupied, rng);

        if (rng.NextDouble() < 0.72)
            AddEndlessObject(level, "heal", rooms[rng.Next(1, rooms.Count)], occupied, rng);

        int trapCount = Mathf.Clamp(1 + EndlessRunState.Level / 2, 1, 7);
        for (int i = 0; i < trapCount; i++)
            AddEndlessObject(level, "trap", rooms[rng.Next(1, rooms.Count)], occupied, rng);
    }

    private void AddEndlessAbilityPickup(LevelDefinition level, List<EndlessRoom> rooms, HashSet<Vector2Int> occupied, System.Random rng)
    {
        EndlessRoom pickupRoom = rooms[Mathf.Min(1, rooms.Count - 1)];
        if (HasRemote)
        {
            if (rng.NextDouble() < 0.32)
                AddEndlessObject(level, "flashlight", pickupRoom, occupied, rng);
            return;
        }

        if (HasFlashlight)
        {
            float remoteChance = EndlessRunState.Level == 1 ? 0.24f : 0.12f;
            if (rng.NextDouble() < remoteChance)
                AddEndlessObject(level, "remote", pickupRoom, occupied, rng);
            return;
        }

        double roll = rng.NextDouble();
        float firstLevelBonus = EndlessRunState.Level == 1 ? 0.12f : 0f;
        if (roll < 0.16 + firstLevelBonus)
            AddEndlessObject(level, "remote", pickupRoom, occupied, rng);
        else if (roll < 0.52 + firstLevelBonus)
            AddEndlessObject(level, "flashlight", pickupRoom, occupied, rng);
    }

    private void AddEndlessObject(LevelDefinition level, string type, EndlessRoom room, HashSet<Vector2Int> occupied, System.Random rng)
    {
        if (!TryPickEndlessCell(room, occupied, rng, out Vector2Int cell))
            return;

        occupied.Add(cell);
        var obj = new LevelObject
        {
            type = type,
            id = $"{type}_{cell.x}_{cell.y}",
            x = cell.x,
            y = cell.y,
            variant = -1,
        };

        if (type == "trap")
        {
            Vector2Int rawDirection = room.Center - cell;
            Vector2 direction = DirectionOrFallback(new Vector2(rawDirection.x, rawDirection.y), Vector2.down);
            obj.direction = new LevelDirection { x = direction.x, y = direction.y };
        }

        level.objects.Add(obj);
    }

    private void AddEndlessEnemies(LevelDefinition level, List<EndlessRoom> rooms, HashSet<Vector2Int> occupied, System.Random rng)
    {
        int floor = EndlessRunState.Level;
        for (int roomIndex = 1; roomIndex < rooms.Count; roomIndex++)
        {
            int enemiesInRoom = 1 + (floor + roomIndex) / 4;
            if (rng.NextDouble() < Mathf.Clamp01(0.18f + floor * 0.025f))
                enemiesInRoom++;
            enemiesInRoom = Mathf.Clamp(enemiesInRoom, 1, 4);

            for (int enemyIndex = 0; enemyIndex < enemiesInRoom; enemyIndex++)
            {
                if (!TryPickEndlessCell(rooms[roomIndex], occupied, rng, out Vector2Int cell))
                    continue;

                occupied.Add(cell);
                Vector2Int patrol = PickEndlessPatrolCell(rooms[roomIndex], cell, rng);
                int enemyLevel = Mathf.Clamp(EnemyBaseLevel + floor - 1 + roomIndex / 2 + rng.Next(-1, 2), EnemyMinLevel, EnemyMaxLevel);
                int hp = Mathf.Clamp(2 + (floor - 1) / 2 + roomIndex / 3, 2, 12);
                string enemyType = PickEndlessEnemyType(floor, roomIndex, enemyIndex, rng);
                if (enemyType == "brute")
                    hp = Mathf.Clamp(hp + 1, 2, 14);
                else if (enemyType == "caller")
                    hp = Mathf.Clamp(hp - 1, 1, 12);
                level.enemies.Add(new LevelEnemy
                {
                    id = $"endless_l{floor}_r{roomIndex}_e{enemyIndex}",
                    type = enemyType,
                    group = $"room_{roomIndex}",
                    alertGroup = $"room_{roomIndex}",
                    branch = "combat",
                    level = enemyLevel,
                    hp = hp,
                    x = cell.x,
                    y = cell.y,
                    patrol = new List<List<int>>
                    {
                        new List<int> { patrol.x, patrol.y },
                    },
                });
            }
        }
    }

    private static string PickEndlessEnemyType(int floor, int roomIndex, int enemyIndex, System.Random rng)
    {
        if (floor <= 2)
            return "patrol";

        double roll = rng.NextDouble();
        if (floor <= 5)
            return roll < Mathf.Clamp01(0.22f + roomIndex * 0.03f) ? "hunter" : "patrol";

        float callerChance = Mathf.Clamp01(0.04f + (floor - 6) * 0.012f);
        if (enemyIndex == 0 && roomIndex >= 2 && roll < callerChance)
            return "caller";
        if (roll < callerChance + Mathf.Clamp01(0.18f + floor * 0.012f))
            return "hunter";
        if (roll < callerChance + Mathf.Clamp01(0.32f + floor * 0.018f))
            return "brute";

        return "patrol";
    }

    private static Vector2Int PickEndlessPatrolCell(EndlessRoom room, Vector2Int start, System.Random rng)
    {
        for (int i = 0; i < 8; i++)
        {
            var cell = new Vector2Int(rng.Next(room.MinX + 1, room.MaxX), rng.Next(room.MinY + 1, room.MaxY));
            if (Manhattan(cell, start) >= 2)
                return cell;
        }

        return room.Center;
    }

    private static bool TryPickEndlessCell(EndlessRoom room, HashSet<Vector2Int> occupied, System.Random rng, out Vector2Int cell)
    {
        for (int i = 0; i < 32; i++)
        {
            cell = new Vector2Int(rng.Next(room.MinX + 1, room.MaxX), rng.Next(room.MinY + 1, room.MaxY));
            if (!occupied.Contains(cell))
                return true;
        }

        cell = room.Center;
        return !occupied.Contains(cell);
    }

    private static void AddEndlessLights(LevelDefinition level, List<EndlessRoom> rooms)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            Vector2Int center = rooms[i].Center;
            level.lights.Add(new LevelLight
            {
                id = $"endless_light_{i}",
                type = "point",
                x = center.x,
                y = center.y,
                intensity = i == 0 ? 0.36f : 0.24f,
                radius = 4.6f,
                color = i == rooms.Count - 1 ? "#ffd8d2" : "#d6f0ff",
            });
        }
    }

    private void LoadNextEndlessLevel()
    {
        ClearLevelEntityViews();
        EndlessRunState.AdvanceLevel();
        currentLevelId = EndlessRunState.CurrentLevelId;
        ResetLevelLocalState();
        playerHp = Mathf.Min(6, playerHp + 1);
        viewerRating = Mathf.Min(100f, viewerRating + 18f);

        BuildLevel();
        if (!enabled)
            return;

        CreateEntityViews();
        currentVelocity = Vector2.zero;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        EnsureGameplayLighting();
        UpdatePostProcessing();
        RedrawAll();
        RebuildTileColliders();
        CaptureLevelRestartState();
        EvaluateEvents("levelStart", null, null);
        SpawnHitBurst(ToWorld(playerStart), false);
        message = $"Бесконечный эфир: уровень {EndlessRunState.Level}. Комнат: {EndlessRoomsPerLevel}. Враги стали сильнее.";
    }
}
