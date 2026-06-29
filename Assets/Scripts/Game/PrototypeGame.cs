using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrototypeGame : MonoBehaviour
{
    private const int Width = 25;
    private const int Height = 17;
    private const float CellSize = 1f;

    private enum Tile
    {
        Floor,
        Wall,
        Exit,
        Plate,
        Gate,
        Door,
        Key,
        Trap,
    }

    private enum EnemyMode
    {
        Patrol,
        Investigate,
        Hunt,
    }

    private enum SpriteMark
    {
        None,
        Plate,
        Gate,
        Exit,
        Door,
        Key,
        Trap,
        Player,
        Stone,
        Enemy,
    }

    private sealed class Enemy
    {
        public Vector2Int Position;
        public readonly List<Vector2Int> Patrol = new List<Vector2Int>();
        public int PatrolIndex;
        public EnemyMode Mode;
        public Vector2Int LastSeen;
        public int AlertTicks;
        public int Hp = 2;
        public GameObject View;
    }

    private readonly Tile[,] tiles = new Tile[Width, Height];
    private readonly GameObject[,] tileViews = new GameObject[Width, Height];
    private readonly List<Vector2Int> stones = new List<Vector2Int>();
    private readonly List<Vector2Int> plates = new List<Vector2Int>();
    private readonly List<Enemy> enemies = new List<Enemy>();

    private Sprite floorSprite;
    private Sprite wallSprite;
    private Sprite plateSprite;
    private Sprite gateSprite;
    private Sprite openGateSprite;
    private Sprite exitSprite;
    private Sprite doorSprite;
    private Sprite keySprite;
    private Sprite trapSprite;
    private Sprite playerSprite;
    private Sprite stoneSprite;
    private Sprite enemySprite;
    private Texture2D hudTexture;

    private GameObject playerView;
    private readonly List<GameObject> stoneViews = new List<GameObject>();
    private SimpleNetworkPeer networkPeer;
    private Vector2Int playerPosition;
    private Vector2Int exitPosition;
    private Vector2Int lastNoisePosition;
    private int lastNoisePower;
    private int floorIndex;
    private int playerHp = 6;
    private int turn;
    private bool gateOpen;
    private bool gameEnded;
    private bool hasKey;
    private string levelName = "Pressure Hall";
    private string blockedMessage;
    private string message = "Find a way through the sealed gate.";

    private void Awake()
    {
        Application.targetFrameRate = 60;
        SetupCamera();
        networkPeer = gameObject.AddComponent<SimpleNetworkPeer>();
        CreateSprites();
        BuildLevel();
        CreateViews();
        RedrawAll();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Restart();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (gameEnded)
            return;

        Vector2Int move = ReadMove();
        if (move != Vector2Int.zero)
            TakePlayerTurn(move);
    }

    private void OnGUI()
    {
        GUI.color = Color.white;
        EnsureHudTexture();
        GUI.DrawTexture(new Rect(10, 10, Mathf.Min(940, Screen.width - 20), 108), hudTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white },
        };

        GUI.Label(new Rect(18, 14, 900, 24), $"Floor {floorIndex + 1}: {levelName}   HP {playerHp}   Turn {turn}   Key {(hasKey ? "yes" : "no")}   Gate {(gateOpen ? "open" : "sealed")}", style);
        GUI.Label(new Rect(18, 40, 900, 24), $"Session {NetworkSessionConfig.Describe()}   Net: {(networkPeer == null ? "not started" : networkPeer.Status)}", style);
        GUI.Label(new Rect(18, 66, 900, 24), message, style);

        var hintStyle = new GUIStyle(style)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.74f, 0.78f, 0.82f) },
        };
        GUI.Label(new Rect(18, 92, 900, 22), "Watch enemy colors: white patrol, amber investigates noise, red hunts.", hintStyle);
        GUI.Label(new Rect(16, Screen.height - 34, 1100, 24), "WASD/arrows: move, attack, push stones, pick keys | R: restart floor | Esc: menu", hintStyle);
    }

    private void Restart()
    {
        foreach (Enemy enemy in enemies)
            Destroy(enemy.View);

        foreach (GameObject stoneView in stoneViews)
            Destroy(stoneView);

        enemies.Clear();
        stones.Clear();
        plates.Clear();
        stoneViews.Clear();
        playerHp = 6;
        turn = 0;
        gateOpen = false;
        gameEnded = false;
        hasKey = false;
        lastNoisePower = 0;
        message = IntroForFloor(floorIndex);

        BuildLevel();
        CreateEntityViews();
        RedrawAll();
    }

    private static Vector2Int ReadMove()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            return Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            return Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            return Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            return Vector2Int.right;
        return Vector2Int.zero;
    }

    private void TakePlayerTurn(Vector2Int direction)
    {
        blockedMessage = null;
        Vector2Int target = playerPosition + direction;
        Enemy enemy = EnemyAt(target);

        if (enemy != null)
        {
            MakeNoise(playerPosition, 7);
            enemy.Hp--;
            message = enemy.Hp <= 0 ? "Enemy collapsed." : "Enemy staggered.";
            if (enemy.Hp <= 0)
            {
                Destroy(enemy.View);
                enemies.Remove(enemy);
            }
        }
        else if (TryMovePlayer(target, direction, out bool pushedStone, out bool specialMessage))
        {
            playerPosition = target;
            MakeNoise(playerPosition, pushedStone ? 8 : 3);
            if (gameEnded)
            {
                RedrawAll();
                return;
            }

            if (!specialMessage)
                message = gateOpen ? "The exit route is open." : "The stone plates control the gate.";
        }
        else
        {
            message = string.IsNullOrEmpty(blockedMessage) ? "Blocked." : blockedMessage;
            return;
        }

        turn++;
        UpdatePuzzle();

        if (playerPosition == exitPosition && gateOpen)
        {
            if (floorIndex >= 2)
            {
                gameEnded = true;
                message = "You cleared the handcrafted slice. Press R to replay this floor.";
            }
            else
            {
                floorIndex++;
                message = "You descend deeper.";
                Restart();
            }
            RedrawAll();
            return;
        }

        TakeEnemyTurns();
        UpdatePuzzle();
        if (lastNoisePower > 0)
            lastNoisePower--;
        RedrawAll();
    }

    private bool TryMovePlayer(Vector2Int target, Vector2Int direction)
    {
        return TryMovePlayer(target, direction, out _, out _);
    }

    private bool TryMovePlayer(Vector2Int target, Vector2Int direction, out bool pushedStone, out bool specialMessage)
    {
        pushedStone = false;
        specialMessage = false;
        int stoneIndex = stones.IndexOf(target);
        if (stoneIndex >= 0)
        {
            Vector2Int pushed = target + direction;
            if (!CanEnter(pushed, false) || stones.Contains(pushed) || EnemyAt(pushed) != null)
                return false;

            stones[stoneIndex] = pushed;
            pushedStone = true;
            return true;
        }

        if (!Inside(target))
            return false;

        Tile tile = tiles[target.x, target.y];
        if (tile == Tile.Door)
        {
            if (!hasKey)
            {
                blockedMessage = "A locked door. Find a key.";
                return false;
            }

            hasKey = false;
            tiles[target.x, target.y] = Tile.Floor;
            MakeNoise(target, 6);
            message = "The key turns and the door opens.";
            specialMessage = true;
            return true;
        }

        if (!CanEnter(target, true))
            return false;

        if (tile == Tile.Key)
        {
            hasKey = true;
            tiles[target.x, target.y] = Tile.Floor;
            message = "You picked up a brass key.";
            specialMessage = true;
        }
        else if (tile == Tile.Trap)
        {
            playerHp--;
            MakeNoise(target, 9);
            message = playerHp <= 0 ? "The trap got you. Press R to retry." : "A floor needle snaps upward.";
            gameEnded = playerHp <= 0;
            specialMessage = true;
        }

        return true;
    }

    private void TakeEnemyTurns()
    {
        foreach (Enemy enemy in enemies.ToArray())
        {
            if (IsAdjacent(enemy.Position, playerPosition))
            {
                playerHp--;
                message = playerHp <= 0 ? "You fell. Press R to rebuild the floor." : "An enemy strikes.";
                gameEnded = playerHp <= 0;
                if (gameEnded)
                    return;
                continue;
            }

            UpdateEnemyState(enemy);
            Vector2Int next = ChooseEnemyStep(enemy);
            if (next != enemy.Position && CanEnemyEnter(next))
                enemy.Position = next;

            if (IsAdjacent(enemy.Position, playerPosition))
            {
                playerHp--;
                message = playerHp <= 0 ? "You fell. Press R to rebuild the floor." : "An enemy closes in and hits.";
                gameEnded = playerHp <= 0;
                if (gameEnded)
                    return;
            }
        }
    }

    private void UpdateEnemyState(Enemy enemy)
    {
        if (CanSeePlayer(enemy.Position))
        {
            enemy.Mode = EnemyMode.Hunt;
            enemy.LastSeen = playerPosition;
            enemy.AlertTicks = 4;
            return;
        }

        if (lastNoisePower > 0 && Manhattan(enemy.Position, lastNoisePosition) <= lastNoisePower)
        {
            enemy.Mode = EnemyMode.Investigate;
            enemy.LastSeen = lastNoisePosition;
            enemy.AlertTicks = Mathf.Max(enemy.AlertTicks, 3);
            return;
        }

        if (enemy.Mode == EnemyMode.Hunt)
        {
            enemy.Mode = EnemyMode.Investigate;
            enemy.AlertTicks = Mathf.Max(enemy.AlertTicks, 3);
        }
        else if (enemy.Mode == EnemyMode.Investigate)
        {
            enemy.AlertTicks--;
            if (enemy.AlertTicks <= 0 && enemy.Position == enemy.LastSeen)
                enemy.Mode = EnemyMode.Patrol;
        }
    }

    private Vector2Int ChooseEnemyStep(Enemy enemy)
    {
        if (enemy.Mode == EnemyMode.Hunt)
            return FirstStepToward(enemy.Position, playerPosition);

        if (enemy.Mode == EnemyMode.Investigate)
            return FirstStepToward(enemy.Position, enemy.LastSeen);

        if (enemy.Patrol.Count == 0)
            return enemy.Position;

        Vector2Int target = enemy.Patrol[enemy.PatrolIndex];
        if (enemy.Position == target)
        {
            enemy.PatrolIndex = (enemy.PatrolIndex + 1) % enemy.Patrol.Count;
            target = enemy.Patrol[enemy.PatrolIndex];
        }

        return FirstStepToward(enemy.Position, target);
    }

    private void MakeNoise(Vector2Int position, int power)
    {
        lastNoisePosition = position;
        lastNoisePower = Mathf.Max(lastNoisePower, power);
    }

    private Vector2Int FirstStepToward(Vector2Int start, Vector2Int goal)
    {
        if (start == goal)
            return start;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        cameFrom[start] = start;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == goal)
                break;

            foreach (Vector2Int next in Neighbors(current))
            {
                if (cameFrom.ContainsKey(next) || !CanEnemyEnter(next) && next != goal)
                    continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!cameFrom.ContainsKey(goal))
            return GreedyStep(start, goal);

        Vector2Int step = goal;
        while (cameFrom[step] != start)
            step = cameFrom[step];
        return step;
    }

    private Vector2Int GreedyStep(Vector2Int start, Vector2Int goal)
    {
        Vector2Int best = start;
        int bestDistance = Manhattan(start, goal);

        foreach (Vector2Int next in Neighbors(start))
        {
            if (!CanEnemyEnter(next))
                continue;

            int distance = Manhattan(next, goal);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = next;
            }
        }

        return best;
    }

    private IEnumerable<Vector2Int> Neighbors(Vector2Int cell)
    {
        yield return cell + Vector2Int.up;
        yield return cell + Vector2Int.down;
        yield return cell + Vector2Int.left;
        yield return cell + Vector2Int.right;
    }

    private bool CanSeePlayer(Vector2Int from)
    {
        if (Manhattan(from, playerPosition) > 7)
            return false;

        if (from.x == playerPosition.x)
        {
            int step = Math.Sign(playerPosition.y - from.y);
            for (int y = from.y + step; y != playerPosition.y; y += step)
            {
                if (BlocksSight(new Vector2Int(from.x, y)))
                    return false;
            }
            return true;
        }

        if (from.y == playerPosition.y)
        {
            int step = Math.Sign(playerPosition.x - from.x);
            for (int x = from.x + step; x != playerPosition.x; x += step)
            {
                if (BlocksSight(new Vector2Int(x, from.y)))
                    return false;
            }
            return true;
        }

        return Manhattan(from, playerPosition) <= 3;
    }

    private bool BlocksSight(Vector2Int cell)
    {
        if (!Inside(cell))
            return true;

        Tile tile = tiles[cell.x, cell.y];
        return tile == Tile.Wall || tile == Tile.Gate && !gateOpen;
    }

    private bool CanEnemyEnter(Vector2Int cell)
    {
        return CanEnter(cell, false) && !stones.Contains(cell) && EnemyAt(cell) == null && cell != playerPosition;
    }

    private bool CanEnter(Vector2Int cell, bool allowExit)
    {
        if (!Inside(cell))
            return false;

        Tile tile = tiles[cell.x, cell.y];
        if (tile == Tile.Wall)
            return false;
        if (tile == Tile.Gate && !gateOpen)
            return false;
        if (tile == Tile.Door)
            return false;
        if (tile == Tile.Exit && !allowExit)
            return false;
        return true;
    }

    private Enemy EnemyAt(Vector2Int cell)
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy.Position == cell)
                return enemy;
        }

        return null;
    }

    private void UpdatePuzzle()
    {
        if (plates.Count == 0)
        {
            gateOpen = true;
            return;
        }

        foreach (Vector2Int plate in plates)
        {
            if (!stones.Contains(plate))
            {
                gateOpen = false;
                return;
            }
        }

        if (!gateOpen)
            message = "Both plates sink. The gate opens.";
        gateOpen = true;
    }

    private void BuildLevel()
    {
        ResetTiles();

        switch (floorIndex)
        {
            case 1:
                BuildSilentArmory();
                break;
            case 2:
                BuildVaultCrossing();
                break;
            default:
                BuildPressureHall();
                break;
        }

        UpdatePuzzle();
    }

    private void ResetTiles()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                tiles[x, y] = x == 0 || y == 0 || x == Width - 1 || y == Height - 1 ? Tile.Wall : Tile.Floor;
        }
    }

    private void BuildPressureHall()
    {
        levelName = "Pressure Hall";
        for (int x = 4; x < 20; x++)
            tiles[x, 4] = Tile.Wall;

        for (int x = 5; x < 22; x++)
            tiles[x, 12] = Tile.Wall;

        for (int y = 5; y < 12; y++)
            tiles[11, y] = Tile.Wall;

        tiles[7, 4] = Tile.Floor;
        tiles[15, 4] = Tile.Floor;
        tiles[11, 8] = Tile.Gate;
        tiles[11, 9] = Tile.Floor;
        tiles[18, 12] = Tile.Floor;
        tiles[12, 14] = Tile.Trap;
        tiles[19, 9] = Tile.Trap;

        playerPosition = new Vector2Int(2, 2);
        exitPosition = new Vector2Int(22, 14);
        tiles[exitPosition.x, exitPosition.y] = Tile.Exit;

        AddPlate(new Vector2Int(6, 9));
        AddPlate(new Vector2Int(8, 9));
        stones.Add(new Vector2Int(6, 7));
        stones.Add(new Vector2Int(8, 7));

        AddEnemy(new Vector2Int(16, 7), new Vector2Int(16, 7), new Vector2Int(21, 7), new Vector2Int(21, 10));
        AddEnemy(new Vector2Int(17, 14), new Vector2Int(17, 14), new Vector2Int(21, 14));
    }

    private void BuildSilentArmory()
    {
        levelName = "Silent Armory";
        for (int y = 2; y < 15; y++)
            tiles[8, y] = Tile.Wall;

        for (int y = 1; y < 11; y++)
            tiles[16, y] = Tile.Wall;

        for (int x = 8; x < 17; x++)
            tiles[x, 10] = Tile.Wall;

        tiles[8, 5] = Tile.Floor;
        tiles[12, 10] = Tile.Door;
        tiles[16, 4] = Tile.Floor;
        tiles[20, 12] = Tile.Wall;
        tiles[21, 12] = Tile.Wall;
        tiles[6, 13] = Tile.Key;
        tiles[5, 8] = Tile.Trap;
        tiles[12, 6] = Tile.Trap;
        tiles[18, 7] = Tile.Trap;

        playerPosition = new Vector2Int(2, 8);
        exitPosition = new Vector2Int(22, 14);
        tiles[exitPosition.x, exitPosition.y] = Tile.Exit;

        AddEnemy(new Vector2Int(12, 3), new Vector2Int(12, 3), new Vector2Int(14, 8), new Vector2Int(10, 8));
        AddEnemy(new Vector2Int(21, 5), new Vector2Int(21, 5), new Vector2Int(21, 10), new Vector2Int(18, 10));
    }

    private void BuildVaultCrossing()
    {
        levelName = "Vault Crossing";
        for (int x = 3; x < 22; x++)
            tiles[x, 6] = Tile.Wall;

        for (int x = 3; x < 22; x++)
            tiles[x, 11] = Tile.Wall;

        for (int y = 2; y < 15; y++)
            tiles[12, y] = Tile.Wall;

        tiles[6, 6] = Tile.Floor;
        tiles[12, 4] = Tile.Door;
        tiles[12, 8] = Tile.Gate;
        tiles[12, 13] = Tile.Floor;
        tiles[18, 6] = Tile.Floor;
        tiles[18, 11] = Tile.Floor;
        tiles[4, 13] = Tile.Key;
        tiles[8, 8] = Tile.Trap;
        tiles[16, 8] = Tile.Trap;
        tiles[20, 4] = Tile.Trap;

        playerPosition = new Vector2Int(2, 2);
        exitPosition = new Vector2Int(22, 14);
        tiles[exitPosition.x, exitPosition.y] = Tile.Exit;

        AddPlate(new Vector2Int(5, 9));
        AddPlate(new Vector2Int(19, 9));
        stones.Add(new Vector2Int(5, 8));
        stones.Add(new Vector2Int(19, 8));

        AddEnemy(new Vector2Int(10, 3), new Vector2Int(10, 3), new Vector2Int(4, 4), new Vector2Int(4, 9));
        AddEnemy(new Vector2Int(15, 13), new Vector2Int(15, 13), new Vector2Int(21, 13), new Vector2Int(21, 8));
        AddEnemy(new Vector2Int(20, 3), new Vector2Int(20, 3), new Vector2Int(14, 3), new Vector2Int(14, 5));
    }

    private static string IntroForFloor(int floor)
    {
        return floor switch
        {
            1 => "Find the key, cross quietly, and unlock the armory door.",
            2 => "Open the door, solve both plates, and survive the vault patrols.",
            _ => "Push both stones onto plates to open the sealed gate.",
        };
    }

    private void AddPlate(Vector2Int cell)
    {
        plates.Add(cell);
        tiles[cell.x, cell.y] = Tile.Plate;
    }

    private void AddEnemy(Vector2Int start, params Vector2Int[] patrol)
    {
        var enemy = new Enemy
        {
            Position = start,
            LastSeen = start,
            Mode = EnemyMode.Patrol,
        };
        enemy.Patrol.AddRange(patrol);
        enemies.Add(enemy);
    }

    private void CreateViews()
    {
        var tileRoot = new GameObject("Tiles");
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                GameObject view = new GameObject($"Tile {x},{y}");
                view.transform.SetParent(tileRoot.transform);
                view.transform.position = ToWorld(new Vector2Int(x, y));
                view.AddComponent<SpriteRenderer>();
                tileViews[x, y] = view;
            }
        }

        CreateEntityViews();
    }

    private void CreateEntityViews()
    {
        if (playerView == null)
        {
            playerView = new GameObject("Player");
            var renderer = playerView.AddComponent<SpriteRenderer>();
            renderer.sprite = playerSprite;
            renderer.sortingOrder = 10;
        }

        foreach (Vector2Int stone in stones)
        {
            GameObject view = new GameObject("Puzzle Stone");
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = stoneSprite;
            renderer.sortingOrder = 8;
            stoneViews.Add(view);
        }

        foreach (Enemy enemy in enemies)
        {
            enemy.View = new GameObject("Enemy");
            var renderer = enemy.View.AddComponent<SpriteRenderer>();
            renderer.sprite = enemySprite;
            renderer.sortingOrder = 9;
        }
    }

    private void RedrawAll()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                SpriteRenderer renderer = tileViews[x, y].GetComponent<SpriteRenderer>();
                renderer.sprite = SpriteFor(tiles[x, y]);
                renderer.sortingOrder = 0;
            }
        }

        playerView.transform.position = ToWorld(playerPosition);

        for (int i = 0; i < stones.Count; i++)
            stoneViews[i].transform.position = ToWorld(stones[i]);

        foreach (Enemy enemy in enemies)
        {
            enemy.View.transform.position = ToWorld(enemy.Position);
            enemy.View.GetComponent<SpriteRenderer>().color = EnemyColor(enemy.Mode);
        }
    }

    private static Color EnemyColor(EnemyMode mode)
    {
        return mode switch
        {
            EnemyMode.Hunt => new Color(1.00f, 0.12f, 0.16f),
            EnemyMode.Investigate => new Color(1.00f, 0.68f, 0.18f),
            _ => Color.white,
        };
    }

    private Sprite SpriteFor(Tile tile)
    {
        switch (tile)
        {
            case Tile.Wall:
                return wallSprite;
            case Tile.Plate:
                return plateSprite;
            case Tile.Gate:
                return gateOpen ? openGateSprite : gateSprite;
            case Tile.Door:
                return doorSprite;
            case Tile.Key:
                return keySprite;
            case Tile.Trap:
                return trapSprite;
            case Tile.Exit:
                return exitSprite;
            default:
                return floorSprite;
        }
    }

    private void CreateSprites()
    {
        floorSprite = CreateSprite(new Color(0.10f, 0.11f, 0.12f), new Color(0.16f, 0.17f, 0.18f), new Color(0.20f, 0.21f, 0.22f), SpriteMark.None);
        wallSprite = CreateSprite(new Color(0.28f, 0.30f, 0.32f), new Color(0.17f, 0.18f, 0.20f), new Color(0.42f, 0.44f, 0.46f), SpriteMark.None);
        plateSprite = CreateSprite(new Color(0.34f, 0.27f, 0.13f), new Color(0.16f, 0.14f, 0.10f), new Color(0.95f, 0.75f, 0.27f), SpriteMark.Plate);
        gateSprite = CreateSprite(new Color(0.44f, 0.12f, 0.16f), new Color(0.17f, 0.08f, 0.10f), new Color(0.88f, 0.28f, 0.32f), SpriteMark.Gate);
        openGateSprite = CreateSprite(new Color(0.12f, 0.34f, 0.27f), new Color(0.07f, 0.18f, 0.15f), new Color(0.24f, 0.74f, 0.54f), SpriteMark.Gate);
        exitSprite = CreateSprite(new Color(0.08f, 0.38f, 0.45f), new Color(0.05f, 0.16f, 0.18f), new Color(0.22f, 0.82f, 0.94f), SpriteMark.Exit);
        doorSprite = CreateSprite(new Color(0.34f, 0.20f, 0.13f), new Color(0.13f, 0.08f, 0.05f), new Color(0.84f, 0.58f, 0.30f), SpriteMark.Door);
        keySprite = CreateSprite(new Color(0.13f, 0.12f, 0.08f), new Color(0.06f, 0.06f, 0.05f), new Color(1.00f, 0.83f, 0.26f), SpriteMark.Key);
        trapSprite = CreateSprite(new Color(0.18f, 0.11f, 0.14f), new Color(0.08f, 0.06f, 0.07f), new Color(0.94f, 0.18f, 0.28f), SpriteMark.Trap);
        playerSprite = CreateSprite(new Color(0.24f, 0.34f, 0.42f), new Color(0.08f, 0.12f, 0.16f), new Color(0.78f, 0.92f, 1.00f), SpriteMark.Player);
        stoneSprite = CreateSprite(new Color(0.46f, 0.38f, 0.28f), new Color(0.20f, 0.17f, 0.13f), new Color(0.72f, 0.60f, 0.42f), SpriteMark.Stone);
        enemySprite = CreateSprite(new Color(0.34f, 0.12f, 0.16f), new Color(0.12f, 0.06f, 0.08f), new Color(0.95f, 0.24f, 0.30f), SpriteMark.Enemy);
    }

    private static Sprite CreateSprite(Color baseColor, Color edgeColor, Color markColor, SpriteMark mark)
    {
        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                bool checker = (x + y) % 7 == 0;
                Color color = edge ? edgeColor : checker ? Color.Lerp(baseColor, markColor, 0.08f) : baseColor;
                texture.SetPixel(x, y, color);
            }
        }

        DrawMark(texture, mark, markColor);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static void DrawMark(Texture2D texture, SpriteMark mark, Color color)
    {
        switch (mark)
        {
            case SpriteMark.Plate:
                DrawRect(texture, 4, 4, 8, 8, color, false);
                DrawRect(texture, 6, 6, 4, 4, color, true);
                break;
            case SpriteMark.Gate:
                for (int x = 4; x <= 11; x += 3)
                    DrawLine(texture, x, 3, x, 12, color);
                break;
            case SpriteMark.Exit:
                DrawRect(texture, 4, 4, 8, 8, color, false);
                DrawLine(texture, 6, 8, 10, 8, color);
                DrawLine(texture, 8, 6, 10, 8, color);
                DrawLine(texture, 8, 10, 10, 8, color);
                break;
            case SpriteMark.Door:
                DrawRect(texture, 4, 3, 8, 10, color, false);
                SetSafe(texture, 10, 8, color);
                break;
            case SpriteMark.Key:
                DrawRect(texture, 4, 8, 4, 4, color, false);
                DrawLine(texture, 8, 10, 12, 10, color);
                SetSafe(texture, 11, 8, color);
                SetSafe(texture, 12, 8, color);
                break;
            case SpriteMark.Trap:
                DrawLine(texture, 4, 4, 11, 11, color);
                DrawLine(texture, 11, 4, 4, 11, color);
                break;
            case SpriteMark.Player:
                DrawLine(texture, 8, 3, 12, 8, color);
                DrawLine(texture, 12, 8, 8, 12, color);
                DrawLine(texture, 8, 12, 4, 8, color);
                DrawLine(texture, 4, 8, 8, 3, color);
                break;
            case SpriteMark.Stone:
                DrawRect(texture, 4, 4, 8, 8, color, true);
                break;
            case SpriteMark.Enemy:
                DrawRect(texture, 4, 5, 8, 6, color, false);
                SetSafe(texture, 6, 8, Color.black);
                SetSafe(texture, 9, 8, Color.black);
                break;
        }
    }

    private static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color color, bool filled)
    {
        for (int ix = x; ix < x + width; ix++)
        {
            for (int iy = y; iy < y + height; iy++)
            {
                if (filled || ix == x || iy == y || ix == x + width - 1 || iy == y + height - 1)
                    SetSafe(texture, ix, iy, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            SetSafe(texture, x0, y0, color);
            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && y >= 0 && x < texture.width && y < texture.height)
            texture.SetPixel(x, y, color);
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 9.5f;
        camera.transform.position = new Vector3((Width - 1) * 0.5f, (Height - 1) * 0.5f, -10f);
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
    }

    private void EnsureHudTexture()
    {
        if (hudTexture != null)
            return;

        hudTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        hudTexture.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.06f, 0.88f));
        hudTexture.Apply();
    }

    private static Vector3 ToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * CellSize, cell.y * CellSize, 0f);
    }

    private static bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Manhattan(a, b) == 1;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }

    private static bool Inside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }
}
