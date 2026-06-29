using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrototypeGame : MonoBehaviour
{
    private const int Width = 39;
    private const int Height = 21;
    private const float CellSize = 1f;
    private const float PlayerSpeed = 4.6f;
    private const float PlayerAcceleration = 24f;
    private const float PlayerDeceleration = 18f;
    private const float RatingCritical = 15f;
    private const int AtlasColumns = 16;
    private const int AtlasCellSize = 128;
    private const int AtlasInset = 4;

    public Texture2D SpriteAtlas;

    private enum Tile
    {
        Floor,
        Wall,
        Exit,
        Plate,
        Gate,
        Rubble,
        Trap,
        Remote,
        Story,
    }

    private enum EnemyMode
    {
        Patrol,
        Investigate,
        Hunt,
    }

    private enum FacingDirection
    {
        Down,
        Up,
        Left,
        Right,
    }

    private enum SpriteMark
    {
        None,
        Plate,
        Gate,
        Exit,
        Rubble,
        Trap,
        Remote,
        Story,
        Player,
        Stone,
        Enemy,
    }

    private sealed class Stone
    {
        public Vector2Int Cell;
        public Vector3 Target;
        public bool Moving;
        public GameObject View;
    }

    private sealed class Enemy
    {
        public Vector2 Position;
        public readonly List<Vector2Int> Patrol = new List<Vector2Int>();
        public int PatrolIndex;
        public EnemyMode Mode;
        public Vector2Int LastSeen;
        public BranchChoice Branch;
        public int Hp = 2;
        public float AttackCooldown;
        public GameObject View;
    }

    private readonly Tile[,] tiles = new Tile[Width, Height];
    private readonly GameObject[,] floorViews = new GameObject[Width, Height];
    private readonly GameObject[,] tileViews = new GameObject[Width, Height];
    private readonly List<Vector2Int> startPlates = new List<Vector2Int>();
    private readonly List<Vector2Int> puzzlePlates = new List<Vector2Int>();
    private readonly List<Stone> stones = new List<Stone>();
    private readonly List<Enemy> enemies = new List<Enemy>();

    private Sprite floorSprite;
    private Sprite wallSprite;
    private Sprite plateSprite;
    private Sprite gateSprite;
    private Sprite openGateSprite;
    private Sprite exitSprite;
    private Sprite rubbleSprite;
    private Sprite trapSprite;
    private Sprite remoteSprite;
    private Sprite storySprite;
    private Sprite playerSprite;
    private readonly Sprite[] playerIdleSprites = new Sprite[4];
    private readonly Sprite[] playerWalkOneSprites = new Sprite[4];
    private readonly Sprite[] playerWalkTwoSprites = new Sprite[4];
    private readonly Sprite[] playerAttackSprites = new Sprite[4];
    private Sprite stoneSprite;
    private Sprite enemySprite;
    private Sprite enemyInvestigateSprite;
    private Sprite enemyHuntSprite;
    private Texture2D hudTexture;
    private Texture2D whiteTexture;
    private int[] atlasVerticalLines;
    private int[] atlasHorizontalLines;

    private GameObject playerView;
    private Rigidbody2D playerBody;
    private Vector2 moveInput;
    private Vector2 currentVelocity;
    private Vector2 lastAim = Vector2.right;
    private Vector2Int exitPosition;
    private Vector2Int lastNoiseCell;
    private int lastNoisePower;
    private int playerHp = 6;
    private float viewerRating = 100f;
    private float idleTimer;
    private float criticalDamageTimer;
    private float attackCooldown;
    private bool startGateOpen;
    private bool puzzleExitOpen;
    private bool combatExitOpen;
    private bool storyRead;
    private bool hasRemote;
    private bool gameEnded;
    private string message = "Канал требует внимания. Соберите сигнал и выберите, как смотреть дальше.";

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Physics2D.gravity = Vector2.zero;
        SetupCamera();
        CreateSprites();
        BuildLevel();
        CreateViews();
        CreateLighting();
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

        ReadMoveInput();
        UpdatePlayerSprite();

        if (gameEnded)
            return;

        UpdateRating(Time.deltaTime);
        UpdateStoneMotion(Time.deltaTime);
        UpdateEnemies(Time.deltaTime);
        UpdateWorldInteractions();

        if (attackCooldown > 0f)
            attackCooldown -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.E))
            TryInteract();

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            TryAttack();
    }

    private void FixedUpdate()
    {
        if (gameEnded || playerBody == null)
        {
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetVelocity = moveInput * PlayerSpeed;
        float rate = moveInput.sqrMagnitude > 0.01f ? PlayerAcceleration : PlayerDeceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        playerBody.linearVelocity = currentVelocity;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null || playerView == null)
            return;

        Vector3 target = playerView.transform.position;
        target.z = -10f;
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        target.x = ClampCameraAxis(target.x, halfWidth - 0.5f, (Width - 1) * CellSize - halfWidth + 0.5f);
        target.y = ClampCameraAxis(target.y, halfHeight - 0.5f, (Height - 1) * CellSize - halfHeight + 0.5f);
        camera.transform.position = Vector3.Lerp(camera.transform.position, target, 0.12f);
    }

    private void OnGUI()
    {
        EnsureHudTextures();
        GUI.color = Color.white;
        float hudWidth = Mathf.Min(960f, Screen.width - 20f);
        float hudHeight = Screen.width < 760 ? 150f : 126f;
        GUI.DrawTexture(new Rect(10, 10, hudWidth, hudHeight), hudTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Screen.width < 760 ? 13 : 16,
            wordWrap = true,
            normal = { textColor = Color.white },
        };

        float textWidth = hudWidth - 16f;
        GUI.Label(new Rect(18, 14, textWidth, 24), $"Канал 01: Новости   HP {playerHp}   Ветка {BranchName()}   Пульт {(hasRemote ? "есть" : "нет")}", style);
        GUI.Label(new Rect(18, 42, textWidth, 42), message, style);

        var hintStyle = new GUIStyle(style)
        {
            fontSize = Screen.width < 760 ? 12 : 14,
            normal = { textColor = new Color(0.74f, 0.78f, 0.82f) },
        };
        GUI.Label(new Rect(18, 74, textWidth, 36), NarrativeRunState.SignalHint(), hintStyle);

        float ratingY = Screen.width < 760 ? 122f : 100f;
        float ratingWidth = Mathf.Min(320f, hudWidth - 220f);
        DrawRatingBar(new Rect(18, ratingY, Mathf.Max(160f, ratingWidth), 18));
        GUI.Label(new Rect(26f + Mathf.Max(160f, ratingWidth), ratingY - 4f, hudWidth - ratingWidth - 40f, 24), $"Рейтинг {Mathf.CeilToInt(viewerRating)}%", hintStyle);

        string controls = Screen.width < 900
            ? "WASD/стрелки: движение | Space/ЛКМ: атака | E: действие | R: рестарт | Esc: меню"
            : "WASD/стрелки: двигаться | Space/ЛКМ: атаковать | E: взаимодействовать/толкать | R: перезапуск | Esc: меню";
        GUI.Label(new Rect(16, Screen.height - 50, Screen.width - 32, 44), controls, hintStyle);
    }

    private void DrawRatingBar(Rect rect)
    {
        GUI.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);
        GUI.DrawTexture(rect, whiteTexture);

        GUI.color = RatingColor();
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(viewerRating / 100f), rect.height), whiteTexture);

        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), whiteTexture);
        GUI.color = Color.white;
    }

    private Color RatingColor()
    {
        Color tone = NarrativeRunState.IsAggressive() ? new Color(1.00f, 0.16f, 0.14f) : new Color(0.60f, 0.88f, 1.00f);
        return Color.Lerp(Color.white, tone, 1f - viewerRating / 100f);
    }

    private string BranchName()
    {
        return NarrativeRunState.Branch switch
        {
            BranchChoice.Puzzle => "разбор",
            BranchChoice.Combat => "агрессия",
            _ => "не выбрана",
        };
    }

    private void Restart()
    {
        NarrativeRunState.Reset();

        foreach (Enemy enemy in enemies)
            Destroy(enemy.View);
        foreach (Stone stone in stones)
            Destroy(stone.View);

        enemies.Clear();
        stones.Clear();
        startPlates.Clear();
        puzzlePlates.Clear();
        playerHp = 6;
        viewerRating = 100f;
        idleTimer = 0f;
        criticalDamageTimer = 0f;
        attackCooldown = 0f;
        startGateOpen = false;
        puzzleExitOpen = false;
        combatExitOpen = false;
        storyRead = false;
        hasRemote = false;
        gameEnded = false;
        lastNoisePower = 0;
        currentVelocity = Vector2.zero;
        message = "Канал требует внимания. Соберите сигнал и выберите, как смотреть дальше.";

        BuildLevel();
        CreateEntityViews();
        RedrawAll();
        RebuildTileColliders();
    }

    private void ReadMoveInput()
    {
        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            y += 1f;

        moveInput = new Vector2(x, y);
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        if (moveInput.sqrMagnitude > 0.01f)
        {
            lastAim = moveInput.normalized;
            MarkActivity();
        }
    }

    private void MarkActivity()
    {
        idleTimer = 0f;
    }

    private void UpdateRating(float dt)
    {
        if (moveInput.sqrMagnitude <= 0.01f)
            idleTimer += dt;

        if (idleTimer > 2f)
        {
            float drain = Mathf.Min(18f, 3f + (idleTimer - 2f) * 1.65f);
            viewerRating = Mathf.Max(0f, viewerRating - drain * dt);
        }

        if (viewerRating <= RatingCritical)
        {
            criticalDamageTimer += dt;
            if (criticalDamageTimer >= 1.4f)
            {
                criticalDamageTimer = 0f;
                DamagePlayer(1, "Рейтинг проваливается. Канал начинает стирать вас из кадра.");
            }
        }
        else
        {
            criticalDamageTimer = 0f;
        }
    }

    private void RestoreRating(float amount)
    {
        viewerRating = Mathf.Min(100f, viewerRating + amount);
    }

    private void TryInteract()
    {
        MarkActivity();

        if (TryPushStone())
            return;

        Vector2Int cell = PlayerCell();
        foreach (Vector2Int next in NeighborCells(cell))
        {
            if (!Inside(next))
                continue;

            Tile tile = tiles[next.x, next.y];
            if (tile == Tile.Remote)
            {
                hasRemote = true;
                tiles[next.x, next.y] = Tile.Floor;
                NarrativeRunState.RecordSignalInsight();
                RestoreRating(10f);
                message = "Пульт щёлкает в руке. Канал на миг теряет власть.";
                RedrawTile(next);
                return;
            }

            if (tile == Tile.Story && NarrativeRunState.Branch == BranchChoice.Puzzle)
            {
                storyRead = true;
                tiles[next.x, next.y] = Tile.Floor;
                NarrativeRunState.RecordPuzzleReflection();
                RestoreRating(18f);
                message = "В монтажной заметке написано: смотреть не значит соглашаться.";
                RedrawTile(next);
                UpdatePuzzle();
                return;
            }
        }

        message = "Здесь нечего переключить.";
    }

    private bool TryPushStone()
    {
        Vector2Int direction = Cardinal(lastAim);
        if (direction == Vector2Int.zero)
            return false;

        Vector2Int from = PlayerCell();
        Stone stone = StoneAt(from + direction);
        if (stone == null || stone.Moving)
            return false;

        Vector2Int destination = stone.Cell + direction;
        if (!CanStoneEnter(destination))
        {
            message = "Заглушка сигнала упирается в эфир.";
            return true;
        }

        stone.Cell = destination;
        stone.Target = ToWorld(destination);
        stone.Moving = true;
        MakeNoise(PlayerCell(), 5);
        RestoreRating(3f);
        message = "Заглушка скользит на соседнюю метку.";
        UpdatePuzzle();
        return true;
    }

    private void TryAttack()
    {
        MarkActivity();
        if (attackCooldown > 0f)
            return;

        attackCooldown = 0.34f;
        MakeNoise(PlayerCell(), 8);
        NarrativeRunState.RecordAttack();
        RestoreRating(NarrativeRunState.IsAggressive() ? 6f : 3f);

        Enemy target = null;
        float best = 1.35f;
        Vector2 player = playerView.transform.position;
        foreach (Enemy enemy in enemies)
        {
            float distance = Vector2.Distance(player, enemy.Position);
            if (distance > best)
                continue;

            Vector2 toEnemy = (enemy.Position - player).normalized;
            if (Vector2.Dot(lastAim, toEnemy) < 0.2f)
                continue;

            target = enemy;
            best = distance;
        }

        if (target == null)
        {
            message = "Удар рассыпался в пустом эфире.";
            return;
        }

        target.Hp--;
        target.Mode = EnemyMode.Hunt;
        target.LastSeen = PlayerCell();
        message = target.Hp <= 0 ? "Диктор рассыпался в белый шум." : "Диктор сбился с текста.";

        if (target.Hp <= 0)
        {
            NarrativeRunState.RecordKill();
            RestoreRating(12f);
            Destroy(target.View);
            enemies.Remove(target);
            UpdateBranchObjective();
        }
    }

    private void DamagePlayer(int amount, string text)
    {
        if (gameEnded)
            return;

        playerHp -= amount;
        message = playerHp <= 0 ? "Game Over: эфир оставил только шум. Нажмите R, чтобы пересмотреть канал." : text;
        if (playerHp <= 0)
        {
            gameEnded = true;
            currentVelocity = Vector2.zero;
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateWorldInteractions()
    {
        Vector2Int playerCell = PlayerCell();
        Tile tile = tiles[playerCell.x, playerCell.y];

        if (tile == Tile.Trap)
        {
            tiles[playerCell.x, playerCell.y] = Tile.Floor;
            NarrativeRunState.RecordTrapMistake();
            MakeNoise(playerCell, 9);
            DamagePlayer(1, "Камера ослепляет вспышкой. Рейтинг вздрагивает.");
            viewerRating = Mathf.Max(0f, viewerRating - 8f);
            RedrawTile(playerCell);
        }

        if (startGateOpen && NarrativeRunState.Branch == BranchChoice.None)
        {
            if (playerCell.x >= 19 && playerCell.y >= 12)
                ChooseBranch(BranchChoice.Puzzle);
            else if (playerCell.x >= 19 && playerCell.y <= 8)
                ChooseBranch(BranchChoice.Combat);
        }

        if (tile == Tile.Exit && CanUseExit())
        {
            gameEnded = true;
            currentVelocity = Vector2.zero;
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;
            message = NarrativeRunState.ChannelClosingLine() + " Нажмите R, чтобы пересмотреть канал.";
        }
    }

    private void ChooseBranch(BranchChoice branch)
    {
        NarrativeRunState.ChooseBranch(branch);
        RestoreRating(16f);

        if (branch == BranchChoice.Puzzle)
        {
            BlockCells(new Vector2Int(18, 7), new Vector2Int(19, 7), new Vector2Int(20, 7), new Vector2Int(18, 8), new Vector2Int(19, 8));
            message = "Нижний эфир заваливается помехой. Остаётся монтажная, где придётся разбирать себя.";
        }
        else
        {
            BlockCells(new Vector2Int(18, 13), new Vector2Int(19, 13), new Vector2Int(20, 13), new Vector2Int(18, 12), new Vector2Int(19, 12));
            message = "Верхний проход тухнет. Дикторы внизу встречают вашу злость аплодисментами.";
        }
    }

    private void BlockCells(params Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells)
        {
            if (!Inside(cell))
                continue;
            tiles[cell.x, cell.y] = Tile.Rubble;
            RedrawTile(cell);
        }
    }

    private bool CanUseExit()
    {
        return NarrativeRunState.Branch == BranchChoice.Puzzle && puzzleExitOpen ||
               NarrativeRunState.Branch == BranchChoice.Combat && combatExitOpen;
    }

    private void UpdatePuzzle()
    {
        bool startSolved = ArePlatesCovered(startPlates);
        if (startSolved && !startGateOpen)
        {
            startGateOpen = true;
            tiles[12, 10] = Tile.Floor;
            NarrativeRunState.RecordPuzzleSolved();
            RestoreRating(22f);
            message = "Стартовый сигнал собран. Два прохода раскрываются одновременно.";
            RedrawTile(new Vector2Int(12, 10));
        }

        bool puzzleSolved = ArePlatesCovered(puzzlePlates);
        if (NarrativeRunState.Branch == BranchChoice.Puzzle && puzzleSolved && storyRead && !puzzleExitOpen)
        {
            puzzleExitOpen = true;
            tiles[34, 12] = Tile.Floor;
            NarrativeRunState.RecordPuzzleSolved();
            RestoreRating(28f);
            message = "Смысл и сигнал совпали. Белая дверь перестаёт быть декорацией.";
            RedrawTile(new Vector2Int(34, 12));
        }
    }

    private bool ArePlatesCovered(List<Vector2Int> plates)
    {
        if (plates.Count == 0)
            return false;

        foreach (Vector2Int plate in plates)
        {
            if (StoneAt(plate) == null)
                return false;
        }

        return true;
    }

    private void UpdateBranchObjective()
    {
        if (NarrativeRunState.Branch != BranchChoice.Combat || combatExitOpen)
            return;

        foreach (Enemy enemy in enemies)
        {
            if (enemy.Branch == BranchChoice.Combat)
                return;
        }

        combatExitOpen = true;
        tiles[34, 8] = Tile.Floor;
        RestoreRating(24f);
        message = "Боевой эфир стихает. Нижний выход открывается, но шум уже похож на вас.";
        RedrawTile(new Vector2Int(34, 8));
    }

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy enemy in enemies.ToArray())
        {
            enemy.AttackCooldown = Mathf.Max(0f, enemy.AttackCooldown - dt);
            UpdateEnemyState(enemy);

            Vector2 target = ChooseEnemyTarget(enemy);
            float speed = enemy.Mode == EnemyMode.Hunt ? 2.55f : enemy.Mode == EnemyMode.Investigate ? 2.1f : 1.55f;
            Vector2 next = Vector2.MoveTowards(enemy.Position, target, speed * dt);
            if (CanEnemyOccupy(next, enemy))
                enemy.Position = next;

            enemy.View.transform.position = enemy.Position;
            SpriteRenderer enemyRenderer = enemy.View.GetComponent<SpriteRenderer>();
            enemyRenderer.sprite = SpriteForEnemyMode(enemy.Mode);
            enemyRenderer.color = EnemyColor(enemy.Mode);

            if (Vector2.Distance(enemy.Position, playerView.transform.position) <= 0.72f && enemy.AttackCooldown <= 0f)
            {
                enemy.AttackCooldown = 1.05f;
                DamagePlayer(1, enemy.Mode == EnemyMode.Hunt ? "Диктор догоняет вас и срывает дыхание." : "Диктор бьёт микрофоном.");
            }
        }

        if (lastNoisePower > 0)
            lastNoisePower = Mathf.Max(0, lastNoisePower - Mathf.CeilToInt(dt * 2f));
    }

    private void UpdateEnemyState(Enemy enemy)
    {
        Vector2Int enemyCell = WorldToCell(enemy.Position);
        if (CanSeePlayer(enemyCell))
        {
            enemy.Mode = EnemyMode.Hunt;
            enemy.LastSeen = PlayerCell();
            return;
        }

        if (lastNoisePower > 0 && Manhattan(enemyCell, lastNoiseCell) <= lastNoisePower)
        {
            enemy.Mode = EnemyMode.Investigate;
            enemy.LastSeen = lastNoiseCell;
            return;
        }

        if (enemy.Mode != EnemyMode.Patrol && Vector2.Distance(enemy.Position, ToWorld(enemy.LastSeen)) <= 0.1f)
            enemy.Mode = EnemyMode.Patrol;
    }

    private Vector2 ChooseEnemyTarget(Enemy enemy)
    {
        if (enemy.Mode == EnemyMode.Hunt)
            return playerView.transform.position;

        if (enemy.Mode == EnemyMode.Investigate)
            return ToWorld(enemy.LastSeen);

        if (enemy.Patrol.Count == 0)
            return enemy.Position;

        Vector2 target = ToWorld(enemy.Patrol[enemy.PatrolIndex]);
        if (Vector2.Distance(enemy.Position, target) <= 0.08f)
        {
            enemy.PatrolIndex = (enemy.PatrolIndex + 1) % enemy.Patrol.Count;
            target = ToWorld(enemy.Patrol[enemy.PatrolIndex]);
        }

        return target;
    }

    private bool CanEnemyOccupy(Vector2 position, Enemy self)
    {
        Vector2Int cell = WorldToCell(position);
        if (!Inside(cell) || IsSolid(tiles[cell.x, cell.y]) || StoneAt(cell) != null)
            return false;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != self && Vector2.Distance(enemy.Position, position) < 0.55f)
                return false;
        }

        return true;
    }

    private bool CanSeePlayer(Vector2Int from)
    {
        Vector2Int player = PlayerCell();
        if (Manhattan(from, player) > 8)
            return false;

        if (from.x == player.x)
        {
            int step = Math.Sign(player.y - from.y);
            for (int y = from.y + step; y != player.y; y += step)
            {
                if (BlocksSight(new Vector2Int(from.x, y)))
                    return false;
            }
            return true;
        }

        if (from.y == player.y)
        {
            int step = Math.Sign(player.x - from.x);
            for (int x = from.x + step; x != player.x; x += step)
            {
                if (BlocksSight(new Vector2Int(x, from.y)))
                    return false;
            }
            return true;
        }

        return Manhattan(from, player) <= 3;
    }

    private bool BlocksSight(Vector2Int cell)
    {
        if (!Inside(cell))
            return true;

        return IsSolid(tiles[cell.x, cell.y]) || StoneAt(cell) != null;
    }

    private void MakeNoise(Vector2Int cell, int power)
    {
        lastNoiseCell = cell;
        lastNoisePower = Mathf.Max(lastNoisePower, power);
        NarrativeRunState.RecordNoise(power);
    }

    private void UpdateStoneMotion(float dt)
    {
        foreach (Stone stone in stones)
        {
            if (!stone.Moving)
                continue;

            stone.View.transform.position = Vector3.MoveTowards(stone.View.transform.position, stone.Target, 7f * dt);
            if (Vector3.Distance(stone.View.transform.position, stone.Target) <= 0.01f)
            {
                stone.View.transform.position = stone.Target;
                stone.Moving = false;
                UpdatePuzzle();
            }
        }
    }

    private bool CanStoneEnter(Vector2Int cell)
    {
        return Inside(cell) && !IsSolid(tiles[cell.x, cell.y]) && StoneAt(cell) == null && EnemyAt(cell) == null;
    }

    private void BuildLevel()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                tiles[x, y] = Tile.Wall;
        }

        CarveRoom(1, 6, 11, 14);
        CarveRoom(13, 8, 17, 12);
        CarveRoom(19, 12, 36, 19);
        CarveRoom(19, 1, 36, 8);
        CarveRoom(34, 9, 37, 11);
        CarveRoom(11, 9, 13, 11);
        CarveRoom(17, 12, 20, 14);
        CarveRoom(17, 6, 20, 8);
        CarveRoom(33, 11, 35, 13);
        CarveRoom(33, 7, 35, 9);

        exitPosition = new Vector2Int(36, 10);
        tiles[exitPosition.x, exitPosition.y] = Tile.Exit;
        tiles[12, 10] = Tile.Gate;
        tiles[34, 12] = Tile.Gate;
        tiles[34, 8] = Tile.Gate;
        tiles[4, 7] = Tile.Remote;
        tiles[7, 13] = Tile.Trap;
        tiles[15, 9] = Tile.Trap;
        tiles[23, 17] = Tile.Story;
        tiles[24, 15] = Tile.Trap;
        tiles[27, 5] = Tile.Trap;
        tiles[32, 3] = Tile.Trap;

        AddPlate(startPlates, new Vector2Int(5, 11));
        AddPlate(startPlates, new Vector2Int(5, 9));
        AddPlate(puzzlePlates, new Vector2Int(27, 16));
        AddPlate(puzzlePlates, new Vector2Int(30, 16));

        AddStone(new Vector2Int(8, 11));
        AddStone(new Vector2Int(8, 9));
        AddStone(new Vector2Int(27, 14));
        AddStone(new Vector2Int(30, 14));

        AddEnemy(BranchChoice.None, new Vector2Int(15, 11), new Vector2Int(15, 11), new Vector2Int(17, 11), new Vector2Int(17, 9));
        AddEnemy(BranchChoice.Combat, new Vector2Int(24, 5), new Vector2Int(24, 5), new Vector2Int(33, 5), new Vector2Int(33, 2));
        AddEnemy(BranchChoice.Combat, new Vector2Int(31, 7), new Vector2Int(31, 7), new Vector2Int(21, 7), new Vector2Int(21, 3));
        AddEnemy(BranchChoice.Combat, new Vector2Int(35, 3), new Vector2Int(35, 3), new Vector2Int(28, 3));

        if (playerView != null)
        {
            playerView.transform.position = ToWorld(new Vector2Int(3, 10));
            playerBody.linearVelocity = Vector2.zero;
        }
    }

    private void CarveRoom(int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (Inside(new Vector2Int(x, y)))
                    tiles[x, y] = Tile.Floor;
            }
        }
    }

    private void AddPlate(List<Vector2Int> list, Vector2Int cell)
    {
        list.Add(cell);
        tiles[cell.x, cell.y] = Tile.Plate;
    }

    private void AddStone(Vector2Int cell)
    {
        stones.Add(new Stone
        {
            Cell = cell,
            Target = ToWorld(cell),
        });
    }

    private void AddEnemy(BranchChoice branch, Vector2Int start, params Vector2Int[] patrol)
    {
        var enemy = new Enemy
        {
            Position = ToWorld(start),
            LastSeen = start,
            Mode = EnemyMode.Patrol,
            Branch = branch,
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
                Vector2Int cell = new Vector2Int(x, y);
                GameObject floorView = new GameObject($"Floor {x},{y}");
                floorView.transform.SetParent(tileRoot.transform);
                floorView.transform.position = ToWorld(cell);
                SpriteRenderer floorRenderer = floorView.AddComponent<SpriteRenderer>();
                SetLitMaterial(floorRenderer);
                floorRenderer.sortingOrder = -2;
                floorViews[x, y] = floorView;

                GameObject view = new GameObject($"Tile {x},{y}");
                view.transform.SetParent(tileRoot.transform);
                view.transform.position = ToWorld(cell);
                SetLitMaterial(view.AddComponent<SpriteRenderer>());
                tileViews[x, y] = view;
            }
        }

        CreateEntityViews();
        RebuildTileColliders();
    }

    private void CreateEntityViews()
    {
        if (playerView == null)
        {
            playerView = new GameObject("Player");
            playerView.transform.position = ToWorld(new Vector2Int(3, 10));
            var renderer = playerView.AddComponent<SpriteRenderer>();
            renderer.sprite = playerSprite;
            SetLitMaterial(renderer);
            renderer.sortingOrder = 20;
            playerBody = playerView.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.freezeRotation = true;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = playerView.AddComponent<CircleCollider2D>();
            collider.radius = 0.34f;
        }
        else
        {
            playerView.transform.position = ToWorld(new Vector2Int(3, 10));
        }

        foreach (Stone stone in stones)
        {
            GameObject view = new GameObject("Signal Blocker");
            view.transform.position = ToWorld(stone.Cell);
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = stoneSprite;
            SetLitMaterial(renderer);
            renderer.sortingOrder = 12;
            var collider = view.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.82f, 0.82f);
            stone.View = view;
        }

        foreach (Enemy enemy in enemies)
        {
            enemy.View = new GameObject("Enemy");
            enemy.View.transform.position = enemy.Position;
            var renderer = enemy.View.AddComponent<SpriteRenderer>();
            renderer.sprite = enemySprite;
            SetLitMaterial(renderer);
            renderer.sortingOrder = 15;
        }
    }

    private void CreateLighting()
    {
        Urp2DLighting.AddGlobalLight(gameObject, new Color(0.64f, 0.67f, 0.70f), 0.92f);

        var channelLightObject = new GameObject("Channel Light");
        channelLightObject.transform.SetParent(transform);
        channelLightObject.transform.position = new Vector3(14f, 10f, 0f);
        Urp2DLighting.AddPointLight(channelLightObject, new Color(0.62f, 0.78f, 0.94f), 0.28f, 10.0f, 1.5f);
    }

    private static void SetLitMaterial(SpriteRenderer renderer)
    {
        if (Urp2DLighting.SpriteLitMaterial != null)
            renderer.sharedMaterial = Urp2DLighting.SpriteLitMaterial;
    }

    private void RedrawAll()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                RedrawTile(new Vector2Int(x, y));
        }

        foreach (Enemy enemy in enemies)
        {
            enemy.View.transform.position = enemy.Position;
            SpriteRenderer enemyRenderer = enemy.View.GetComponent<SpriteRenderer>();
            enemyRenderer.sprite = SpriteForEnemyMode(enemy.Mode);
            enemyRenderer.color = EnemyColor(enemy.Mode);
        }
    }

    private void RedrawTile(Vector2Int cell)
    {
        if (!Inside(cell) || tileViews[cell.x, cell.y] == null)
            return;

        SpriteRenderer renderer = tileViews[cell.x, cell.y].GetComponent<SpriteRenderer>();
        SpriteRenderer floorRenderer = floorViews[cell.x, cell.y].GetComponent<SpriteRenderer>();
        bool hasFloor = tiles[cell.x, cell.y] != Tile.Wall;
        floorRenderer.sprite = hasFloor ? floorSprite : null;

        renderer.sprite = OverlaySpriteFor(tiles[cell.x, cell.y], cell);
        renderer.sortingOrder = tiles[cell.x, cell.y] == Tile.Rubble ? 3 : tiles[cell.x, cell.y] == Tile.Wall ? 1 : 2;

        BoxCollider2D collider = tileViews[cell.x, cell.y].GetComponent<BoxCollider2D>();
        bool shouldCollide = IsSolid(tiles[cell.x, cell.y]);
        if (shouldCollide && collider == null)
        {
            collider = tileViews[cell.x, cell.y].AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
        }
        else if (!shouldCollide && collider != null)
        {
            Destroy(collider);
        }
    }

    private void RebuildTileColliders()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                RedrawTile(new Vector2Int(x, y));
        }
    }

    private static Color EnemyColor(EnemyMode mode)
    {
        return mode switch
        {
            EnemyMode.Hunt => new Color(1.00f, 0.42f, 0.56f),
            EnemyMode.Investigate => new Color(1.00f, 0.78f, 0.36f),
            _ => Color.white,
        };
    }

    private Sprite SpriteForEnemyMode(EnemyMode mode)
    {
        return mode switch
        {
            EnemyMode.Hunt => enemyHuntSprite,
            EnemyMode.Investigate => enemyInvestigateSprite,
            _ => enemySprite,
        };
    }

    private void UpdatePlayerSprite()
    {
        if (playerView == null)
            return;

        SpriteRenderer renderer = playerView.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        FacingDirection direction = FacingFromAim(lastAim);
        int index = (int)direction;
        if (attackCooldown > 0.18f)
        {
            renderer.sprite = playerAttackSprites[index] ?? playerSprite;
            return;
        }

        if (moveInput.sqrMagnitude > 0.01f)
        {
            bool first = Mathf.FloorToInt(Time.time * 8f) % 2 == 0;
            renderer.sprite = (first ? playerWalkOneSprites[index] : playerWalkTwoSprites[index]) ?? playerSprite;
            return;
        }

        renderer.sprite = playerIdleSprites[index] ?? playerSprite;
    }

    private static FacingDirection FacingFromAim(Vector2 aim)
    {
        if (Mathf.Abs(aim.x) > Mathf.Abs(aim.y))
            return aim.x < 0f ? FacingDirection.Left : FacingDirection.Right;

        return aim.y < 0f ? FacingDirection.Down : FacingDirection.Up;
    }

    private Sprite OverlaySpriteFor(Tile tile, Vector2Int cell)
    {
        return tile switch
        {
            Tile.Wall => wallSprite,
            Tile.Plate => plateSprite,
            Tile.Gate => GateOpenForCell(cell) ? openGateSprite : gateSprite,
            Tile.Rubble => rubbleSprite,
            Tile.Trap => trapSprite,
            Tile.Remote => remoteSprite,
            Tile.Story => storySprite,
            Tile.Exit => exitSprite,
            _ => null,
        };
    }

    private bool GateOpenForCell(Vector2Int cell)
    {
        return cell == new Vector2Int(12, 10) && startGateOpen ||
               cell == new Vector2Int(34, 12) && puzzleExitOpen ||
               cell == new Vector2Int(34, 8) && combatExitOpen;
    }

    private bool TryCreateAtlasSprites()
    {
        try
        {
            DetectAtlasGrid();
            floorSprite = CreateQuietFloorSprite();
            wallSprite = CreateQuietWallSprite();
            plateSprite = CreateAtlasSprite(3, 4, "signal_plate");
            gateSprite = CreateAtlasSprite(3, 0, "closed_gate");
            openGateSprite = CreateAtlasSprite(3, 2, "open_gate");
            exitSprite = CreateAtlasSprite(3, 10, "signal_exit");
            rubbleSprite = CreateAtlasSprite(14, 10, "static_rubble");
            trapSprite = CreateAtlasSprite(3, 12, "camera_trap");
            remoteSprite = CreateAtlasSprite(3, 9, "remote");
            storySprite = CreateAtlasSprite(12, 1, "story_note");
            stoneSprite = CreateAtlasSprite(3, 7, "signal_blocker");

            SetPlayerSprites(FacingDirection.Down, 4, 0, 1, 2, 6);
            SetPlayerSprites(FacingDirection.Up, 5, 1, 2, 3, 6);
            SetPlayerSprites(FacingDirection.Left, 6, 0, 1, 2, 7);
            SetPlayerSprites(FacingDirection.Right, 4, 8, 9, 10, 14);
            playerSprite = playerIdleSprites[(int)FacingDirection.Down];

            enemySprite = CreateAtlasSprite(8, 0, "anchor_patrol");
            enemyInvestigateSprite = CreateAtlasSprite(9, 8, "anchor_investigate");
            enemyHuntSprite = CreateAtlasSprite(10, 8, "anchor_hunt");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Sprite atlas could not be sliced, using fallback sprites: {ex.Message}");
            return false;
        }
    }

    private void SetPlayerSprites(FacingDirection direction, int topRow, int idleCol, int walkOneCol, int walkTwoCol, int attackCol)
    {
        int index = (int)direction;
        playerIdleSprites[index] = CreateAtlasSprite(topRow, idleCol, $"player_{direction}_idle");
        playerWalkOneSprites[index] = CreateAtlasSprite(topRow, walkOneCol, $"player_{direction}_walk_1");
        playerWalkTwoSprites[index] = CreateAtlasSprite(topRow, walkTwoCol, $"player_{direction}_walk_2");
        playerAttackSprites[index] = CreateAtlasSprite(topRow, attackCol, $"player_{direction}_attack");
    }

    private Sprite CreateAtlasSprite(int topRow, int column, string spriteName)
    {
        if (atlasVerticalLines == null || atlasHorizontalLines == null ||
            column + 1 >= atlasVerticalLines.Length || topRow + 1 >= atlasHorizontalLines.Length)
        {
            throw new InvalidOperationException($"Atlas cell {column},{topRow} is outside detected grid.");
        }

        int left = atlasVerticalLines[column];
        int right = atlasVerticalLines[column + 1];
        int top = atlasHorizontalLines[topRow];
        int bottom = atlasHorizontalLines[topRow + 1];
        int cellWidth = right - left;
        int cellHeight = bottom - top;
        int cropSize = Mathf.Max(8, Mathf.Min(cellWidth, cellHeight) - AtlasInset * 2);
        int sourceX = left + (cellWidth - cropSize) / 2;
        int sourceTop = top + (cellHeight - cropSize) / 2;
        int sourceY = SpriteAtlas.height - sourceTop - cropSize;
        Color[] pixels = SpriteAtlas.GetPixels(sourceX, sourceY, cropSize, cropSize);
        var texture = new Texture2D(cropSize, cropSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = spriteName,
        };

        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % cropSize;
            int y = i / cropSize;
            if (x <= 1 || y <= 1 || x >= cropSize - 2 || y >= cropSize - 2 || IsChromaGreen(pixels[i]))
                pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, cropSize, cropSize), new Vector2(0.5f, 0.5f), cropSize, 0, SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    private void DetectAtlasGrid()
    {
        atlasVerticalLines = DetectAxisLines(true, AtlasColumns + 1, SpriteAtlas.width);
        atlasHorizontalLines = DetectAxisLines(false, 16, SpriteAtlas.height);
    }

    private int[] DetectAxisLines(bool vertical, int expectedCount, int length)
    {
        var candidates = new List<int>();
        int perpendicular = vertical ? SpriteAtlas.height : SpriteAtlas.width;
        bool inRun = false;
        int runStart = 0;

        for (int i = 0; i < length; i++)
        {
            int dark = 0;
            for (int j = 0; j < perpendicular; j++)
            {
                Color color = vertical ? ReadAtlasPixelTop(i, j) : ReadAtlasPixelTop(j, i);
                if (IsSeparatorDark(color))
                    dark++;
            }

            bool separator = dark / (float)perpendicular > 0.70f;
            if (separator && !inRun)
            {
                inRun = true;
                runStart = i;
            }
            else if (!separator && inRun)
            {
                AddSeparatorCandidate(candidates, runStart, i - 1);
                inRun = false;
            }
        }

        if (inRun)
            AddSeparatorCandidate(candidates, runStart, length - 1);

        var lines = new List<int> { 0 };
        foreach (int candidate in candidates)
        {
            if (candidate <= 4 || candidate >= length - 4)
                continue;
            if (lines.Count == 0 || candidate - lines[lines.Count - 1] >= 64)
                lines.Add(candidate);
        }
        if (lines[lines.Count - 1] != length)
            lines.Add(length);

        if (lines.Count >= expectedCount)
            return lines.ToArray();

        Debug.LogWarning($"Atlas grid detection found {lines.Count} lines on {(vertical ? "X" : "Y")} axis, falling back to uniform 128px grid.");
        lines.Clear();
        for (int i = 0; i <= length; i += AtlasCellSize)
            lines.Add(Mathf.Min(i, length));
        if (lines[lines.Count - 1] != length)
            lines.Add(length);
        return lines.ToArray();
    }

    private static void AddSeparatorCandidate(List<int> candidates, int start, int end)
    {
        if (end - start <= 4)
            candidates.Add((start + end) / 2);
    }

    private Color ReadAtlasPixelTop(int x, int topY)
    {
        return SpriteAtlas.GetPixel(x, SpriteAtlas.height - 1 - topY);
    }

    private static bool IsSeparatorDark(Color color)
    {
        return color.r < 0.16f && color.g < 0.18f && color.b < 0.16f;
    }

    private static bool IsChromaGreen(Color color)
    {
        float maxOther = Mathf.Max(color.r, color.b);
        return color.g > 0.22f &&
               color.g - maxOther > 0.10f &&
               color.r < 0.50f &&
               color.b < 0.50f;
    }

    private static Sprite CreateQuietFloorSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };

        Color baseColor = new Color(0.095f, 0.105f, 0.115f);
        Color lineColor = new Color(0.135f, 0.145f, 0.155f);
        Color moteColor = new Color(0.115f, 0.125f, 0.135f);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool seam = x == 0 || y == 0;
                bool mote = (x * 19 + y * 11) % 53 == 0;
                texture.SetPixel(x, y, seam ? lineColor : mote ? moteColor : baseColor);
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }

    private static Sprite CreateQuietWallSprite()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };

        Color baseColor = new Color(0.235f, 0.255f, 0.275f);
        Color edgeColor = new Color(0.095f, 0.105f, 0.120f);
        Color highlightColor = new Color(0.36f, 0.39f, 0.42f);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                bool panelLine = y == 8 || y == 24;
                bool faint = (x + y * 3) % 47 == 0;
                texture.SetPixel(x, y, edge ? edgeColor : panelLine ? highlightColor : faint ? Color.Lerp(baseColor, highlightColor, 0.35f) : baseColor);
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }

    private void CreateSprites()
    {
        if (SpriteAtlas != null && TryCreateAtlasSprites())
            return;

        CreateFallbackSprites();
    }

    private void CreateFallbackSprites()
    {
        floorSprite = CreateSprite(new Color(0.09f, 0.10f, 0.11f), new Color(0.15f, 0.16f, 0.18f), new Color(0.18f, 0.22f, 0.25f), SpriteMark.None);
        wallSprite = CreateSprite(new Color(0.22f, 0.24f, 0.27f), new Color(0.12f, 0.13f, 0.15f), new Color(0.52f, 0.56f, 0.60f), SpriteMark.None);
        plateSprite = CreateSprite(new Color(0.28f, 0.25f, 0.18f), new Color(0.11f, 0.10f, 0.08f), new Color(0.95f, 0.82f, 0.36f), SpriteMark.Plate);
        gateSprite = CreateSprite(new Color(0.34f, 0.10f, 0.14f), new Color(0.12f, 0.05f, 0.06f), new Color(0.90f, 0.18f, 0.24f), SpriteMark.Gate);
        openGateSprite = CreateSprite(new Color(0.10f, 0.30f, 0.28f), new Color(0.04f, 0.12f, 0.13f), new Color(0.66f, 0.92f, 1.00f), SpriteMark.Gate);
        exitSprite = CreateSprite(new Color(0.82f, 0.88f, 0.92f), new Color(0.56f, 0.66f, 0.72f), new Color(0.12f, 0.18f, 0.22f), SpriteMark.Exit);
        rubbleSprite = CreateSprite(new Color(0.18f, 0.18f, 0.20f), new Color(0.08f, 0.08f, 0.09f), new Color(0.72f, 0.76f, 0.82f), SpriteMark.Rubble);
        trapSprite = CreateSprite(new Color(0.18f, 0.10f, 0.13f), new Color(0.08f, 0.05f, 0.06f), new Color(0.94f, 0.18f, 0.28f), SpriteMark.Trap);
        remoteSprite = CreateSprite(new Color(0.12f, 0.13f, 0.14f), new Color(0.05f, 0.05f, 0.05f), new Color(1.00f, 0.86f, 0.25f), SpriteMark.Remote);
        storySprite = CreateSprite(new Color(0.12f, 0.17f, 0.20f), new Color(0.04f, 0.07f, 0.08f), new Color(0.68f, 0.94f, 1.00f), SpriteMark.Story);
        playerSprite = CreateSprite(new Color(0.22f, 0.33f, 0.40f), new Color(0.07f, 0.10f, 0.13f), new Color(0.86f, 0.96f, 1.00f), SpriteMark.Player);
        for (int i = 0; i < playerIdleSprites.Length; i++)
        {
            playerIdleSprites[i] = playerSprite;
            playerWalkOneSprites[i] = playerSprite;
            playerWalkTwoSprites[i] = playerSprite;
            playerAttackSprites[i] = playerSprite;
        }
        stoneSprite = CreateSprite(new Color(0.39f, 0.33f, 0.27f), new Color(0.17f, 0.14f, 0.11f), new Color(0.74f, 0.64f, 0.48f), SpriteMark.Stone);
        enemySprite = CreateSprite(new Color(0.34f, 0.12f, 0.16f), new Color(0.12f, 0.06f, 0.08f), new Color(0.95f, 0.24f, 0.30f), SpriteMark.Enemy);
        enemyInvestigateSprite = enemySprite;
        enemyHuntSprite = enemySprite;
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
                bool scan = y % 5 == 0;
                Color color = edge ? edgeColor : scan ? Color.Lerp(baseColor, markColor, 0.12f) : baseColor;
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
                DrawRect(texture, 3, 3, 10, 10, color, false);
                DrawLine(texture, 5, 8, 10, 8, color);
                DrawLine(texture, 8, 6, 10, 8, color);
                DrawLine(texture, 8, 10, 10, 8, color);
                break;
            case SpriteMark.Rubble:
                DrawRect(texture, 3, 3, 4, 4, color, true);
                DrawRect(texture, 8, 5, 5, 3, color, true);
                DrawRect(texture, 5, 10, 7, 3, color, true);
                break;
            case SpriteMark.Trap:
                DrawLine(texture, 4, 4, 11, 11, color);
                DrawLine(texture, 11, 4, 4, 11, color);
                break;
            case SpriteMark.Remote:
                DrawRect(texture, 5, 4, 6, 9, color, false);
                SetSafe(texture, 8, 6, color);
                SetSafe(texture, 8, 9, color);
                break;
            case SpriteMark.Story:
                DrawRect(texture, 4, 3, 8, 10, color, false);
                DrawLine(texture, 6, 6, 10, 6, color);
                DrawLine(texture, 6, 9, 9, 9, color);
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
        camera.orthographicSize = 6.8f;
        camera.transform.position = new Vector3(8f, 10f, -10f);
        camera.backgroundColor = new Color(0.070f, 0.076f, 0.086f);
    }

    private static float ClampCameraAxis(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }

    private void EnsureHudTextures()
    {
        if (hudTexture == null)
        {
            hudTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            hudTexture.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.06f, 0.88f));
            hudTexture.Apply();
        }

        if (whiteTexture == null)
        {
            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }
    }

    private Vector2Int PlayerCell()
    {
        return WorldToCell(playerView.transform.position);
    }

    private static Vector2Int WorldToCell(Vector2 position)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(position.x / CellSize), 0, Width - 1),
            Mathf.Clamp(Mathf.RoundToInt(position.y / CellSize), 0, Height - 1));
    }

    private static Vector3 ToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * CellSize, cell.y * CellSize, 0f);
    }

    private static Vector2Int Cardinal(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return Vector2Int.zero;

        return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? new Vector2Int(direction.x >= 0f ? 1 : -1, 0)
            : new Vector2Int(0, direction.y >= 0f ? 1 : -1);
    }

    private IEnumerable<Vector2Int> NeighborCells(Vector2Int cell)
    {
        yield return cell;
        yield return cell + Vector2Int.up;
        yield return cell + Vector2Int.down;
        yield return cell + Vector2Int.left;
        yield return cell + Vector2Int.right;
    }

    private Stone StoneAt(Vector2Int cell)
    {
        foreach (Stone stone in stones)
        {
            if (stone.Cell == cell)
                return stone;
        }

        return null;
    }

    private Enemy EnemyAt(Vector2Int cell)
    {
        foreach (Enemy enemy in enemies)
        {
            if (WorldToCell(enemy.Position) == cell)
                return enemy;
        }

        return null;
    }

    private static bool IsSolid(Tile tile)
    {
        return tile == Tile.Wall || tile == Tile.Gate || tile == Tile.Rubble;
    }

    private static bool Inside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }
}
