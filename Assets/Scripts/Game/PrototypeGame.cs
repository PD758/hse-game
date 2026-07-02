using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed partial class PrototypeGame : MonoBehaviour
{
    private const int Width = 39;
    private const int Height = 21;
    private const float CellSize = 1f;
    private const float PlayerSpeed = 4.6f;
    private const float PlayerAcceleration = 24f;
    private const float PlayerDeceleration = 18f;
    private const float RatingCritical = 15f;
    private const float GameplayCameraSize = 4.8f;
    private const float PlayerAttackRange = 1.75f;
    private const float PlayerAttackConeMinDot = 0.08f;
    private const float PlayerAttackCooldown = 0.42f;
    private const float EnemyAttackRange = 0.96f;
    private const float EnemyAttackConeMinDot = 0.12f;
    private const float EnemyAttackWindup = 0.45f;
    private const float EnemyAttackStrike = 0.12f;
    private const float EnemyAttackRecovery = 0.76f;
    private const int EnemyBaseLevel = 3;
    private const int EnemyMinLevel = 1;
    private const int EnemyMaxLevel = 9;
    private const float EnemyStunDuration = 0.24f;
    private const float EnemyHitFlashDuration = 0.18f;
    private const float EnemyKnockbackSpeed = 4.8f;
    private const float EnemyKnockbackDamping = 14f;
    private const float EnemyAggroResetDistance = 9.0f;
    private const float EnemyAggroResetDelay = 2.8f;
    private const float EnemyDirectChaseGrace = 0.55f;
    private const float EnemyForwardSightRange = 11.5f;
    private const float EnemySideSightRange = 4.5f;
    private const float EnemyBackSightRange = 2.5f;
    private const float EnemyCloseDetectionRange = 4.0f;
    private const float RemoteCooldown = 18f;
    private const float RemoteJamDuration = 3f;
    private const float RemoteRatingRestore = 18f;
    private const float RemoteEnemySpeedMultiplier = 0.18f;
    private const string CameraLightName = "Camera Light";
    private const string EnemyLightName = "Enemy Light";
    private const string EnemyBeamName = "Enemy Beam";
    private const int FixedAtlasColumns = 8;
    private const int FixedAtlasRows = 8;
    private const int HudAtlasColumns = 4;
    private const int HudAtlasRows = 4;
    private const float HudScale = 1.5f;
    private const int HeartAtlasRow = 5;
    private const int HeartAtlasColumn = 5;

    public Texture2D CharacterAtlas;
    public Texture2D EnvironmentAtlas;
    public Texture2D WallAtlas;
    public Texture2D HudAtlas;
    public TextAsset LevelAsset;
    public TextAsset[] LevelAssets = Array.Empty<TextAsset>();
    public string StartingLevelId = "prototype_01";

    private readonly Tile[,] tiles = new Tile[Width, Height];
    private readonly int[,] tileVariants = new int[Width, Height];
    private readonly GameObject[,] floorViews = new GameObject[Width, Height];
    private readonly GameObject[,] floorDecalViews = new GameObject[Width, Height];
    private readonly GameObject[,] tileViews = new GameObject[Width, Height];
    private readonly GridPathfinder pathfinder = new GridPathfinder(Width, Height);
    private readonly List<Stone> stones = new List<Stone>();
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<CombatEffect> combatEffects = new List<CombatEffect>();
    private readonly List<GameObject> levelVisualObjects = new List<GameObject>();
    private readonly Dictionary<Vector2Int, string> gateGroupsByCell = new Dictionary<Vector2Int, string>();
    private readonly Dictionary<string, List<Vector2Int>> gateCellsByGroup = new Dictionary<string, List<Vector2Int>>();
    private readonly Dictionary<Vector2Int, LevelObject> gateObjectsByCell = new Dictionary<Vector2Int, LevelObject>();
    private readonly Dictionary<string, List<Vector2Int>> plateCellsByGroup = new Dictionary<string, List<Vector2Int>>();
    private readonly Dictionary<Vector2Int, LevelObject> storyObjectsByCell = new Dictionary<Vector2Int, LevelObject>();
    private readonly Dictionary<Vector2Int, LevelExit> exitsByCell = new Dictionary<Vector2Int, LevelExit>();
    private readonly HashSet<string> readStoryIds = new HashSet<string>();
    private readonly HashSet<string> killedEnemyIds = new HashSet<string>();
    private readonly HashSet<string> activeEnemyGroups = new HashSet<string>();
    private readonly HashSet<string> clearedEnemyGroups = new HashSet<string>();
    private readonly HashSet<string> executedEventIds = new HashSet<string>();
    private readonly List<LevelEvent> levelEvents = new List<LevelEvent>();
    private readonly List<LevelDecoration> levelDecorations = new List<LevelDecoration>();
    private readonly List<LevelLight> levelLights = new List<LevelLight>();
    private readonly Dictionary<string, HashSet<Vector2Int>> regionsById = new Dictionary<string, HashSet<Vector2Int>>();
    private readonly Dictionary<Vector2Int, Vector2> cameraDirectionsByCell = new Dictionary<Vector2Int, Vector2>();
    private readonly Dictionary<Texture2D, Rect> visibleTextureBounds = new Dictionary<Texture2D, Rect>();
    private readonly Dictionary<string, Texture2D> runtimeAtlasCells = new Dictionary<string, Texture2D>();

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
    [SerializeField] private Sprite healSprite;
    [SerializeField] private Sprite playerSprite;
    [SerializeField] private Sprite[] playerIdleSprites = new Sprite[4];
    [SerializeField] private Sprite[] playerWalkOneSprites = new Sprite[4];
    [SerializeField] private Sprite[] playerWalkTwoSprites = new Sprite[4];
    [SerializeField] private Sprite[] playerAttackSprites = new Sprite[4];
    [SerializeField] private Sprite stoneSprite;
    [SerializeField] private Sprite enemySprite;
    [SerializeField] private Sprite enemyInvestigateSprite;
    [SerializeField] private Sprite enemyHuntSprite;
    [SerializeField] private Sprite enemyBeamSprite;
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
    private Transform combatVfxRoot;
    private Sprite effectSprite;
    private Rigidbody2D playerBody;
    private Light2D playerLight;
    private Volume postProcessVolume;
    private VolumeProfile postProcessProfile;
    private Vignette postProcessVignette;
    private ColorAdjustments postProcessColor;
    private ChromaticAberration postProcessChromaticAberration;
    private FilmGrain postProcessFilmGrain;
    private LensDistortion postProcessLensDistortion;
    private Bloom postProcessBloom;
    private Vector2 moveInput;
    private Vector2 currentVelocity;
    private Vector2 lastAim = Vector2.right;
    private string currentLevelId;
    private Vector2Int playerStart = new Vector2Int(3, 10);
    private Vector2Int lastEventPlayerCell = new Vector2Int(-1, -1);
    private Vector2Int lastNoiseCell;
    private int lastNoisePower;
    private int playerHp = 6;
    private int levelEnemiesKilled;
    private int camerasBroken;
    private float viewerRating = 100f;
    private float idleTimer;
    private float criticalDamageTimer;
    private float attackCooldown;
    private float remoteCooldown;
    private float remoteJamTimer;
    private bool hasRemote;
    private bool gameEnded;
    private bool runCompleted;
    private string message = "Канал требует внимания. Соберите сигнал и выберите, как смотреть дальше.";

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Physics2D.gravity = Vector2.zero;
        SetupCamera();
        EnsurePostProcessing();
        currentLevelId = LevelAssetResolver.NormalizeLevelId(string.IsNullOrEmpty(StartingLevelId) ? LevelAsset?.name : StartingLevelId);
        BuildLevel();

        if (!HasBakedAssets() || !BindSceneViews())
        {
            Debug.LogError("Prototype scene is not baked. Run Rogue > Bootstrap All Scenes before entering Play Mode.");
            enabled = false;
            return;
        }

        EnsureGameplayLighting();
        UpdatePostProcessing();
        RedrawAll();
        EvaluateEvents("levelStart", null, null);
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
        UpdateCombatEffects(Time.deltaTime);
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

















    private Texture2D GetRuntimeAtlasCell(Texture2D atlas, int rows, int columns, int row, int column, string name, bool removeCellBackground)
    {
        if (atlas == null || row < 0 || row >= rows || column < 0 || column >= columns)
            return null;

        string key = $"{atlas.name}:{atlas.width}x{atlas.height}:{rows}:{columns}:{row}:{column}:{removeCellBackground}";
        if (runtimeAtlasCells.TryGetValue(key, out Texture2D cached))
            return cached;

        int cellWidth = atlas.width / columns;
        int cellHeight = atlas.height / rows;
        int sourceX = column * cellWidth;
        int sourceY = atlas.height - (row + 1) * cellHeight;

        try
        {
            Color[] pixels = atlas.GetPixels(sourceX, sourceY, cellWidth, cellHeight);
            Color background = removeCellBackground ? SampleCellBackground(pixels, cellWidth, cellHeight) : Color.clear;
            var texture = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                name = name,
                hideFlags = HideFlags.DontSave,
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
            runtimeAtlasCells[key] = texture;
            return texture;
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private Rect VisibleTextureBounds(Texture2D texture)
    {
        if (texture == null)
            return new Rect(0f, 0f, 1f, 1f);

        if (visibleTextureBounds.TryGetValue(texture, out Rect cached))
            return cached;

        Rect bounds = new Rect(0f, 0f, texture.width, texture.height);
        try
        {
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < texture.height; y++)
            {
                int row = y * texture.width;
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[row + x].a <= 8)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX >= minX && maxY >= minY)
                bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        catch (UnityException)
        {
            bounds = new Rect(0f, 0f, texture.width, texture.height);
        }

        visibleTextureBounds[texture] = bounds;
        return bounds;
    }

    private static Rect FitRectToAspect(Rect target, float aspect)
    {
        if (aspect <= 0f || target.width <= 0f || target.height <= 0f)
            return PixelRect(target);

        float targetAspect = target.width / target.height;
        if (targetAspect > aspect)
        {
            float width = target.height * aspect;
            return PixelRect(new Rect(target.x + (target.width - width) * 0.5f, target.y, width, target.height));
        }

        float height = target.width / aspect;
        return PixelRect(new Rect(target.x, target.y + (target.height - height) * 0.5f, target.width, height));
    }

    private static Rect PixelRect(Rect rect)
    {
        return new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), Mathf.Round(rect.width), Mathf.Round(rect.height));
    }




    private void Restart()
    {
        ResetCurrentLevel();
    }

    private void ResetCurrentLevel()
    {
        ClearLevelEntityViews();

        playerHp = 6;
        viewerRating = 100f;
        idleTimer = 0f;
        criticalDamageTimer = 0f;
        attackCooldown = 0f;
        remoteCooldown = 0f;
        remoteJamTimer = 0f;
        hasRemote = false;
        lastNoisePower = 0;
        currentVelocity = Vector2.zero;
        gameEnded = false;
        runCompleted = false;
        camerasBroken = 0;

        BuildLevel();
        if (!enabled)
            return;

        CreateEntityViews();

        if (playerView != null)
            playerView.transform.position = ToWorld(playerStart);
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        EnsureGameplayLighting();
        EnsurePostProcessing();
        UpdatePostProcessing();
        RedrawAll();
        RebuildTileColliders();
        EvaluateEvents("levelStart", null, null);
        SpawnHitBurst(ToWorld(playerStart), false);
        message = "Канал перемотан к началу текущего уровня.";
    }

    private void RestartRun()
    {
        NarrativeRunState.Reset();
        ClearCombatRuntimeObjects();

        enemies.Clear();
        stones.Clear();
        playerHp = 6;
        viewerRating = 100f;
        idleTimer = 0f;
        criticalDamageTimer = 0f;
        attackCooldown = 0f;
        remoteCooldown = 0f;
        remoteJamTimer = 0f;
        hasRemote = false;
        gameEnded = false;
        runCompleted = false;
        lastNoisePower = 0;
        currentVelocity = Vector2.zero;
        currentLevelId = LevelAssetResolver.NormalizeLevelId(string.IsNullOrEmpty(StartingLevelId) ? LevelAsset?.name : StartingLevelId);
        killedEnemyIds.Clear();
        levelEnemiesKilled = 0;
        camerasBroken = 0;
        message = "Канал требует внимания. Соберите сигнал и выберите, как смотреть дальше.";

        BuildLevel();
        if (!BindSceneViews())
        {
            Debug.LogError("Prototype scene lost baked references. Run Rogue > Bootstrap All Scenes.");
            enabled = false;
            return;
        }

        EnsureGameplayLighting();
        EnsurePostProcessing();
        UpdatePostProcessing();
        RedrawAll();
        RebuildTileColliders();
        EvaluateEvents("levelStart", null, null);
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
        float previousRating = viewerRating;
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

        UpdatePostProcessing();
        if (!Mathf.Approximately(previousRating, viewerRating))
            RefreshStatGates();
    }

    private void RestoreRating(float amount)
    {
        float previousRating = viewerRating;
        viewerRating = Mathf.Min(100f, viewerRating + amount);
        UpdatePostProcessing();
        if (!Mathf.Approximately(previousRating, viewerRating))
            RefreshStatGates();
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
                tileVariants[next.x, next.y] = -1;
                NarrativeRunState.RecordSignalInsight();
                RestoreRating(10f);
                message = "Пульт: Q глушит эфир на 3 секунды. Кд 18 секунд.";
                RedrawTile(next);
                return;
            }

            if (tile == Tile.Story)
            {
                LevelObject story = storyObjectsByCell.TryGetValue(next, out LevelObject storyData) ? storyData : null;
                string storyId = string.IsNullOrEmpty(story?.id) ? $"story_{next.x}_{next.y}" : story.id;
                readStoryIds.Add(storyId);
                tiles[next.x, next.y] = Tile.Floor;
                tileVariants[next.x, next.y] = -1;
                NarrativeRunState.RecordPuzzleReflection();
                RestoreRating(18f);
                message = string.IsNullOrEmpty(story?.text) ? "В монтажной заметке написано: смотреть не значит соглашаться." : story.text;
                RedrawTile(next);
                UpdatePuzzle();
                return;
            }

            if (tile == Tile.Heal)
            {
                TryConsumeHeal(next);
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

        attackCooldown = PlayerAttackCooldown;
        MakeNoise(PlayerCell(), 8);
        NarrativeRunState.RecordAttack();
        RestoreRating(NarrativeRunState.IsAggressive() ? 6f : 3f);
        SpawnAttackSwing(playerView.transform.position, lastAim);

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
            if (!HasSightLine(PlayerCell(), WorldToCell(enemy.Position)))
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

        DamageEnemy(target, player);
    }

    private void DamageEnemy(Enemy target, Vector2 player)
    {
        Vector2 away = target.Position - player;
        if (away.sqrMagnitude < 0.001f)
            away = lastAim;
        away.Normalize();

        target.Hp--;
        target.Mode = EnemyMode.Hunt;
        target.LastSeen = PlayerCell();
        target.LostSightTimer = 0f;
        target.StunTimer = EnemyStunDuration;
        target.HitFlashTimer = EnemyHitFlashDuration;
        target.KnockbackVelocity = away * EnemyKnockbackSpeed;
        CancelEnemyAttack(target);
        SpawnHitBurst(target.Position, target.Hp <= 0);
        if (target.Hp <= 0)
            SpawnEnemyDeathFlash(target.Position);
        message = target.Hp <= 0 ? "Диктор рассыпался в белый шум." : "Диктор сбился с текста.";

        if (target.Hp > 0)
            return;

        NarrativeRunState.RecordKill();
        levelEnemiesKilled += 1;
        if (!string.IsNullOrEmpty(target.Id))
            killedEnemyIds.Add(target.Id);
        RestoreRating(12f);
        DestroyEnemyTelegraph(target);
        if (target.View != null)
            target.View.SetActive(false);
        enemies.Remove(target);
        EvaluateEvents("enemyKilled", target.Id, target.Group);
        CheckEnemyGroupCleared(target.Group);
        RedrawGateGroups();
        RedrawExits();
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
                if (!HasSightLine(playerCell, cell))
                    continue;

                found = true;
                best = distance;
                bestCell = cell;
            }
        }

        if (!found)
            return false;

        tiles[bestCell.x, bestCell.y] = Tile.Floor;
        tileVariants[bestCell.x, bestCell.y] = -1;
        NarrativeRunState.RecordSignalInsight();
        camerasBroken += 1;
        RestoreRating(8f);
        RedrawTile(bestCell);
        RedrawGateGroups();
        RedrawExits();
        SpawnHitBurst(ToWorld(bestCell), true);
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

    private bool TryConsumeHeal(Vector2Int cell)
    {
        if (!Inside(cell) || tiles[cell.x, cell.y] != Tile.Heal)
            return false;

        if (playerHp >= 6)
        {
            message = "Кассета перемотки цела. Сейчас раны не требуют монтажа.";
            return false;
        }

        playerHp = Mathf.Min(6, playerHp + 2);
        tiles[cell.x, cell.y] = Tile.Floor;
        tileVariants[cell.x, cell.y] = -1;
        message = "Кассета перематывает боль назад. HP +2.";
        RestoreRating(4f);
        RedrawTile(cell);
        return true;
    }

    private void UpdateWorldInteractions()
    {
        Vector2Int playerCell = PlayerCell();
        Tile tile = tiles[playerCell.x, playerCell.y];

        if (tile == Tile.Trap)
        {
            SpawnCameraFlash(ToWorld(playerCell), CameraDirectionForCell(playerCell));
            tiles[playerCell.x, playerCell.y] = Tile.Floor;
            tileVariants[playerCell.x, playerCell.y] = -1;
            NarrativeRunState.RecordTrapMistake();
            MakeNoise(playerCell, 9);
            DamagePlayer(1, "Камера ослепляет вспышкой. Рейтинг вздрагивает.");
            viewerRating = Mathf.Max(0f, viewerRating - 8f);
            UpdatePostProcessing();
            RedrawTile(playerCell);
        }

        if (tile == Tile.Heal && playerHp < 6)
            TryConsumeHeal(playerCell);

        if (tile == Tile.Exit)
            TryUseExit(playerCell);

        if (playerCell != lastEventPlayerCell)
        {
            lastEventPlayerCell = playerCell;
            EvaluateEvents("enterRegion", null, null);
        }
    }

    private void BlockRectangle(int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
                BlockCells(new Vector2Int(x, y));
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

    private void TryUseExit(Vector2Int cell)
    {
        if (!CanUseExit(cell))
            return;

        LevelExit exit = exitsByCell.TryGetValue(cell, out LevelExit data) ? data : null;
        string targetLevel = LevelAssetResolver.NormalizeLevelId(exit?.targetLevel);
        if (!string.IsNullOrEmpty(targetLevel))
        {
            LoadNextLevel(targetLevel);
            return;
        }

        gameEnded = true;
        runCompleted = true;
        currentVelocity = Vector2.zero;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
        message = NarrativeRunState.ChannelClosingLine() + " Вы прошли игру.";
    }

    private void LoadNextLevel(string targetLevel)
    {
        if (ResolveLevelAsset(targetLevel) == null)
        {
            message = $"Следующий уровень не найден: {targetLevel}.";
            return;
        }

        ClearLevelEntityViews();
        currentLevelId = LevelAssetResolver.NormalizeLevelId(targetLevel);
        ResetLevelLocalState();
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
        EvaluateEvents("levelStart", null, null);
        message = $"Канал переключён: {currentLevelId}.";
    }

    private void ResetLevelLocalState()
    {
        attackCooldown = 0f;
        remoteJamTimer = 0f;
        runCompleted = false;
        lastNoisePower = 0;
        killedEnemyIds.Clear();
        levelEnemiesKilled = 0;
    }

    private void ClearLevelEntityViews()
    {
        foreach (Stone stone in stones)
            DestroyRuntimeObject(stone.View);

        foreach (Enemy enemy in enemies)
        {
            DestroyRuntimeObject(enemy.TelegraphView);
            DestroyRuntimeObject(enemy.View);
        }

        foreach (CombatEffect effect in combatEffects)
            DestroyRuntimeObject(effect.View);
        combatEffects.Clear();

        foreach (GameObject visual in levelVisualObjects)
            DestroyRuntimeObject(visual);
        levelVisualObjects.Clear();
    }

    private static void DestroyRuntimeObject(GameObject obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private bool CanUseExit(Vector2Int cell)
    {
        if (!exitsByCell.TryGetValue(cell, out LevelExit exit))
            return false;

        if (!string.IsNullOrEmpty(exit.requiresGate))
            return GateOpenForId(exit.requiresGate);

        return true;
    }

    private bool GateOpenForId(string gateId)
    {
        if (string.IsNullOrEmpty(gateId))
            return false;

        foreach (KeyValuePair<Vector2Int, LevelObject> item in gateObjectsByCell)
        {
            if (item.Value != null && item.Value.id == gateId && GateOpenForCell(item.Key))
                return true;
        }

        return false;
    }

    private void UpdatePuzzle()
    {
        RedrawGateGroups();
        RedrawExits();
    }

    private bool GateGroupRequirementsMet(string group)
    {
        if (!gateCellsByGroup.TryGetValue(group, out List<Vector2Int> cells))
            return false;

        foreach (Vector2Int cell in cells)
        {
            if (gateObjectsByCell.TryGetValue(cell, out LevelObject gate) && GateRequirementsMet(gate))
                return true;
        }

        return false;
    }

    private bool GateRequirementsMet(LevelObject gate)
    {
        if (!HasExplicitGateRequirements(gate))
            return false;

        if (gate.requiresPlates != null)
        {
            foreach (string group in gate.requiresPlates)
            {
                if (!ArePlateGroupCovered(group))
                    return false;
            }
        }

        if (gate.requiresStories != null)
        {
            foreach (string storyId in gate.requiresStories)
            {
                if (string.IsNullOrEmpty(storyId) || !readStoryIds.Contains(storyId))
                    return false;
            }
        }

        if (gate.requiresEnemies != null)
        {
            foreach (string enemyId in gate.requiresEnemies)
            {
                if (string.IsNullOrEmpty(enemyId) || !killedEnemyIds.Contains(enemyId))
                    return false;
            }
        }

        if (gate.requiresStats != null)
        {
            foreach (List<object> condition in gate.requiresStats)
            {
                if (!GateConditionEvaluator.StatConditionMet(condition, CurrentGateStats()))
                    return false;
            }
        }

        return true;
    }

    private static bool HasExplicitGateRequirements(LevelObject gate)
    {
        return gate != null &&
               (gate.requiresPlates != null && gate.requiresPlates.Count > 0 ||
                gate.requiresStories != null && gate.requiresStories.Count > 0 ||
                gate.requiresEnemies != null && gate.requiresEnemies.Count > 0 ||
                gate.requiresStats != null && gate.requiresStats.Count > 0);
    }

    private GateStatSnapshot CurrentGateStats()
    {
        return new GateStatSnapshot(NarrativeRunState.EnemiesKilled, levelEnemiesKilled, camerasBroken, viewerRating);
    }

    private bool ArePlateGroupCovered(string group)
    {
        if (string.IsNullOrEmpty(group) || !plateCellsByGroup.TryGetValue(group, out List<Vector2Int> cells))
            return false;

        return ArePlatesCovered(cells);
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

    private void RedrawExits()
    {
        foreach (Vector2Int cell in exitsByCell.Keys)
            RedrawTile(cell);
    }

    private void RedrawGateGroup(string group)
    {
        if (!gateCellsByGroup.TryGetValue(group, out List<Vector2Int> cells))
            return;

        foreach (Vector2Int cell in cells)
            RedrawTile(cell);
    }

    private void RedrawGateGroups()
    {
        foreach (string group in gateCellsByGroup.Keys)
            RedrawGateGroup(group);
    }

    private void RefreshStatGates()
    {
        RedrawGateGroups();
        RedrawExits();
        EvaluateEvents("statsChanged", null, null);
    }

    private void CheckEnemyGroupCleared(string group)
    {
        if (string.IsNullOrEmpty(group) || !activeEnemyGroups.Contains(group) || clearedEnemyGroups.Contains(group))
            return;

        foreach (Enemy enemy in enemies)
        {
            if (enemy.Group == group)
                return;
        }

        clearedEnemyGroups.Add(group);
        EvaluateEvents("enemyGroupCleared", null, group);
    }

    private void EvaluateEvents(string trigger, string enemyId, string enemyGroup)
    {
        if (levelEvents.Count == 0)
            return;

        foreach (LevelEvent levelEvent in levelEvents.ToArray())
        {
            if (!EventMatches(levelEvent, trigger, enemyId, enemyGroup))
                continue;

            ExecuteEvent(levelEvent);
        }
    }

    private bool EventMatches(LevelEvent levelEvent, string trigger, string enemyId, string enemyGroup)
    {
        if (levelEvent == null || levelEvent.enabled == false)
            return false;
        if (levelEvent.once && !string.IsNullOrEmpty(levelEvent.id) && executedEventIds.Contains(levelEvent.id))
            return false;
        if (!string.Equals(levelEvent.trigger, trigger, StringComparison.Ordinal))
            return false;

        if (trigger == "enterRegion")
        {
            if (string.IsNullOrEmpty(levelEvent.region) || !regionsById.TryGetValue(levelEvent.region, out HashSet<Vector2Int> cells) || !cells.Contains(PlayerCell()))
                return false;
        }
        else if (trigger == "enemyKilled")
        {
            if (!string.Equals(levelEvent.enemyId, enemyId, StringComparison.Ordinal))
                return false;
        }
        else if (trigger == "enemyGroupCleared")
        {
            if (!string.Equals(levelEvent.enemyGroup, enemyGroup, StringComparison.Ordinal))
                return false;
        }

        if (levelEvent.conditions != null)
        {
            foreach (List<object> condition in levelEvent.conditions)
            {
                if (!GateConditionEvaluator.StatConditionMet(condition, CurrentGateStats()))
                    return false;
            }
        }

        return true;
    }

    private void ExecuteEvent(LevelEvent levelEvent)
    {
        if (levelEvent.once && !string.IsNullOrEmpty(levelEvent.id))
            executedEventIds.Add(levelEvent.id);

        if (levelEvent.actions == null)
            return;

        bool rebuildColliders = false;
        foreach (LevelEventAction action in levelEvent.actions)
        {
            if (ExecuteEventAction(action))
                rebuildColliders = true;
        }

        if (rebuildColliders)
            RebuildTileColliders();
        RedrawGateGroups();
        RedrawExits();
    }

    private bool ExecuteEventAction(LevelEventAction action)
    {
        if (action == null)
            return false;

        switch (action.type)
        {
            case "spawnEnemy":
                SpawnEventEnemy(action.enemy ?? EnemyFromAction(action));
                return false;
            case "spawnEnemies":
                if (action.enemies != null)
                {
                    foreach (LevelEnemy enemy in action.enemies)
                        SpawnEventEnemy(enemy);
                }
                return false;
            case "fallStone":
                SpawnEventStone(new Vector2Int(action.x, action.y), true);
                return true;
            case "setTile":
                SetEventTile(new Vector2Int(action.x, action.y), action.tile, action.variant);
                return true;
            case "spawnObject":
                SpawnEventObject(action);
                return true;
            case "removeObject":
                RemoveEventObject(action);
                return true;
            case "playEffect":
                SpawnHitBurst(ToWorld(new Vector2Int(action.x, action.y)), true);
                return false;
            default:
                return false;
        }
    }

    private LevelEnemy EnemyFromAction(LevelEventAction action)
    {
        return new LevelEnemy
        {
            id = action.id,
            group = action.group,
            x = action.x,
            y = action.y,
            level = action.enemy?.level ?? 3,
            hp = action.enemy?.hp ?? 2,
            patrol = action.enemy?.patrol ?? new List<List<int>> { new List<int> { action.x, action.y } },
        };
    }

    private void SpawnEventEnemy(LevelEnemy data)
    {
        if (data == null)
            return;

        Vector2Int start = new Vector2Int(data.x, data.y);
        if (!Inside(start) || IsSolidCell(start) || EnemyAt(start) != null)
            return;

        int before = enemies.Count;
        ApplyLevelEnemy(data);
        if (enemies.Count <= before)
            return;

        Enemy spawned = enemies[enemies.Count - 1];
        if (Application.isPlaying || playerView != null)
            CreateEnemyView(spawned, enemies.Count - 1);
    }

    private void SpawnEventStone(Vector2Int cell, bool falling)
    {
        if (!Inside(cell) || StoneAt(cell) != null)
            return;

        AddStone(cell);
        Stone stone = stones[stones.Count - 1];
        if (falling)
            SpawnHitBurst(ToWorld(cell), false);
        CreateStoneView(stone);
    }

    private void SetEventTile(Vector2Int cell, string tileName, int variant)
    {
        if (!Inside(cell))
            return;

        tiles[cell.x, cell.y] = TileFromName(tileName);
        tileVariants[cell.x, cell.y] = variant;
        RedrawTile(cell);
    }

    private void SpawnEventObject(LevelEventAction action)
    {
        LevelObject obj = action.obj ?? new LevelObject
        {
            type = string.IsNullOrEmpty(action.objectType) ? action.type : action.objectType,
            id = action.id,
            group = action.group,
            x = action.x,
            y = action.y,
            variant = action.variant,
        };
        ApplyLevelObject(obj);
        RedrawTile(new Vector2Int(obj.x, obj.y));
    }

    private void RemoveEventObject(LevelEventAction action)
    {
        Vector2Int cell = new Vector2Int(action.x, action.y);
        if (!string.IsNullOrEmpty(action.id))
        {
            foreach (KeyValuePair<Vector2Int, LevelObject> item in gateObjectsByCell)
            {
                if (item.Value != null && item.Value.id == action.id)
                {
                    cell = item.Key;
                    break;
                }
            }
        }

        if (!Inside(cell))
            return;

        if (StoneAt(cell) is Stone stone)
        {
            DestroyRuntimeObject(stone.View);
            stones.Remove(stone);
        }
        gateGroupsByCell.Remove(cell);
        gateObjectsByCell.Remove(cell);
        storyObjectsByCell.Remove(cell);
        cameraDirectionsByCell.Remove(cell);
        tiles[cell.x, cell.y] = Tile.Floor;
        tileVariants[cell.x, cell.y] = -1;
        RedrawTile(cell);
    }

    private void UpdateEnemies(float dt)
    {
        if (playerView == null)
            return;

        foreach (Enemy enemy in enemies.ToArray())
        {
            if (enemy.View == null)
                continue;

            UpdateEnemyHitTimers(enemy, dt);
            if (UpdateEnemyAttack(enemy, dt))
            {
                UpdateEnemyVisual(enemy);
                continue;
            }

            if (UpdateEnemyKnockback(enemy, dt))
            {
                UpdateEnemyVisual(enemy);
                continue;
            }

            UpdateEnemyState(enemy, dt);

            Vector2 target = ChooseEnemyTarget(enemy);
            float speed = enemy.Mode == EnemyMode.Hunt ? 2.55f : enemy.Mode == EnemyMode.Investigate ? 2.1f : 1.55f;
            speed *= EnemySpeedScale(enemy);
            if (RemoteJamActive())
                speed *= RemoteEnemySpeedMultiplier;
            Vector2 steeringTarget = EnemySteeringTarget(enemy, target);
            Vector2 previousPosition = enemy.Position;
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

            Vector2 movement = enemy.Position - previousPosition;
            enemy.LookDirection = DirectionOrFallback(movement.sqrMagnitude > 0.001f ? movement : steeringTarget - enemy.Position, enemy.LookDirection);
            enemy.View.transform.position = enemy.Position;
            UpdateEnemyVisual(enemy);

            if (!RemoteJamActive() && EnemyCanStartAttack(enemy))
                StartEnemyAttack(enemy);
        }

        if (lastNoisePower > 0)
            lastNoisePower = Mathf.Max(0, lastNoisePower - Mathf.CeilToInt(dt * 2f));
    }

    private void UpdateEnemyHitTimers(Enemy enemy, float dt)
    {
        enemy.HitFlashTimer = Mathf.Max(0f, enemy.HitFlashTimer - dt);
        enemy.StunTimer = Mathf.Max(0f, enemy.StunTimer - dt);
    }

    private bool UpdateEnemyKnockback(Enemy enemy, float dt)
    {
        bool stunned = enemy.StunTimer > 0f;
        bool moving = enemy.KnockbackVelocity.sqrMagnitude > 0.01f;
        if (!stunned && !moving)
            return false;

        if (moving)
        {
            Vector2 next = enemy.Position + enemy.KnockbackVelocity * dt;
            if (CanEnemyOccupy(next, enemy))
            {
                enemy.Position = next;
                enemy.View.transform.position = enemy.Position;
            }
            else
            {
                enemy.KnockbackVelocity = Vector2.zero;
            }

            enemy.KnockbackVelocity = Vector2.MoveTowards(enemy.KnockbackVelocity, Vector2.zero, EnemyKnockbackDamping * dt);
        }

        return stunned || moving;
    }

    private bool UpdateEnemyAttack(Enemy enemy, float dt)
    {
        if (enemy.AttackWindupTimer > 0f)
        {
            enemy.LookDirection = DirectionOrFallback(enemy.AttackDirection, enemy.LookDirection);
            enemy.AttackWindupTimer = Mathf.Max(0f, enemy.AttackWindupTimer - dt);
            UpdateEnemyTelegraph(enemy, false);
            if (enemy.AttackWindupTimer <= 0f)
            {
                enemy.AttackStrikeTimer = EnemyAttackStrike;
                enemy.AttackApplied = false;
                UpdateEnemyTelegraph(enemy, true);
            }

            return true;
        }

        if (enemy.AttackStrikeTimer > 0f)
        {
            enemy.LookDirection = DirectionOrFallback(enemy.AttackDirection, enemy.LookDirection);
            enemy.AttackStrikeTimer = Mathf.Max(0f, enemy.AttackStrikeTimer - dt);
            UpdateEnemyTelegraph(enemy, true);
            if (!enemy.AttackApplied)
            {
                enemy.AttackApplied = true;
                if (!RemoteJamActive() && PlayerInEnemyAttackZone(enemy))
                    DamagePlayer(EnemyAttackDamageFor(enemy), enemy.Mode == EnemyMode.Hunt ? "Диктор ловит вас в кадре после замаха." : "Диктор бьёт микрофоном после паузы.");
            }

            if (enemy.AttackStrikeTimer <= 0f)
            {
                enemy.AttackRecoveryTimer = EnemyAttackRecoveryFor(enemy);
                HideEnemyTelegraph(enemy);
            }

            return true;
        }

        if (enemy.AttackRecoveryTimer > 0f)
        {
            enemy.AttackRecoveryTimer = Mathf.Max(0f, enemy.AttackRecoveryTimer - dt);
            HideEnemyTelegraph(enemy);
            return true;
        }

        HideEnemyTelegraph(enemy);
        return false;
    }

    private bool EnemyCanStartAttack(Enemy enemy)
    {
        if (enemy.Mode == EnemyMode.Patrol || playerView == null)
            return false;

        return Vector2.Distance(enemy.Position, playerView.transform.position) <= EnemyAttackRange;
    }

    private void StartEnemyAttack(Enemy enemy)
    {
        Vector2 direction = (Vector2)playerView.transform.position - enemy.Position;
        enemy.AttackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        enemy.LookDirection = enemy.AttackDirection;
        enemy.AttackWindupTimer = EnemyAttackWindupFor(enemy);
        enemy.AttackStrikeTimer = 0f;
        enemy.AttackRecoveryTimer = 0f;
        enemy.AttackApplied = false;
        enemy.KnockbackVelocity = Vector2.zero;
        UpdateEnemyTelegraph(enemy, false);
    }

    private void CancelEnemyAttack(Enemy enemy)
    {
        enemy.AttackWindupTimer = 0f;
        enemy.AttackStrikeTimer = 0f;
        enemy.AttackRecoveryTimer = 0f;
        enemy.AttackApplied = false;
        HideEnemyTelegraph(enemy);
    }

    private bool PlayerInEnemyAttackZone(Enemy enemy)
    {
        if (playerView == null)
            return false;

        Vector2 toPlayer = (Vector2)playerView.transform.position - enemy.Position;
        if (toPlayer.magnitude > EnemyAttackRange)
            return false;
        if (toPlayer.sqrMagnitude < 0.001f)
            return true;

        Vector2 direction = enemy.AttackDirection.sqrMagnitude > 0.001f ? enemy.AttackDirection.normalized : toPlayer.normalized;
        return Vector2.Dot(direction, toPlayer.normalized) >= EnemyAttackConeMinDot;
    }

    private static int EnemyLevel(Enemy enemy)
    {
        return Mathf.Clamp(enemy?.Level ?? EnemyBaseLevel, EnemyMinLevel, EnemyMaxLevel);
    }

    private static float EnemyLevelOffset(Enemy enemy)
    {
        return EnemyLevel(enemy) - EnemyBaseLevel;
    }

    private static float EnemySpeedScale(Enemy enemy)
    {
        return Mathf.Clamp(1f + EnemyLevelOffset(enemy) * 0.10f, 0.70f, 1.60f);
    }

    private static float EnemyAttackWindupFor(Enemy enemy)
    {
        return EnemyAttackWindup / Mathf.Clamp(1f + EnemyLevelOffset(enemy) * 0.08f, 0.70f, 1.60f);
    }

    private static float EnemyAttackRecoveryFor(Enemy enemy)
    {
        return EnemyAttackRecovery / Mathf.Clamp(1f + EnemyLevelOffset(enemy) * 0.15f, 0.60f, 1.90f);
    }

    private static int EnemyAttackDamageFor(Enemy enemy)
    {
        return 1 + Mathf.FloorToInt((EnemyLevel(enemy) - 1) / 4f);
    }

    private void UpdateEnemyVisual(Enemy enemy)
    {
        SpriteRenderer enemyRenderer = enemy.View.GetComponent<SpriteRenderer>();
        if (enemyRenderer == null)
            return;

        enemyRenderer.sprite = SpriteForEnemyMode(enemy.Mode);
        Color color = EnemyColor(enemy.Mode);
        if (RemoteJamActive())
            color = Color.Lerp(color, new Color(0.48f, 0.92f, 1.00f), 0.62f);

        if (enemy.HitFlashTimer > 0f)
        {
            float pulse = Mathf.PingPong(enemy.HitFlashTimer * 34f, 1f);
            color = Color.Lerp(color, Color.white, 0.55f + pulse * 0.35f);
        }

        enemyRenderer.color = color;
        float punch = enemy.HitFlashTimer > 0f ? Mathf.Lerp(1.0f, 1.18f, enemy.HitFlashTimer / EnemyHitFlashDuration) : 1f;
        if (enemy.AttackWindupTimer > 0f)
            punch += Mathf.PingPong(Time.time * 10f, 0.05f);
        enemy.View.transform.localScale = new Vector3(punch, punch, 1f);
        ConfigureEnemyLight(enemy);
    }

    private void UpdateEnemyTelegraph(Enemy enemy, bool striking)
    {
        if (enemy.TelegraphView == null)
            enemy.TelegraphView = CreateTelegraphView(enemy);

        Vector2 direction = enemy.AttackDirection.sqrMagnitude > 0.001f ? enemy.AttackDirection.normalized : Vector2.down;
        enemy.TelegraphView.SetActive(true);
        enemy.TelegraphView.transform.position = enemy.Position + direction * (EnemyAttackRange * 0.48f);
        enemy.TelegraphView.transform.localScale = new Vector3(0.62f, EnemyAttackRange * 0.92f, 1f);
        enemy.TelegraphView.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        SpriteRenderer renderer = enemy.TelegraphView.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        float windup = EnemyAttackWindupFor(enemy);
        float windupRatio = enemy.AttackWindupTimer > 0f ? 1f - enemy.AttackWindupTimer / windup : 1f;
        renderer.color = striking
            ? new Color(1f, 0.18f, 0.12f, 0.58f)
            : Color.Lerp(new Color(1f, 0.80f, 0.20f, 0.22f), new Color(1f, 0.28f, 0.14f, 0.46f), windupRatio);
    }

    private GameObject CreateTelegraphView(Enemy enemy)
    {
        GameObject view = new GameObject("Enemy Attack Telegraph");
        view.transform.SetParent(EnsureCombatVfxRoot());
        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = EnsureEffectSprite();
        renderer.sortingOrder = 14;
        view.SetActive(false);
        return view;
    }

    private void HideEnemyTelegraph(Enemy enemy)
    {
        if (enemy.TelegraphView != null)
            enemy.TelegraphView.SetActive(false);
    }

    private void DestroyEnemyTelegraph(Enemy enemy)
    {
        if (enemy.TelegraphView == null)
            return;

        Destroy(enemy.TelegraphView);
        enemy.TelegraphView = null;
    }

    private void SpawnAttackSwing(Vector2 origin, Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;
        direction.Normalize();

        CombatEffect effect = CreateCombatEffect(
            "Attack Swing",
            origin + direction * 0.74f,
            new Vector3(0.72f, 0.22f, 1f),
            new Color(0.86f, 0.96f, 1f, 0.54f),
            0.14f,
            24);
        effect.EndScale = new Vector3(1.12f, 0.08f, 1f);
        effect.View.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private void SpawnHitBurst(Vector2 position, bool fatal)
    {
        int count = fatal ? 12 : 7;
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f) * i / count + UnityEngine.Random.Range(-0.22f, 0.22f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = UnityEngine.Random.Range(fatal ? 1.8f : 1.1f, fatal ? 3.5f : 2.3f);
            CombatEffect effect = CreateCombatEffect(
                fatal ? "White Noise Burst" : "Hit Spark",
                position + direction * UnityEngine.Random.Range(0.02f, 0.18f),
                Vector3.one * UnityEngine.Random.Range(fatal ? 0.08f : 0.06f, fatal ? 0.18f : 0.13f),
                fatal ? new Color(0.94f, 0.98f, 1f, 0.78f) : new Color(1f, 0.40f, 0.34f, 0.72f),
                UnityEngine.Random.Range(fatal ? 0.28f : 0.18f, fatal ? 0.48f : 0.34f),
                25);
            effect.Velocity = direction * speed;
            effect.EndScale = effect.StartScale * UnityEngine.Random.Range(0.25f, 0.55f);
            effect.RotationSpeed = UnityEngine.Random.Range(-260f, 260f);
        }
    }

    private void SpawnCameraFlash(Vector2 position, Vector2 direction)
    {
        direction = DirectionOrFallback(direction, Vector2.up);
        GameObject view = new GameObject("Camera Flash Light");
        view.transform.SetParent(EnsureCombatVfxRoot());
        view.transform.position = position + direction * 0.24f;

        var effect = new CombatEffect
        {
            View = view,
            StartScale = Vector3.one,
            EndScale = Vector3.one,
            Duration = 0.20f,
        };
        effect.Light = Urp2DLighting.AddConeLight(effect.View, new Color(0.78f, 0.93f, 1f), 3.2f, 6.0f, 0.55f, 95f, 48f, direction);
        effect.LightStartIntensity = effect.Light.intensity;
        Urp2DLighting.ConfigurePointLightShadows(effect.Light, 0.55f, 0.36f, 0.62f);
        combatEffects.Add(effect);
    }

    private void SpawnEnemyDeathFlash(Vector2 position)
    {
        GameObject view = new GameObject("Enemy Death Flash");
        view.transform.SetParent(EnsureCombatVfxRoot());
        view.transform.position = position;

        var effect = new CombatEffect
        {
            View = view,
            StartScale = Vector3.one,
            EndScale = Vector3.one,
            Duration = 0.20f,
        };
        effect.Light = Urp2DLighting.AddPointLight(view, new Color(1f, 0.62f, 0.54f), 2.0f, 3.2f, 0.35f);
        effect.LightStartIntensity = effect.Light.intensity;
        Urp2DLighting.ConfigurePointLightShadows(effect.Light, 0.35f, 0.42f, 0.62f);
        combatEffects.Add(effect);
    }

    private CombatEffect CreateCombatEffect(string name, Vector2 position, Vector3 scale, Color color, float duration, int sortingOrder)
    {
        GameObject view = new GameObject(name);
        view.transform.SetParent(EnsureCombatVfxRoot());
        view.transform.position = position;
        view.transform.localScale = scale;

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = EnsureEffectSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        var effect = new CombatEffect
        {
            View = view,
            Renderer = renderer,
            StartScale = scale,
            EndScale = scale,
            Color = color,
            Duration = duration,
        };
        combatEffects.Add(effect);
        return effect;
    }

    private void UpdateCombatEffects(float dt)
    {
        for (int i = combatEffects.Count - 1; i >= 0; i--)
        {
            CombatEffect effect = combatEffects[i];
            if (effect.View == null || (effect.Renderer == null && effect.Light == null))
            {
                combatEffects.RemoveAt(i);
                continue;
            }

            effect.Age += dt;
            float ratio = effect.Duration <= 0f ? 1f : Mathf.Clamp01(effect.Age / effect.Duration);
            effect.View.transform.position += (Vector3)(effect.Velocity * dt);
            effect.View.transform.localScale = Vector3.Lerp(effect.StartScale, effect.EndScale, ratio);
            if (Mathf.Abs(effect.RotationSpeed) > 0.01f)
                effect.View.transform.Rotate(0f, 0f, effect.RotationSpeed * dt);
            if (effect.Light != null)
                effect.Light.intensity = Mathf.Lerp(effect.LightStartIntensity, 0f, ratio);

            if (effect.Renderer != null)
            {
                Color color = effect.Color;
                color.a *= 1f - ratio;
                effect.Renderer.color = color;
            }

            if (ratio < 1f)
                continue;

            Destroy(effect.View);
            combatEffects.RemoveAt(i);
        }
    }

    private Transform EnsureCombatVfxRoot()
    {
        if (combatVfxRoot != null)
            return combatVfxRoot;

        GameObject root = FindSceneObjectIncludingInactive("Combat VFX");
        if (root == null)
            root = new GameObject("Combat VFX");
        combatVfxRoot = root.transform;
        return combatVfxRoot;
    }

    private Sprite EnsureEffectSprite()
    {
        if (effectSprite != null)
            return effectSprite;

        EnsureHudTextures();
        Texture2D texture = whiteTexture != null ? whiteTexture : Texture2D.whiteTexture;
        effectSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        return effectSprite;
    }

    private void ClearCombatRuntimeObjects()
    {
        foreach (Enemy enemy in enemies)
            DestroyEnemyTelegraph(enemy);

        for (int i = combatEffects.Count - 1; i >= 0; i--)
        {
            if (combatEffects[i].View != null)
                Destroy(combatEffects[i].View);
        }
        combatEffects.Clear();

        if (combatVfxRoot != null)
        {
            Destroy(combatVfxRoot.gameObject);
            combatVfxRoot = null;
        }

        GameObject staleRoot = FindSceneObjectIncludingInactive("Combat VFX");
        if (staleRoot != null)
            Destroy(staleRoot);
    }

    private bool RemoteJamActive()
    {
        return remoteJamTimer > 0f;
    }

    private void UpdateEnemyState(Enemy enemy, float dt)
    {
        Vector2Int enemyCell = WorldToCell(enemy.Position);
        if (CanSeePlayer(enemy))
        {
            enemy.Mode = EnemyMode.Hunt;
            enemy.LastSeen = PlayerCell();
            enemy.LostSightTimer = 0f;
            return;
        }

        if (enemy.Mode == EnemyMode.Hunt)
        {
            enemy.LostSightTimer += dt;
            float playerDistance = playerView != null ? Vector2.Distance(enemy.Position, playerView.transform.position) : float.PositiveInfinity;
            if (playerDistance > EnemyAggroResetDistance && enemy.LostSightTimer >= EnemyAggroResetDelay)
            {
                enemy.Mode = EnemyMode.Investigate;
                CancelEnemyAttack(enemy);
            }
        }

        if (lastNoisePower > 0 && Manhattan(enemyCell, lastNoiseCell) <= lastNoisePower)
        {
            enemy.Mode = EnemyMode.Investigate;
            enemy.LastSeen = lastNoiseCell;
            enemy.LostSightTimer = 0f;
            return;
        }

        if (enemy.Mode != EnemyMode.Patrol && Vector2.Distance(enemy.Position, ToWorld(enemy.LastSeen)) <= 0.1f)
        {
            enemy.Mode = EnemyMode.Patrol;
            enemy.LostSightTimer = 0f;
        }
    }

    private Vector2 ChooseEnemyTarget(Enemy enemy)
    {
        if (enemy.Mode == EnemyMode.Hunt)
        {
            if (playerView != null && enemy.LostSightTimer <= EnemyDirectChaseGrace)
                return playerView.transform.position;

            return ToWorld(enemy.LastSeen);
        }

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
        Vector2Int goal = WorldToCell(ChooseEnemyTarget(enemy));
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
        return pathfinder.TryFindNextPathCell(start, goal, EnemyPathPassable, out nextCell);
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
        Vector2Int playerCell = playerView != null ? WorldToCell(playerView.transform.position) : playerStart;
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

    private bool CanSeePlayer(Enemy enemy)
    {
        if (enemy == null || playerView == null)
            return false;

        Vector2 toPlayer = (Vector2)playerView.transform.position - enemy.Position;
        float distance = toPlayer.magnitude;
        if (distance <= 0.05f)
            return true;
        if (distance > EnemyForwardSightRange)
            return false;

        Vector2Int enemyCell = WorldToCell(enemy.Position);
        Vector2Int playerCell = PlayerCell();
        if (!HasSightLine(enemyCell, playerCell))
            return false;

        if (distance <= EnemyCloseDetectionRange)
            return true;

        Vector2 look = DirectionOrFallback(enemy.LookDirection, Vector2.down);
        float forward = Vector2.Dot(toPlayer, look);
        Vector2 sideAxis = new Vector2(-look.y, look.x);
        float side = Mathf.Abs(Vector2.Dot(toPlayer, sideAxis));
        float forwardRange = forward >= 0f ? EnemyForwardSightRange : EnemyBackSightRange;
        float forwardRatio = forward / forwardRange;
        float sideRatio = side / EnemySideSightRange;
        return forwardRatio * forwardRatio + sideRatio * sideRatio <= 1f;
    }

    private bool HasSightLine(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x;
        int y0 = from.y;
        int x1 = to.x;
        int y1 = to.y;
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;

        while (x0 != x1 || y0 != y1)
        {
            int e2 = 2 * error;
            if (e2 > -dy)
            {
                error -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                error += dx;
                y0 += sy;
            }

            Vector2Int current = new Vector2Int(x0, y0);
            if (current != to && BlocksSight(current))
                return false;
        }

        return true;
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
        stones.Clear();
        enemies.Clear();
        killedEnemyIds.Clear();
        activeEnemyGroups.Clear();
        clearedEnemyGroups.Clear();
        executedEventIds.Clear();
        levelEvents.Clear();
        levelDecorations.Clear();
        levelLights.Clear();
        regionsById.Clear();
        levelEnemiesKilled = 0;
        gateGroupsByCell.Clear();
        gateCellsByGroup.Clear();
        gateObjectsByCell.Clear();
        plateCellsByGroup.Clear();
        storyObjectsByCell.Clear();
        exitsByCell.Clear();
        readStoryIds.Clear();
        cameraDirectionsByCell.Clear();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                tiles[x, y] = Tile.Wall;
                tileVariants[x, y] = -1;
            }
        }

        LevelDefinition level = LoadLevelDefinition();
        if (level == null)
            return;

        if (level.size.width != Width || level.size.height != Height)
        {
            Debug.LogError($"Level {level.id} has size {level.size.width}x{level.size.height}; PrototypeGame currently expects {Width}x{Height}.");
            enabled = false;
            return;
        }

        playerStart = level.playerStart.ToVector2Int();

        if (level.tiles != null)
        {
            foreach (LevelTileRun run in level.tiles)
                ApplyTileRun(run);
        }

        if (level.walls != null)
        {
            foreach (LevelPoint wall in level.walls)
                SetWall(wall.ToVector2Int());
        }

        RegisterLevelExits(level);

        if (level.objects != null)
        {
            foreach (LevelObject obj in level.objects)
                ApplyLevelObject(obj);
        }

        if (level.enemies != null)
        {
            foreach (LevelEnemy enemy in level.enemies)
                ApplyLevelEnemy(enemy);
        }

        if (level.decorations != null)
            levelDecorations.AddRange(level.decorations);

        if (level.lights != null)
            levelLights.AddRange(level.lights);

        if (level.regions != null)
        {
            foreach (LevelRegion region in level.regions)
                RegisterRegion(region);
        }

        if (level.events != null)
            levelEvents.AddRange(level.events);

        ResetPlayerTransformIfBound();
        lastEventPlayerCell = playerStart;
    }

    private LevelDefinition LoadLevelDefinition()
    {
        TextAsset asset = ResolveLevelAsset(currentLevelId);
        if (asset == null)
        {
            Debug.LogError($"Prototype level asset '{currentLevelId}' is missing. Add it to Assets/Levels and rebuild the Prototype scene.");
            enabled = false;
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<LevelDefinition>(asset.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Prototype level could not be parsed: {ex.Message}");
            enabled = false;
            return null;
        }
    }

    private TextAsset ResolveLevelAsset(string levelId)
    {
        return LevelAssetResolver.Resolve(levelId, LevelAsset, LevelAssets, StartingLevelId);
    }

    private void ApplyTileRun(LevelTileRun run)
    {
        Tile tile = TileFromName(run.tile);
        for (int offset = 0; offset < run.length; offset++)
        {
            Vector2Int cell = new Vector2Int(run.x + offset, run.y);
            if (Inside(cell))
            {
                tiles[cell.x, cell.y] = tile;
                tileVariants[cell.x, cell.y] = run.variant;
            }
        }
    }

    private void RegisterRegion(LevelRegion region)
    {
        if (region == null || string.IsNullOrWhiteSpace(region.id))
            return;

        var cells = new HashSet<Vector2Int>();
        if (region.runs != null)
        {
            foreach (LevelTileRun run in region.runs)
            {
                for (int offset = 0; offset < run.length; offset++)
                {
                    Vector2Int cell = new Vector2Int(run.x + offset, run.y);
                    if (Inside(cell))
                        cells.Add(cell);
                }
            }
        }

        regionsById[region.id.Trim()] = cells;
    }

    private Tile TileFromName(string name)
    {
        return name switch
        {
            "floor" => Tile.Floor,
            "wall" => Tile.Wall,
            "exit" => Tile.Exit,
            "plate" => Tile.Plate,
            "gate" => Tile.Gate,
            "rubble" => Tile.Rubble,
            "trap" => Tile.Trap,
            "remote" => Tile.Remote,
            "story" => Tile.Story,
            "heal" => Tile.Heal,
            _ => Tile.Wall,
        };
    }

    private void RegisterLevelExits(LevelDefinition level)
    {
        bool hasNewExits = level.exits != null && level.exits.Count > 0;
        if (hasNewExits)
        {
            foreach (LevelExit exit in level.exits)
                RegisterExit(exit);
            return;
        }

        if (level.exit.HasValue)
        {
            Vector2Int cell = level.exit.Value.ToVector2Int();
            RegisterExit(new LevelExit
            {
                id = "exit_0",
                branch = "none",
                x = cell.x,
                y = cell.y,
            });
        }
    }

    private void RegisterExit(LevelExit exit)
    {
        Vector2Int cell = exit.ToVector2Int();
        if (!Inside(cell))
            return;

        tiles[cell.x, cell.y] = Tile.Exit;
        tileVariants[cell.x, cell.y] = exit.variant;
        exitsByCell[cell] = exit;
    }

    private void ApplyLevelObject(LevelObject obj)
    {
        Vector2Int cell = new Vector2Int(obj.x, obj.y);
        if (!Inside(cell))
            return;

        switch (obj.type)
        {
            case "gate":
                tiles[cell.x, cell.y] = Tile.Gate;
                tileVariants[cell.x, cell.y] = obj.variant;
                RegisterGate(cell, string.IsNullOrEmpty(obj.group) ? obj.id : obj.group, obj);
                break;
            case "remote":
                tiles[cell.x, cell.y] = Tile.Remote;
                tileVariants[cell.x, cell.y] = obj.variant;
                break;
            case "trap":
                tiles[cell.x, cell.y] = Tile.Trap;
                tileVariants[cell.x, cell.y] = obj.variant;
                if (obj.direction != null)
                    cameraDirectionsByCell[cell] = DirectionOrFallback(obj.direction.ToVector2(), Vector2.down);
                break;
            case "story":
                tiles[cell.x, cell.y] = Tile.Story;
                tileVariants[cell.x, cell.y] = obj.variant;
                storyObjectsByCell[cell] = obj;
                break;
            case "heal":
                tiles[cell.x, cell.y] = Tile.Heal;
                tileVariants[cell.x, cell.y] = obj.variant;
                break;
            case "plate":
                tiles[cell.x, cell.y] = Tile.Plate;
                tileVariants[cell.x, cell.y] = obj.variant;
                RegisterPlateGroup(cell, obj.group);
                break;
            case "stone":
                tileVariants[cell.x, cell.y] = obj.variant;
                AddStone(cell);
                break;
            case "rubble":
                tiles[cell.x, cell.y] = Tile.Rubble;
                tileVariants[cell.x, cell.y] = obj.variant;
                break;
        }
    }

    private void RegisterGate(Vector2Int cell, string group, LevelObject gate)
    {
        if (string.IsNullOrEmpty(group))
            group = "default";

        gateGroupsByCell[cell] = group;
        gateObjectsByCell[cell] = gate;
        if (!gateCellsByGroup.TryGetValue(group, out List<Vector2Int> cells))
        {
            cells = new List<Vector2Int>();
            gateCellsByGroup[group] = cells;
        }

        cells.Add(cell);
    }

    private void RegisterPlateGroup(Vector2Int cell, string group)
    {
        if (string.IsNullOrEmpty(group))
            group = "default";

        if (!plateCellsByGroup.TryGetValue(group, out List<Vector2Int> cells))
        {
            cells = new List<Vector2Int>();
            plateCellsByGroup[group] = cells;
        }

        cells.Add(cell);
    }

    private void ApplyLevelEnemy(LevelEnemy data)
    {
        Vector2Int start = new Vector2Int(data.x, data.y);
        var patrol = new List<Vector2Int>();
        if (data.patrol == null)
            data.patrol = new List<List<int>>();

        foreach (List<int> point in data.patrol)
        {
            if (point == null || point.Count < 2)
                continue;
            patrol.Add(new Vector2Int(point[0], point[1]));
        }

        string id = string.IsNullOrWhiteSpace(data.id) ? $"enemy_{start.x}_{start.y}_{enemies.Count}" : data.id.Trim();
        int level = Mathf.Clamp(data.level <= 0 ? EnemyBaseLevel : data.level, EnemyMinLevel, EnemyMaxLevel);
        int hp = Mathf.Max(1, data.hp <= 0 ? 2 : data.hp);
        AddEnemy(id, data.group, ParseBranch(data.branch), level, hp, start, patrol.ToArray());
    }

    private void ResetPlayerTransformIfBound()
    {
        if (playerView == null)
            return;

        playerView.transform.position = ToWorld(playerStart);
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

    private void SetWall(Vector2Int cell)
    {
        if (Inside(cell))
        {
            tiles[cell.x, cell.y] = Tile.Wall;
            tileVariants[cell.x, cell.y] = -1;
        }
    }

    private void AddStone(Vector2Int cell)
    {
        stones.Add(new Stone
        {
            Cell = cell,
            Target = ToWorld(cell),
        });
    }

    private Enemy AddEnemy(string id, string group, BranchChoice branch, int level, int hp, Vector2Int start, params Vector2Int[] patrol)
    {
        var enemy = new Enemy
        {
            Id = id,
            Group = string.IsNullOrWhiteSpace(group) ? string.Empty : group.Trim(),
            Position = ToWorld(start),
            LastSeen = start,
            Mode = EnemyMode.Patrol,
            Branch = branch,
            Level = level,
            Hp = hp,
            LookDirection = InitialEnemyLookDirection(start, patrol),
        };
        enemy.Patrol.AddRange(patrol);
        enemies.Add(enemy);
        if (!string.IsNullOrEmpty(enemy.Group))
            activeEnemyGroups.Add(enemy.Group);
        return enemy;
    }






















































#if UNITY_EDITOR










#endif

























    private Vector2Int PlayerCell()
    {
        if (playerView == null)
            return playerStart;

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
