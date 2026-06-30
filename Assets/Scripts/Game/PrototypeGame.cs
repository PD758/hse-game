using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class PrototypeGame : MonoBehaviour
{
    private const int Width = 39;
    private const int Height = 21;
    private const float CellSize = 1f;
    private const float PlayerSpeed = 4.6f;
    private const float PlayerAcceleration = 24f;
    private const float PlayerDeceleration = 18f;
    private const float RatingCritical = 15f;
    private const float GameplayCameraSize = 4.8f;
    private const float PlayerAttackRange = 1.85f;
    private const float PlayerAttackConeMinDot = -0.05f;
    private const float EnemyAttackCooldown = 1.75f;
    private const float RemoteCooldown = 18f;
    private const float RemoteJamDuration = 3f;
    private const float RemoteRatingRestore = 18f;
    private const float RemoteEnemySpeedMultiplier = 0.18f;
    private const int FixedAtlasColumns = 8;
    private const int FixedAtlasRows = 8;
    private const int HudAtlasColumns = 4;
    private const int HudAtlasRows = 4;

    public Texture2D CharacterAtlas;
    public Texture2D EnvironmentAtlas;
    public Texture2D WallAtlas;
    public Texture2D HudAtlas;

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
    private readonly GameObject[,] floorDecalViews = new GameObject[Width, Height];
    private readonly GameObject[,] tileViews = new GameObject[Width, Height];
    private readonly List<Vector2Int> startPlates = new List<Vector2Int>();
    private readonly List<Vector2Int> puzzlePlates = new List<Vector2Int>();
    private readonly Vector2Int[] pathParents = new Vector2Int[Width * Height];
    private readonly bool[] pathVisited = new bool[Width * Height];
    private readonly Queue<Vector2Int> pathQueue = new Queue<Vector2Int>();
    private readonly List<Stone> stones = new List<Stone>();
    private readonly List<Enemy> enemies = new List<Enemy>();

    [SerializeField] private Sprite floorSprite;
    [SerializeField] private Sprite wallSprite;
    [SerializeField] private Sprite wallVerticalSprite;
    [SerializeField] private Sprite wallCornerSprite;
    [SerializeField] private Sprite plateSprite;
    [SerializeField] private Sprite pressedPlateSprite;
    [SerializeField] private Sprite gateSprite;
    [SerializeField] private Sprite openGateSprite;
    [SerializeField] private Sprite exitSprite;
    [SerializeField] private Sprite openExitSprite;
    [SerializeField] private Sprite rubbleSprite;
    [SerializeField] private Sprite trapSprite;
    [SerializeField] private Sprite remoteSprite;
    [SerializeField] private Sprite storySprite;
    [SerializeField] private Sprite playerSprite;
    [SerializeField] private Sprite[] playerIdleSprites = new Sprite[4];
    [SerializeField] private Sprite[] playerWalkOneSprites = new Sprite[4];
    [SerializeField] private Sprite[] playerWalkTwoSprites = new Sprite[4];
    [SerializeField] private Sprite[] playerAttackSprites = new Sprite[4];
    [SerializeField] private Sprite stoneSprite;
    [SerializeField] private Sprite enemySprite;
    [SerializeField] private Sprite enemyInvestigateSprite;
    [SerializeField] private Sprite enemyHuntSprite;
    [SerializeField] private Texture2D hudTexture;
    [SerializeField] private Texture2D hudPanelTexture;
    [SerializeField] private Texture2D ratingFrameNeutralTexture;
    [SerializeField] private Texture2D ratingFramePuzzleTexture;
    [SerializeField] private Texture2D ratingFrameCombatTexture;
    [SerializeField] private Texture2D ratingFrameCriticalTexture;
    [SerializeField] private Texture2D whiteTexture;
    [SerializeField] private Sprite[] floorSprites = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] floorDecalSprites = Array.Empty<Sprite>();

    [SerializeField] private GameObject playerView;
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
    private float remoteCooldown;
    private float remoteJamTimer;
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
        BuildLevel();

        if (!HasBakedAssets() || !BindSceneViews())
        {
            Debug.LogError("Prototype scene is not baked. Run Rogue > Bootstrap All Scenes before entering Play Mode.");
            enabled = false;
            return;
        }

        RedrawAll();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (Pressed(keyboard?.rKey))
        {
            Restart();
            return;
        }

        if (Pressed(keyboard?.escapeKey))
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        ReadMoveInput();
        UpdatePlayerSprite();

        if (gameEnded)
            return;

        UpdateRating(Time.deltaTime);
        UpdateRemoteTimers(Time.deltaTime);
        if (Pressed(keyboard?.qKey))
            TryUseRemoteAbility();

        UpdateStoneMotion(Time.deltaTime);
        UpdateEnemies(Time.deltaTime);
        UpdateWorldInteractions();

        if (attackCooldown > 0f)
            attackCooldown -= Time.deltaTime;

        if (Pressed(keyboard?.eKey))
            TryInteract();

        if (Pressed(keyboard?.spaceKey) || Pressed(mouse?.leftButton))
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
        if (hudTexture == null || whiteTexture == null)
            return;

        GUI.color = Color.white;
        float meterWidth = Screen.width < 760 ? 44f : 54f;
        float hudWidth = Mathf.Min(900f, Screen.width - meterWidth - 28f);
        float hudHeight = Screen.width < 760 ? 118f : 104f;
        GUI.DrawTexture(new Rect(10, 10, hudWidth, hudHeight), hudPanelTexture ?? hudTexture, ScaleMode.StretchToFill, true);

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Screen.width < 760 ? 13 : 16,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        PixelGui.Apply(style);

        DrawStatusIcons(new Rect(18, 18, 178, 28));
        GUI.Label(new Rect(210, 16, hudWidth - 226f, 42), message, style);

        var hintStyle = new GUIStyle(style)
        {
            fontSize = Screen.width < 760 ? 12 : 14,
            normal = { textColor = new Color(0.74f, 0.78f, 0.82f) },
        };
        PixelGui.Apply(hintStyle);
        GUI.Label(new Rect(210, 56, hudWidth - 226f, 32), NarrativeRunState.SignalHint(), hintStyle);

        string controls = Screen.width < 900
            ? "WASD/стрелки: движение | Space/ЛКМ: атака | E: действие | Q: пульт | R: рестарт | Esc: меню"
            : "WASD/стрелки: двигаться | Space/ЛКМ: атаковать | E: взаимодействовать/толкать | Q: пульт | R: перезапуск | Esc: меню";
        GUI.Label(new Rect(16, Screen.height - 50, Screen.width - 32, 44), controls, hintStyle);
        DrawVerticalRatingMeter(new Rect(Screen.width - meterWidth - 16f, 78f, meterWidth, Mathf.Min(300f, Screen.height - 156f)));
    }

    private void DrawStatusIcons(Rect rect)
    {
        DrawHpPips(new Rect(rect.x, rect.y, 68f, rect.height));
        DrawBranchBadge(new Rect(rect.x + 78f, rect.y, 28f, rect.height));
        DrawRemoteBadge(new Rect(rect.x + 116f, rect.y, 38f, rect.height));
    }

    private void DrawHpPips(Rect rect)
    {
        for (int i = 0; i < 6; i++)
        {
            Rect pip = new Rect(rect.x + i * 11f, rect.y + 4f, 8f, rect.height - 8f);
            GUI.color = i < playerHp ? new Color(0.92f, 0.96f, 1f, 0.96f) : new Color(0.18f, 0.20f, 0.23f, 0.92f);
            GUI.DrawTexture(pip, whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(new Rect(pip.x, pip.y, pip.width, 1f), whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DrawBranchBadge(Rect rect)
    {
        GUI.color = new Color(0.05f, 0.06f, 0.07f, 0.88f);
        GUI.DrawTexture(rect, whiteTexture);

        Color color = NarrativeRunState.Branch switch
        {
            BranchChoice.Puzzle => new Color(0.48f, 0.86f, 1f, 0.95f),
            BranchChoice.Combat => new Color(1f, 0.24f, 0.20f, 0.95f),
            _ => new Color(0.50f, 0.54f, 0.60f, 0.80f),
        };

        GUI.color = color;
        if (NarrativeRunState.Branch == BranchChoice.Combat)
        {
            GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 6f, 4f, rect.height - 12f), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 17f, rect.y + 6f, 4f, rect.height - 12f), whiteTexture);
        }
        else
        {
            GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 5f, 4f, rect.height - 10f), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 12f, rect.width - 12f, 4f), whiteTexture);
        }

        GUI.color = Color.white;
    }

    private void DrawRemoteBadge(Rect rect)
    {
        GUI.color = hasRemote ? new Color(0.06f, 0.07f, 0.08f, 0.92f) : new Color(0.04f, 0.04f, 0.05f, 0.52f);
        GUI.DrawTexture(rect, whiteTexture);

        Rect body = new Rect(rect.x + 10f, rect.y + 5f, 18f, rect.height - 10f);
        GUI.color = hasRemote ? new Color(0.42f, 0.47f, 0.52f, 0.96f) : new Color(0.16f, 0.17f, 0.19f, 0.72f);
        GUI.DrawTexture(body, whiteTexture);
        GUI.color = RemoteJamActive() ? new Color(0.50f, 0.92f, 1f, 0.96f) : new Color(0.86f, 0.18f, 0.16f, hasRemote ? 0.94f : 0.35f);
        GUI.DrawTexture(new Rect(body.x + 11f, body.y + 3f, 4f, 4f), whiteTexture);

        if (hasRemote && remoteCooldown > 0f)
        {
            float ratio = Mathf.Clamp01(remoteCooldown / RemoteCooldown);
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height * ratio), whiteTexture);
        }

        GUI.color = Color.white;
    }

    private void DrawVerticalRatingMeter(Rect rect)
    {
        Texture2D frame = RatingFrameTexture();
        GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);
        GUI.DrawTexture(rect, whiteTexture);

        Rect inner = new Rect(rect.x + rect.width * 0.37f, rect.y + rect.height * 0.13f, rect.width * 0.26f, rect.height * 0.70f);
        GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);
        GUI.DrawTexture(inner, whiteTexture);

        float fill = inner.height * Mathf.Clamp01(viewerRating / 100f);
        GUI.color = RatingColor();
        GUI.DrawTexture(new Rect(inner.x + 2f, inner.yMax - fill + 2f, inner.width - 4f, Mathf.Max(0f, fill - 4f)), whiteTexture);

        GUI.color = new Color(0.72f, 0.78f, 0.84f, 0.42f);
        for (int i = 0; i < 6; i++)
        {
            float y = Mathf.Lerp(inner.yMax - 4f, inner.y + 4f, i / 5f);
            GUI.DrawTexture(new Rect(rect.x + 6f, y, rect.width - 12f, 1f), whiteTexture);
        }

        if (frame != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, frame, ScaleMode.StretchToFill, true);
        }
        else
        {
            GUI.color = new Color(0.70f, 0.76f, 0.82f, 0.58f);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 2f), whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.yMax - 6f, rect.width - 8f, 2f), whiteTexture);
        }

        GUI.color = Color.white;
    }

    private Texture2D RatingFrameTexture()
    {
        if (viewerRating <= RatingCritical)
            return ratingFrameCriticalTexture ?? ratingFrameCombatTexture ?? ratingFrameNeutralTexture;

        return NarrativeRunState.Branch switch
        {
            BranchChoice.Puzzle => ratingFramePuzzleTexture ?? ratingFrameNeutralTexture,
            BranchChoice.Combat => ratingFrameCombatTexture ?? ratingFrameNeutralTexture,
            _ => ratingFrameNeutralTexture,
        };
    }

    private Color RatingColor()
    {
        Color tone = NarrativeRunState.Branch switch
        {
            BranchChoice.Combat => new Color(1.00f, 0.16f, 0.14f),
            BranchChoice.Puzzle => new Color(0.52f, 0.86f, 1.00f),
            _ => NarrativeRunState.IsAggressive() ? new Color(1.00f, 0.16f, 0.14f) : new Color(0.60f, 0.88f, 1.00f),
        };
        return Color.Lerp(Color.white, tone, 1f - viewerRating / 100f);
    }

    private void Restart()
    {
        NarrativeRunState.Reset();

        enemies.Clear();
        stones.Clear();
        startPlates.Clear();
        puzzlePlates.Clear();
        playerHp = 6;
        viewerRating = 100f;
        idleTimer = 0f;
        criticalDamageTimer = 0f;
        attackCooldown = 0f;
        remoteCooldown = 0f;
        remoteJamTimer = 0f;
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
        if (!BindSceneViews())
        {
            Debug.LogError("Prototype scene lost baked references. Run Rogue > Bootstrap All Scenes.");
            enabled = false;
            return;
        }

        RedrawAll();
        RebuildTileColliders();
    }

    private void ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        float x = 0f;
        float y = 0f;
        if (Held(keyboard?.aKey) || Held(keyboard?.leftArrowKey))
            x -= 1f;
        if (Held(keyboard?.dKey) || Held(keyboard?.rightArrowKey))
            x += 1f;
        if (Held(keyboard?.sKey) || Held(keyboard?.downArrowKey))
            y -= 1f;
        if (Held(keyboard?.wKey) || Held(keyboard?.upArrowKey))
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

    private static bool Pressed(ButtonControl control)
    {
        return control != null && control.wasPressedThisFrame;
    }

    private static bool Held(ButtonControl control)
    {
        return control != null && control.isPressed;
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
                message = "Пульт: Q глушит эфир на 3 секунды. Кд 18 секунд.";
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

    private void UpdateRemoteTimers(float dt)
    {
        if (remoteCooldown > 0f)
            remoteCooldown = Mathf.Max(0f, remoteCooldown - dt);
        if (remoteJamTimer > 0f)
            remoteJamTimer = Mathf.Max(0f, remoteJamTimer - dt);
    }

    private void TryUseRemoteAbility()
    {
        MarkActivity();

        if (!hasRemote)
        {
            message = "Пульт ещё где-то в эфире. Найдите его и нажмите E.";
            return;
        }

        if (remoteCooldown > 0f)
        {
            message = $"Пульт перезаряжается: {Mathf.CeilToInt(remoteCooldown)} сек.";
            return;
        }

        remoteJamTimer = RemoteJamDuration;
        remoteCooldown = RemoteCooldown;
        RestoreRating(RemoteRatingRestore);
        MakeNoise(PlayerCell(), 2);
        message = "Пульт глушит эфир. Дикторы вязнут в помехах.";
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

        Vector2Int previous = stone.Cell;
        stone.Cell = destination;
        stone.Target = ToWorld(destination);
        stone.Moving = true;
        RedrawTile(previous);
        RedrawTile(destination);
        MakeNoise(PlayerCell(), 5);
        RestoreRating(3f);
        message = "Заглушка скользит на соседнюю метку.";
        UpdatePuzzle();
        return true;
    }

    private void TryAttack()
    {
        if (playerView == null)
            return;

        MarkActivity();
        if (attackCooldown > 0f)
            return;

        attackCooldown = 0.34f;
        MakeNoise(PlayerCell(), 8);
        NarrativeRunState.RecordAttack();
        RestoreRating(NarrativeRunState.IsAggressive() ? 6f : 3f);

        Enemy target = null;
        float best = PlayerAttackRange;
        Vector2 player = playerView.transform.position;
        foreach (Enemy enemy in enemies)
        {
            float distance = Vector2.Distance(player, enemy.Position);
            if (distance > best)
                continue;

            Vector2 toEnemy = (enemy.Position - player).normalized;
            if (Vector2.Dot(lastAim, toEnemy) < PlayerAttackConeMinDot)
                continue;

            target = enemy;
            best = distance;
        }

        if (target == null)
        {
            if (TryBreakTrap(player))
                return;

            message = "Удар достаёт только дикторов рядом и примерно перед вами.";
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
            if (target.View != null)
                target.View.SetActive(false);
            enemies.Remove(target);
            UpdateBranchObjective();
        }
    }

    private bool TryBreakTrap(Vector2 player)
    {
        if (playerView == null)
            return false;

        Vector2Int playerCell = PlayerCell();
        Vector2Int bestCell = Vector2Int.zero;
        float best = PlayerAttackRange;
        bool found = false;

        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                Vector2Int cell = playerCell + new Vector2Int(dx, dy);
                if (!Inside(cell) || tiles[cell.x, cell.y] != Tile.Trap)
                    continue;

                Vector2 target = ToWorld(cell);
                float distance = Vector2.Distance(player, target);
                if (distance > best)
                    continue;

                Vector2 toTrap = (target - player).normalized;
                if (Vector2.Dot(lastAim, toTrap) < PlayerAttackConeMinDot)
                    continue;

                found = true;
                best = distance;
                bestCell = cell;
            }
        }

        if (!found)
            return false;

        tiles[bestCell.x, bestCell.y] = Tile.Floor;
        NarrativeRunState.RecordSignalInsight();
        RestoreRating(8f);
        RedrawTile(bestCell);
        message = "Камера хрустит и гаснет. Эфир на секунду теряет взгляд.";
        return true;
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
            UpdateBranchObjective();
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
            NarrativeRunState.RecordPuzzleSolved();
            RestoreRating(22f);
            message = "Стартовый сигнал собран. Два прохода раскрываются одновременно.";
            RedrawTile(new Vector2Int(12, 10));
        }

        bool puzzleSolved = ArePlatesCovered(puzzlePlates);
        if (NarrativeRunState.Branch == BranchChoice.Puzzle && puzzleSolved && storyRead && !puzzleExitOpen)
        {
            puzzleExitOpen = true;
            NarrativeRunState.RecordPuzzleSolved();
            RestoreRating(28f);
            message = "Смысл и сигнал совпали. Белая дверь перестаёт быть декорацией.";
            RedrawTile(new Vector2Int(34, 12));
            RedrawTile(exitPosition);
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
        RestoreRating(24f);
        message = "Боевой эфир стихает. Нижний выход открывается, но шум уже похож на вас.";
        RedrawTile(new Vector2Int(34, 8));
        RedrawTile(exitPosition);
    }

    private void UpdateEnemies(float dt)
    {
        if (playerView == null)
            return;

        foreach (Enemy enemy in enemies.ToArray())
        {
            if (enemy.View == null)
                continue;

            enemy.AttackCooldown = Mathf.Max(0f, enemy.AttackCooldown - dt);
            UpdateEnemyState(enemy);

            Vector2 target = ChooseEnemyTarget(enemy);
            float speed = enemy.Mode == EnemyMode.Hunt ? 2.55f : enemy.Mode == EnemyMode.Investigate ? 2.1f : 1.55f;
            if (RemoteJamActive())
                speed *= RemoteEnemySpeedMultiplier;
            Vector2 steeringTarget = EnemySteeringTarget(enemy, target);
            Vector2 next = Vector2.MoveTowards(enemy.Position, steeringTarget, speed * dt);
            if (CanEnemyOccupy(next, enemy))
            {
                enemy.Position = next;
            }
            else if (enemy.Mode != EnemyMode.Patrol)
            {
                Vector2 fallback = EnemyFallbackStep(enemy, speed * dt);
                if (CanEnemyOccupy(fallback, enemy))
                    enemy.Position = fallback;
            }

            enemy.View.transform.position = enemy.Position;
            SpriteRenderer enemyRenderer = enemy.View.GetComponent<SpriteRenderer>();
            if (enemyRenderer == null)
                continue;

            enemyRenderer.sprite = SpriteForEnemyMode(enemy.Mode);
            enemyRenderer.color = EnemyColor(enemy.Mode);
            if (RemoteJamActive())
                enemyRenderer.color = Color.Lerp(enemyRenderer.color, new Color(0.48f, 0.92f, 1.00f), 0.62f);

            if (!RemoteJamActive() && Vector2.Distance(enemy.Position, playerView.transform.position) <= 0.72f && enemy.AttackCooldown <= 0f)
            {
                enemy.AttackCooldown = EnemyAttackCooldown;
                DamagePlayer(1, enemy.Mode == EnemyMode.Hunt ? "Диктор догоняет вас и срывает дыхание." : "Диктор бьёт микрофоном.");
            }
        }

        if (lastNoisePower > 0)
            lastNoisePower = Mathf.Max(0, lastNoisePower - Mathf.CeilToInt(dt * 2f));
    }

    private bool RemoteJamActive()
    {
        return remoteJamTimer > 0f;
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
            return playerView != null ? playerView.transform.position : enemy.Position;

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

    private Vector2 EnemySteeringTarget(Enemy enemy, Vector2 directTarget)
    {
        if (enemy.Mode == EnemyMode.Patrol)
            return directTarget;

        Vector2Int from = WorldToCell(enemy.Position);
        Vector2Int to = WorldToCell(directTarget);
        if (from == to || HasStraightWalkLine(from, to))
            return directTarget;

        if (TryFindNextPathCell(from, to, out Vector2Int nextCell))
            return ToWorld(nextCell);

        return directTarget;
    }

    private Vector2 EnemyFallbackStep(Enemy enemy, float distance)
    {
        Vector2Int cell = WorldToCell(enemy.Position);
        Vector2Int goal = enemy.Mode == EnemyMode.Hunt ? PlayerCell() : enemy.LastSeen;
        Vector2Int bestCell = cell;
        int bestScore = Manhattan(cell, goal);

        foreach (Vector2Int next in CardinalCells(cell))
        {
            if (!CanEnemyEnterCell(next, enemy))
                continue;

            int score = Manhattan(next, goal);
            if (score < bestScore)
            {
                bestScore = score;
                bestCell = next;
            }
        }

        return bestCell == cell ? enemy.Position : Vector2.MoveTowards(enemy.Position, ToWorld(bestCell), distance);
    }

    private bool TryFindNextPathCell(Vector2Int start, Vector2Int goal, out Vector2Int nextCell)
    {
        nextCell = start;
        if (!Inside(start) || !Inside(goal))
            return false;

        Array.Fill(pathVisited, false);
        Array.Fill(pathParents, new Vector2Int(-1, -1));
        pathQueue.Clear();

        int startIndex = CellIndex(start);
        pathVisited[startIndex] = true;
        pathQueue.Enqueue(start);

        while (pathQueue.Count > 0)
        {
            Vector2Int current = pathQueue.Dequeue();
            if (current == goal)
                break;

            foreach (Vector2Int next in CardinalCells(current))
            {
                if (!Inside(next))
                    continue;

                int index = CellIndex(next);
                if (pathVisited[index] || !EnemyPathPassable(next, goal))
                    continue;

                pathVisited[index] = true;
                pathParents[index] = current;
                pathQueue.Enqueue(next);
            }
        }

        if (!pathVisited[CellIndex(goal)])
            return false;

        Vector2Int step = goal;
        while (pathParents[CellIndex(step)] != start)
        {
            step = pathParents[CellIndex(step)];
            if (step.x < 0)
                return false;
        }

        nextCell = step;
        return true;
    }

    private bool HasStraightWalkLine(Vector2Int from, Vector2Int to)
    {
        if (from.x == to.x)
        {
            int step = Math.Sign(to.y - from.y);
            for (int y = from.y + step; y != to.y; y += step)
            {
                if (!EnemyPathPassable(new Vector2Int(from.x, y), to))
                    return false;
            }
            return true;
        }

        if (from.y == to.y)
        {
            int step = Math.Sign(to.x - from.x);
            for (int x = from.x + step; x != to.x; x += step)
            {
                if (!EnemyPathPassable(new Vector2Int(x, from.y), to))
                    return false;
            }
            return true;
        }

        return false;
    }

    private bool CanEnemyOccupy(Vector2 position, Enemy self)
    {
        Vector2Int cell = WorldToCell(position);
        if (!CanEnemyEnterCell(cell, self))
            return false;

        foreach (Enemy enemy in enemies)
        {
            if (enemy != self && Vector2.Distance(enemy.Position, position) < 0.55f)
                return false;
        }

        return true;
    }

    private bool CanEnemyEnterCell(Vector2Int cell, Enemy self)
    {
        Vector2Int playerCell = playerView != null ? WorldToCell(playerView.transform.position) : new Vector2Int(3, 10);
        if (!EnemyPathPassable(cell, playerCell))
            return false;

        Enemy occupant = EnemyAt(cell);
        return occupant == null || occupant == self;
    }

    private bool EnemyPathPassable(Vector2Int cell, Vector2Int goal)
    {
        if (!Inside(cell))
            return false;
        if (cell == goal)
            return true;

        return !IsSolidCell(cell) && StoneAt(cell) == null;
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

        return IsSolidCell(cell) || StoneAt(cell) != null;
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
            if (stone.View == null)
                continue;

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
        return Inside(cell) && !IsSolidCell(cell) && StoneAt(cell) == null && EnemyAt(cell) == null;
    }

    private void BuildLevel()
    {
        startPlates.Clear();
        puzzlePlates.Clear();
        stones.Clear();
        enemies.Clear();

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
        AddGateWithFrame(new Vector2Int(12, 10), Vector2Int.up, Vector2Int.down);
        AddGateWithFrame(new Vector2Int(34, 12), Vector2Int.left, Vector2Int.right);
        AddGateWithFrame(new Vector2Int(34, 8), Vector2Int.left, Vector2Int.right);
        SetWall(new Vector2Int(36, 12));
        SetWall(new Vector2Int(36, 8));
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

        ResetPlayerTransformIfBound();
    }

    private void ResetPlayerTransformIfBound()
    {
        if (playerView == null)
            return;

        playerView.transform.position = ToWorld(new Vector2Int(3, 10));
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
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

    private void AddGateWithFrame(Vector2Int gate, Vector2Int sideA, Vector2Int sideB)
    {
        tiles[gate.x, gate.y] = Tile.Gate;
        SetWall(gate + sideA);
        SetWall(gate + sideB);
        SealGateCorners(gate, sideA, sideB);
    }

    private void SealGateCorners(Vector2Int gate, Vector2Int sideA, Vector2Int sideB)
    {
        if (sideA.x == 0 && sideB.x == 0)
        {
            SetWall(gate + Vector2Int.left + sideA);
            SetWall(gate + Vector2Int.left + sideB);
            SetWall(gate + Vector2Int.right + sideA);
            SetWall(gate + Vector2Int.right + sideB);
        }
        else
        {
            SetWall(gate + Vector2Int.up + sideA);
            SetWall(gate + Vector2Int.up + sideB);
            SetWall(gate + Vector2Int.down + sideA);
            SetWall(gate + Vector2Int.down + sideB);
        }
    }

    private void SetWall(Vector2Int cell)
    {
        if (Inside(cell))
            tiles[cell.x, cell.y] = Tile.Wall;
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
        ThrowIfPlayingBake("CreateViews");
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

                GameObject floorDecalView = new GameObject($"Floor Decal {x},{y}");
                floorDecalView.transform.SetParent(tileRoot.transform);
                floorDecalView.transform.position = ToWorld(cell);
                SpriteRenderer decalRenderer = floorDecalView.AddComponent<SpriteRenderer>();
                SetLitMaterial(decalRenderer);
                decalRenderer.sortingOrder = -1;
                floorDecalViews[x, y] = floorDecalView;

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
        ThrowIfPlayingBake("CreateEntityViews");
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
            GameObject view = new GameObject($"Signal Blocker {stone.Cell.x},{stone.Cell.y}");
            view.transform.position = ToWorld(stone.Cell);
            view.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = stoneSprite;
            SetLitMaterial(renderer);
            renderer.sortingOrder = 12;
            var collider = view.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.95f, 0.95f);
            stone.View = view;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            enemy.View = new GameObject($"Enemy {i}");
            enemy.View.transform.position = enemy.Position;
            var renderer = enemy.View.AddComponent<SpriteRenderer>();
            renderer.sprite = enemySprite;
            SetLitMaterial(renderer);
            renderer.sortingOrder = 15;
        }
    }

    private void CreateLighting()
    {
        ThrowIfPlayingBake("CreateLighting");
        Urp2DLighting.AddGlobalLight(gameObject, new Color(0.64f, 0.67f, 0.70f), 0.92f);

        var channelLightObject = new GameObject("Channel Light");
        channelLightObject.transform.SetParent(transform);
        channelLightObject.transform.position = new Vector3(14f, 10f, 0f);
        Urp2DLighting.AddPointLight(channelLightObject, new Color(0.62f, 0.78f, 0.94f), 0.28f, 10.0f, 1.5f);
    }

    private bool BindSceneViews()
    {
        Transform tileRoot = GameObject.Find("Tiles")?.transform;
        if (tileRoot == null)
            return false;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                floorViews[x, y] = FindChildObject(tileRoot, $"Floor {x},{y}");
                floorDecalViews[x, y] = FindChildObject(tileRoot, $"Floor Decal {x},{y}");
                tileViews[x, y] = FindChildObject(tileRoot, $"Tile {x},{y}");
                if (floorViews[x, y] == null || floorDecalViews[x, y] == null || tileViews[x, y] == null)
                    return false;
            }
        }

        playerView = GameObject.Find("Player");
        if (playerView == null)
            return false;

        playerBody = playerView.GetComponent<Rigidbody2D>();
        if (playerBody == null)
            return false;

        playerBody.bodyType = RigidbodyType2D.Dynamic;
        playerBody.simulated = true;
        playerBody.gravityScale = 0f;
        playerBody.freezeRotation = true;
        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (playerView.TryGetComponent(out CircleCollider2D playerCollider))
            playerCollider.enabled = true;

        playerView.transform.position = ToWorld(new Vector2Int(3, 10));
        playerBody.linearVelocity = Vector2.zero;

        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            stone.View = GameObject.Find($"Signal Blocker {stone.Cell.x},{stone.Cell.y}");
            if (stone.View == null)
                return false;

            stone.View.transform.position = ToWorld(stone.Cell);
            stone.View.SetActive(true);
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            enemy.View = GameObject.Find($"Enemy {i}");
            if (enemy.View == null)
                return false;

            enemy.View.transform.position = enemy.Position;
            enemy.View.SetActive(true);
        }

        return true;
    }

    private static GameObject FindChildObject(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        return child == null ? null : child.gameObject;
    }

    private bool HasBakedAssets()
    {
        return floorSprite != null &&
               wallSprite != null &&
               plateSprite != null &&
               pressedPlateSprite != null &&
               gateSprite != null &&
               openGateSprite != null &&
               exitSprite != null &&
               rubbleSprite != null &&
               trapSprite != null &&
               remoteSprite != null &&
               storySprite != null &&
               playerSprite != null &&
               stoneSprite != null &&
               enemySprite != null &&
               enemyInvestigateSprite != null &&
               enemyHuntSprite != null &&
               hudTexture != null &&
               whiteTexture != null &&
               floorSprites != null &&
               floorSprites.Length > 0 &&
               playerIdleSprites != null &&
               playerIdleSprites.Length == 4 &&
               playerWalkOneSprites != null &&
               playerWalkOneSprites.Length == 4 &&
               playerWalkTwoSprites != null &&
               playerWalkTwoSprites.Length == 4 &&
               playerAttackSprites != null &&
               playerAttackSprites.Length == 4;
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
            if (enemy.View == null)
                continue;

            enemy.View.transform.position = enemy.Position;
            SpriteRenderer enemyRenderer = enemy.View.GetComponent<SpriteRenderer>();
            if (enemyRenderer == null)
                continue;

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
        SpriteRenderer decalRenderer = floorDecalViews[cell.x, cell.y].GetComponent<SpriteRenderer>();
        bool hasFloor = tiles[cell.x, cell.y] != Tile.Wall;
        floorRenderer.sprite = hasFloor ? FloorSpriteFor(cell) : null;
        decalRenderer.sprite = hasFloor && tiles[cell.x, cell.y] == Tile.Floor ? FloorDecalFor(cell) : null;

        tileViews[cell.x, cell.y].transform.localRotation = Quaternion.identity;
        tileViews[cell.x, cell.y].transform.localScale = OverlayScaleFor(tiles[cell.x, cell.y]);
        renderer.sprite = OverlaySpriteFor(tiles[cell.x, cell.y], cell);
        renderer.color = OverlayColorFor(tiles[cell.x, cell.y], cell);
        if (tiles[cell.x, cell.y] == Tile.Wall)
            ApplyWallTransform(cell, tileViews[cell.x, cell.y].transform);
        renderer.sortingOrder = tiles[cell.x, cell.y] == Tile.Rubble ? 3 : tiles[cell.x, cell.y] == Tile.Wall ? 1 : 2;

        BoxCollider2D collider = tileViews[cell.x, cell.y].GetComponent<BoxCollider2D>();
        bool shouldCollide = IsSolidCell(cell);
        if (collider == null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                collider = tileViews[cell.x, cell.y].AddComponent<BoxCollider2D>();
                collider.size = Vector2.one;
            }
            else
#endif
            if (shouldCollide)
            {
                Debug.LogError($"Missing baked collider on solid tile {cell.x},{cell.y}.");
                return;
            }
        }

        if (collider != null)
        {
            collider.size = Vector2.one;
            collider.enabled = shouldCollide;
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
        renderer.flipX = direction == FacingDirection.Right;
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
            Tile.Wall => WallSpriteFor(cell),
            Tile.Plate => StoneAt(cell) != null ? pressedPlateSprite : plateSprite,
            Tile.Gate => GateOpenForCell(cell) ? openGateSprite : gateSprite,
            Tile.Rubble => rubbleSprite,
            Tile.Trap => trapSprite,
            Tile.Remote => remoteSprite,
            Tile.Story => storySprite,
            Tile.Exit => CanUseExit() ? openExitSprite ?? exitSprite : exitSprite,
            _ => null,
        };
    }

    private Color OverlayColorFor(Tile tile, Vector2Int cell)
    {
        if (tile == Tile.Exit)
            return CanUseExit() ? Color.white : new Color(0.48f, 0.55f, 0.62f, 0.72f);

        if (tile == Tile.Gate && GateOpenForCell(cell))
            return new Color(0.78f, 0.96f, 1.00f, 0.88f);

        return Color.white;
    }

    private static Vector3 OverlayScaleFor(Tile tile)
    {
        return tile switch
        {
            Tile.Remote => new Vector3(1.25f, 1.25f, 1f),
            Tile.Story => new Vector3(1.15f, 1.15f, 1f),
            Tile.Exit => new Vector3(1.10f, 1.10f, 1f),
            _ => Vector3.one,
        };
    }

    private Sprite WallSpriteFor(Vector2Int cell)
    {
        if (!WallVisibleFor(cell))
            return null;

        bool up = OpenTileAdjacent(cell + Vector2Int.up);
        bool down = OpenTileAdjacent(cell + Vector2Int.down);
        bool left = OpenTileAdjacent(cell + Vector2Int.left);
        bool right = OpenTileAdjacent(cell + Vector2Int.right);
        int openCount = BoolCount(up, down, left, right);

        if (openCount == 2 && HasCornerOpenPattern(up, down, left, right))
            return wallCornerSprite ?? wallSprite;

        int verticalWeight = BoolCount(left, right);
        int horizontalWeight = BoolCount(up, down);
        return verticalWeight > horizontalWeight ? wallVerticalSprite ?? wallSprite : wallSprite;
    }

    private void ApplyWallTransform(Vector2Int cell, Transform target)
    {
        target.localRotation = Quaternion.Euler(0f, 0f, WallRotationFor(cell));
    }

    private float WallRotationFor(Vector2Int cell)
    {
        bool up = OpenTileAdjacent(cell + Vector2Int.up);
        bool down = OpenTileAdjacent(cell + Vector2Int.down);
        bool left = OpenTileAdjacent(cell + Vector2Int.left);
        bool right = OpenTileAdjacent(cell + Vector2Int.right);

        if (up && right && !down && !left)
            return 0f;
        if (right && down && !left && !up)
            return 90f;
        if (down && left && !up && !right)
            return 180f;
        if (left && up && !right && !down)
            return 270f;

        return 0f;
    }

    private static bool HasCornerOpenPattern(bool up, bool down, bool left, bool right)
    {
        return (up && right) || (right && down) || (down && left) || (left && up);
    }

    private static int BoolCount(params bool[] values)
    {
        int count = 0;
        foreach (bool value in values)
        {
            if (value)
                count++;
        }

        return count;
    }

    private bool GateOpenForCell(Vector2Int cell)
    {
        return cell == new Vector2Int(12, 10) && startGateOpen ||
               cell == new Vector2Int(34, 12) && puzzleExitOpen ||
               cell == new Vector2Int(34, 8) && combatExitOpen;
    }

    private bool WallVisibleFor(Vector2Int cell)
    {
        return OpenTileAdjacent(cell + Vector2Int.up) ||
               OpenTileAdjacent(cell + Vector2Int.down) ||
               OpenTileAdjacent(cell + Vector2Int.left) ||
               OpenTileAdjacent(cell + Vector2Int.right);
    }

    private bool OpenTileAdjacent(Vector2Int cell)
    {
        return Inside(cell) && tiles[cell.x, cell.y] != Tile.Wall;
    }

    private bool TryApplyCharacterAtlas()
    {
        try
        {
            SetPlayerSpritesFromFixedAtlas(FacingDirection.Down, 0);
            SetPlayerSpritesFromFixedAtlas(FacingDirection.Up, 1);
            SetPlayerSpritesFromFixedAtlas(FacingDirection.Left, 2);
            CopyPlayerSprites(FacingDirection.Right, FacingDirection.Left);
            playerSprite = playerIdleSprites[(int)FacingDirection.Down];

            enemySprite = CreateFixedAtlasSprite(CharacterAtlas, 4, 0, "anchor_patrol_down");
            enemyInvestigateSprite = CreateFixedAtlasSprite(CharacterAtlas, 4, 2, "anchor_investigate_down");
            enemyHuntSprite = CreateFixedAtlasSprite(CharacterAtlas, 4, 3, "anchor_hunt_down");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Character atlas could not be sliced, keeping fallback characters: {ex.Message}");
            return false;
        }
    }

    private bool TryApplyEnvironmentAtlas()
    {
        try
        {
            floorSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 0, 0, "floor_base");
            floorSprites = new[] { floorSprite };

            floorDecalSprites = new[]
            {
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 0, "floor_decal_0", true),
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 1, "floor_decal_1", true),
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 2, "floor_decal_2", true),
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 3, "floor_decal_3", true),
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 4, "floor_decal_4", true),
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 5, "floor_decal_5", true),
                CreateFixedAtlasSprite(EnvironmentAtlas, 1, 6, "floor_decal_6", true),
            };

            wallSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 2, 0, "wall_straight");
            wallVerticalSprite = wallSprite;
            wallCornerSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 2, 1, "wall_corner");
            gateSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 3, 0, "signal_gate_closed", true);
            openGateSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 3, 1, "signal_gate_open", true);
            exitSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 3, 6, "tv_exit", true);
            openExitSprite = exitSprite;
            plateSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 0, "pressure_plate", true);
            pressedPlateSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 1, "pressure_plate_pressed", true);
            stoneSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 2, "signal_blocker", true);
            remoteSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 3, "remote", true);
            storySprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 4, "story_note", true);
            trapSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 5, "camera_trap", true);
            rubbleSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 4, 6, "rubble", true);
            TryApplyWallAtlas();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Environment atlas could not be sliced, keeping fallback environment: {ex.Message}");
            return false;
        }
    }

    private void TryApplyWallAtlas()
    {
        if (WallAtlas == null)
            return;

        try
        {
            wallSprite = CreateFixedAtlasSprite(WallAtlas, 0, 0, "wall_horizontal");
            wallVerticalSprite = CreateFixedAtlasSprite(WallAtlas, 0, 1, "wall_vertical");
            wallCornerSprite = CreateFixedAtlasSprite(WallAtlas, 0, 2, "wall_corner");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Wall atlas could not be sliced, keeping environment wall sprites: {ex.Message}");
        }
    }

    private void SetPlayerSpritesFromFixedAtlas(FacingDirection direction, int row)
    {
        int index = (int)direction;
        playerIdleSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, 0, $"player_{direction}_idle");
        playerWalkOneSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, 1, $"player_{direction}_walk_1");
        playerWalkTwoSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, 2, $"player_{direction}_walk_2");
        playerAttackSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, 3, $"player_{direction}_attack");
    }

    private void CopyPlayerSprites(FacingDirection target, FacingDirection source)
    {
        int targetIndex = (int)target;
        int sourceIndex = (int)source;
        playerIdleSprites[targetIndex] = playerIdleSprites[sourceIndex];
        playerWalkOneSprites[targetIndex] = playerWalkOneSprites[sourceIndex];
        playerWalkTwoSprites[targetIndex] = playerWalkTwoSprites[sourceIndex];
        playerAttackSprites[targetIndex] = playerAttackSprites[sourceIndex];
    }

    private Sprite CreateFixedAtlasSprite(Texture2D atlas, int row, int column, string spriteName, bool removeCellBackground = false)
    {
        ThrowIfPlayingBake("CreateFixedAtlasSprite");
        if (atlas == null)
            throw new InvalidOperationException("Atlas texture is not assigned.");
        if (row < 0 || row >= FixedAtlasRows || column < 0 || column >= FixedAtlasColumns)
            throw new InvalidOperationException($"Fixed atlas cell {column},{row} is outside 8x8 grid.");

        int cellWidth = atlas.width / FixedAtlasColumns;
        int cellHeight = atlas.height / FixedAtlasRows;
        int sourceX = column * cellWidth;
        int sourceY = atlas.height - (row + 1) * cellHeight;
        return CreateSpriteFromAtlasPixels(atlas, sourceX, sourceY, cellWidth, cellHeight, spriteName, cellWidth, removeCellBackground);
    }

    private Texture2D CreateHudAtlasTexture(int row, int column, string textureName, bool removeCellBackground)
    {
        ThrowIfPlayingBake("CreateHudAtlasTexture");
        if (HudAtlas == null)
            throw new InvalidOperationException("HUD atlas texture is not assigned.");
        if (row < 0 || row >= HudAtlasRows || column < 0 || column >= HudAtlasColumns)
            throw new InvalidOperationException($"HUD atlas cell {column},{row} is outside 4x4 grid.");

        int cellWidth = HudAtlas.width / HudAtlasColumns;
        int cellHeight = HudAtlas.height / HudAtlasRows;
        int sourceX = column * cellWidth;
        int sourceY = HudAtlas.height - (row + 1) * cellHeight;
        Color[] pixels = HudAtlas.GetPixels(sourceX, sourceY, cellWidth, cellHeight);
        Color background = removeCellBackground ? SampleCellBackground(pixels, cellWidth, cellHeight) : Color.clear;
        var texture = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = textureName,
        };

        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % cellWidth;
            int y = i / cellWidth;
            bool atlasEdge = x <= 1 || y <= 1 || x >= cellWidth - 2 || y >= cellHeight - 2;
            if (atlasEdge || IsChromaGreen(pixels[i]) || removeCellBackground && SimilarToBackground(pixels[i], background))
                pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Sprite CreateSpriteFromAtlasPixels(Texture2D atlas, int sourceX, int sourceY, int width, int height, string spriteName, float pixelsPerUnit, bool removeCellBackground)
    {
        ThrowIfPlayingBake("CreateSpriteFromAtlasPixels");
        Color[] pixels = atlas.GetPixels(sourceX, sourceY, width, height);
        Color background = removeCellBackground ? SampleCellBackground(pixels, width, height) : Color.clear;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = spriteName,
        };

        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % width;
            int y = i / width;
            bool atlasEdge = x <= 1 || y <= 1 || x >= width - 2 || y >= height - 2;
            if (atlasEdge || IsChromaGreen(pixels[i]) || removeCellBackground && SimilarToBackground(pixels[i], background))
                pixels[i] = new Color(0f, 0f, 0f, 0f);
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    private static Color SampleCellBackground(Color[] pixels, int width, int height)
    {
        Color total = Color.clear;
        int count = 0;
        AccumulateCorner(pixels, width, 4, 4, ref total, ref count);
        AccumulateCorner(pixels, width, width - 9, 4, ref total, ref count);
        AccumulateCorner(pixels, width, 4, height - 9, ref total, ref count);
        AccumulateCorner(pixels, width, width - 9, height - 9, ref total, ref count);
        return count == 0 ? Color.clear : total / count;
    }

    private static void AccumulateCorner(Color[] pixels, int width, int startX, int startY, ref Color total, ref int count)
    {
        for (int y = startY; y < startY + 5; y++)
        {
            for (int x = startX; x < startX + 5; x++)
            {
                total += pixels[y * width + x];
                count++;
            }
        }
    }

    private static bool SimilarToBackground(Color color, Color background)
    {
        float dr = color.r - background.r;
        float dg = color.g - background.g;
        float db = color.b - background.b;
        float da = color.a - background.a;
        return dr * dr + dg * dg + db * db + da * da < 0.014f;
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
        ThrowIfPlayingBake("CreateQuietFloorSprite");
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
        ThrowIfPlayingBake("CreateQuietWallSprite");
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

#if UNITY_EDITOR
    public void BakeSceneForEditor()
    {
        ClearBakedSceneObjects();
        SetupCamera();
        CreateSprites();
        PersistGeneratedSpritesForEditor();
        EnsureHudTextures();
        PersistHudTexturesForEditor();
        BuildLevel();
        CreateViews();
        CreateLighting();
        RedrawAll();
        EditorUtility.SetDirty(this);
    }

    private void ClearBakedSceneObjects()
    {
        DestroySceneObject("Tiles");
        DestroySceneObject("Player");
        DestroySceneObject("Channel Light");

        foreach (Light2D light in GetComponents<Light2D>())
            DestroyImmediate(light);

        foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (obj.scene != gameObject.scene)
                continue;

            if (obj.name.StartsWith("Signal Blocker ", StringComparison.Ordinal) ||
                obj.name.StartsWith("Enemy ", StringComparison.Ordinal))
            {
                DestroyImmediate(obj);
            }
        }
    }

    private static void DestroySceneObject(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
            DestroyImmediate(obj);
    }

    private void PersistGeneratedSpritesForEditor()
    {
        const string folder = "Assets/Generated/Prototype/Sprites";
        floorSprite = PersistSpriteForEditor(folder, "floor_base", floorSprite);
        wallSprite = PersistSpriteForEditor(folder, "wall_horizontal", wallSprite);
        wallVerticalSprite = PersistSpriteForEditor(folder, "wall_vertical", wallVerticalSprite);
        wallCornerSprite = PersistSpriteForEditor(folder, "wall_corner", wallCornerSprite);
        plateSprite = PersistSpriteForEditor(folder, "pressure_plate", plateSprite);
        pressedPlateSprite = PersistSpriteForEditor(folder, "pressure_plate_pressed", pressedPlateSprite);
        gateSprite = PersistSpriteForEditor(folder, "signal_gate_closed", gateSprite);
        openGateSprite = PersistSpriteForEditor(folder, "signal_gate_open", openGateSprite);
        exitSprite = PersistSpriteForEditor(folder, "tv_exit", exitSprite);
        openExitSprite = PersistSpriteForEditor(folder, "tv_exit_open", openExitSprite);
        rubbleSprite = PersistSpriteForEditor(folder, "rubble", rubbleSprite);
        trapSprite = PersistSpriteForEditor(folder, "camera_trap", trapSprite);
        remoteSprite = PersistSpriteForEditor(folder, "remote", remoteSprite);
        storySprite = PersistSpriteForEditor(folder, "story_note", storySprite);
        playerSprite = PersistSpriteForEditor(folder, "player_base", playerSprite);
        stoneSprite = PersistSpriteForEditor(folder, "signal_blocker", stoneSprite);
        enemySprite = PersistSpriteForEditor(folder, "enemy_patrol", enemySprite);
        enemyInvestigateSprite = PersistSpriteForEditor(folder, "enemy_investigate", enemyInvestigateSprite);
        enemyHuntSprite = PersistSpriteForEditor(folder, "enemy_hunt", enemyHuntSprite);
        floorSprites = PersistSpriteArrayForEditor(folder, "floor", floorSprites);
        floorDecalSprites = PersistSpriteArrayForEditor(folder, "floor_decal", floorDecalSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_idle", playerIdleSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_walk_1", playerWalkOneSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_walk_2", playerWalkTwoSprites);
        PersistPlayerSpriteArrayForEditor(folder, "player_attack", playerAttackSprites);
    }

    private void PersistHudTexturesForEditor()
    {
        const string folder = "Assets/Generated/Prototype/HUD";
        hudTexture = PersistTextureForEditor(folder, "hud_fill", hudTexture);
        hudPanelTexture = PersistTextureForEditor(folder, "hud_panel", hudPanelTexture);
        ratingFrameNeutralTexture = PersistTextureForEditor(folder, "rating_frame_neutral", ratingFrameNeutralTexture);
        ratingFramePuzzleTexture = PersistTextureForEditor(folder, "rating_frame_puzzle", ratingFramePuzzleTexture);
        ratingFrameCombatTexture = PersistTextureForEditor(folder, "rating_frame_combat", ratingFrameCombatTexture);
        ratingFrameCriticalTexture = PersistTextureForEditor(folder, "rating_frame_critical", ratingFrameCriticalTexture);
        whiteTexture = PersistTextureForEditor(folder, "white", whiteTexture);
    }

    private static Sprite[] PersistSpriteArrayForEditor(string folder, string prefix, Sprite[] sprites)
    {
        if (sprites == null)
            return Array.Empty<Sprite>();

        var persisted = new Sprite[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            persisted[i] = PersistSpriteForEditor(folder, $"{prefix}_{i}", sprites[i]);

        return persisted;
    }

    private static void PersistPlayerSpriteArrayForEditor(string folder, string prefix, Sprite[] sprites)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
            sprites[i] = PersistSpriteForEditor(folder, $"{prefix}_{i}", sprites[i]);
    }

    private static Sprite PersistSpriteForEditor(string folder, string assetName, Sprite sprite)
    {
        if (sprite == null || AssetDatabase.Contains(sprite))
            return sprite;

        Texture2D texture = CopySpriteTexture(sprite);
        Texture2D importedTexture = PersistTextureForEditor(folder, assetName, texture);
        UnityEngine.Object.DestroyImmediate(texture);

        string path = $"{folder}/{assetName}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? Sprite.Create(importedTexture, new Rect(0, 0, importedTexture.width, importedTexture.height), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
    }

    private static Texture2D PersistTextureForEditor(string folder, string assetName, Texture2D texture)
    {
        if (texture == null || AssetDatabase.Contains(texture))
            return texture;

        EnsureAssetFolder(folder);
        string path = $"{folder}/{assetName}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D CopySpriteTexture(Sprite sprite)
    {
        Rect rect = sprite.rect;
        var texture = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            name = sprite.name,
        };
        texture.SetPixels(sprite.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), texture.width, texture.height));
        texture.Apply(false, false);
        return texture;
    }

    private static void EnsureAssetFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
#endif

    private void CreateSprites()
    {
        ThrowIfPlayingBake("CreateSprites");
        CreateFallbackSprites();

        if (EnvironmentAtlas != null)
            TryApplyEnvironmentAtlas();
        if (CharacterAtlas != null)
            TryApplyCharacterAtlas();
    }

    private void CreateFallbackSprites()
    {
        ThrowIfPlayingBake("CreateFallbackSprites");
        floorSprite = CreateSprite(new Color(0.09f, 0.10f, 0.11f), new Color(0.15f, 0.16f, 0.18f), new Color(0.18f, 0.22f, 0.25f), SpriteMark.None);
        floorSprites = new[] { floorSprite };
        floorDecalSprites = new[]
        {
            CreateDecalSprite(new Color(0.65f, 0.72f, 0.76f, 0.72f), SpriteMark.Story),
            CreateDecalSprite(new Color(0.25f, 0.40f, 0.48f, 0.70f), SpriteMark.Remote),
            CreateDecalSprite(new Color(0.80f, 0.18f, 0.22f, 0.58f), SpriteMark.Trap),
        };
        wallSprite = CreateSprite(new Color(0.22f, 0.24f, 0.27f), new Color(0.12f, 0.13f, 0.15f), new Color(0.52f, 0.56f, 0.60f), SpriteMark.None);
        wallVerticalSprite = wallSprite;
        wallCornerSprite = wallSprite;
        plateSprite = CreateSprite(new Color(0.28f, 0.25f, 0.18f), new Color(0.11f, 0.10f, 0.08f), new Color(0.95f, 0.82f, 0.36f), SpriteMark.Plate);
        pressedPlateSprite = CreateSprite(new Color(0.12f, 0.24f, 0.26f), new Color(0.04f, 0.10f, 0.12f), new Color(0.66f, 0.92f, 1.00f), SpriteMark.Plate);
        gateSprite = CreateSprite(new Color(0.34f, 0.10f, 0.14f), new Color(0.12f, 0.05f, 0.06f), new Color(0.90f, 0.18f, 0.24f), SpriteMark.Gate);
        openGateSprite = CreateSprite(new Color(0.10f, 0.30f, 0.28f), new Color(0.04f, 0.12f, 0.13f), new Color(0.66f, 0.92f, 1.00f), SpriteMark.Gate);
        exitSprite = CreateSprite(new Color(0.82f, 0.88f, 0.92f), new Color(0.56f, 0.66f, 0.72f), new Color(0.12f, 0.18f, 0.22f), SpriteMark.Exit);
        openExitSprite = exitSprite;
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

    private static Sprite CreateDecalSprite(Color color, SpriteMark mark)
    {
        ThrowIfPlayingBake("CreateDecalSprite");
        const int size = 16;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
        };
        Fill(texture, new Color(0f, 0f, 0f, 0f));
        DrawMark(texture, mark, color);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }

    private static void Fill(Texture2D texture, Color color)
    {
        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
                texture.SetPixel(x, y, color);
        }
    }

    private static Sprite CreateSprite(Color baseColor, Color edgeColor, Color markColor, SpriteMark mark)
    {
        ThrowIfPlayingBake("CreateSprite");
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
            if (Application.isPlaying)
            {
                Debug.LogError("Prototype scene is missing a baked Main Camera.");
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = GameplayCameraSize;
        camera.transform.position = new Vector3(8f, 10f, -10f);
        camera.backgroundColor = new Color(0.070f, 0.076f, 0.086f);
    }

    private Sprite FloorSpriteFor(Vector2Int cell)
    {
        if (floorSprites.Length == 0)
            return floorSprite;

        return floorSprites[CellHash(cell, 11) % floorSprites.Length];
    }

    private Sprite FloorDecalFor(Vector2Int cell)
    {
        if (floorDecalSprites.Length == 0 || DecalSuppressed(cell))
            return null;

        int adjacentWalls = NearbyWallWeight(cell);
        int roll = CellHash(cell, 29) % 100;
        int chance = adjacentWalls >= 4 ? 20 : adjacentWalls >= 2 ? 9 : 2;
        if (roll >= chance)
            return null;

        return floorDecalSprites[CellHash(cell, 47) % floorDecalSprites.Length];
    }

    private bool DecalSuppressed(Vector2Int cell)
    {
        if (Manhattan(cell, new Vector2Int(3, 10)) <= 2 || StoneAt(cell) != null || EnemyAt(cell) != null)
            return true;

        foreach (Vector2Int next in NeighborCells(cell))
        {
            if (!Inside(next))
                continue;

            if (DecalBlockingTile(tiles[next.x, next.y]) || StoneAt(next) != null || EnemyAt(next) != null)
                return true;
        }

        return false;
    }

    private static bool DecalBlockingTile(Tile tile)
    {
        return tile == Tile.Plate ||
               tile == Tile.Gate ||
               tile == Tile.Rubble ||
               tile == Tile.Trap ||
               tile == Tile.Remote ||
               tile == Tile.Story ||
               tile == Tile.Exit;
    }

    private int NearbyWallWeight(Vector2Int cell)
    {
        int count = 0;
        if (ClosedWallAdjacent(cell + Vector2Int.up))
            count += 2;
        if (ClosedWallAdjacent(cell + Vector2Int.down))
            count += 2;
        if (ClosedWallAdjacent(cell + Vector2Int.left))
            count += 2;
        if (ClosedWallAdjacent(cell + Vector2Int.right))
            count += 2;
        if (ClosedWallAdjacent(cell + new Vector2Int(1, 1)))
            count++;
        if (ClosedWallAdjacent(cell + new Vector2Int(1, -1)))
            count++;
        if (ClosedWallAdjacent(cell + new Vector2Int(-1, 1)))
            count++;
        if (ClosedWallAdjacent(cell + new Vector2Int(-1, -1)))
            count++;

        return count;
    }

    private bool ClosedWallAdjacent(Vector2Int cell)
    {
        return Inside(cell) && tiles[cell.x, cell.y] == Tile.Wall;
    }

    private static int CellHash(Vector2Int cell, int salt)
    {
        unchecked
        {
            uint hash = (uint)(cell.x * 73856093) ^ (uint)(cell.y * 19349663) ^ (uint)(salt * 83492791);
            return (int)(hash & 0x7fffffff);
        }
    }

    private static float ClampCameraAxis(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }

    private void EnsureHudTextures()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (HudAtlas != null && ratingFrameNeutralTexture == null)
        {
            try
            {
                ratingFrameNeutralTexture = CreateHudAtlasTexture(0, 0, "rating_frame_neutral", true);
                ratingFramePuzzleTexture = CreateHudAtlasTexture(0, 1, "rating_frame_puzzle", true);
                ratingFrameCombatTexture = CreateHudAtlasTexture(0, 2, "rating_frame_combat", true);
                ratingFrameCriticalTexture = CreateHudAtlasTexture(0, 3, "rating_frame_critical", true);
                CutRatingFrameGaugeSlot(ratingFrameNeutralTexture);
                CutRatingFrameGaugeSlot(ratingFramePuzzleTexture);
                CutRatingFrameGaugeSlot(ratingFrameCombatTexture);
                CutRatingFrameGaugeSlot(ratingFrameCriticalTexture);
                hudPanelTexture = CreateHudAtlasTexture(1, 0, "hud_panel", false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"HUD atlas could not be sliced, keeping fallback HUD: {ex.Message}");
                ratingFrameNeutralTexture = Texture2D.whiteTexture;
            }
        }

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
#endif
    }

    private static void ThrowIfPlayingBake(string method)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException($"{method} is editor-bake only and must not run in Play Mode.");
    }

    private static void CutRatingFrameGaugeSlot(Texture2D texture)
    {
        if (texture == null)
            return;

        int minX = Mathf.RoundToInt(texture.width * 0.34f);
        int maxX = Mathf.RoundToInt(texture.width * 0.66f);
        int minY = Mathf.RoundToInt(texture.height * 0.13f);
        int maxY = Mathf.RoundToInt(texture.height * 0.84f);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
                texture.SetPixel(x, y, Color.clear);
        }

        texture.Apply(false, false);
    }

    private Vector2Int PlayerCell()
    {
        if (playerView == null)
            return new Vector2Int(3, 10);

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

    private IEnumerable<Vector2Int> CardinalCells(Vector2Int cell)
    {
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

    private bool IsSolidCell(Vector2Int cell)
    {
        Tile tile = tiles[cell.x, cell.y];
        return tile == Tile.Wall || tile == Tile.Rubble || (tile == Tile.Gate && !GateOpenForCell(cell));
    }

    private static bool Inside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }

    private static int CellIndex(Vector2Int cell)
    {
        return cell.y * Width + cell.x;
    }
}
