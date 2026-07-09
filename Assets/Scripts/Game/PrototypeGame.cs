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
    private const float NoteTextRevealCharactersPerSecond = 28f;
    private const int Width = 39;
    private const int Height = 21;
    private const float CellSize = 1f;
    private const float PlayerSpeed = 4.6f;
    private const float PlayerAcceleration = 24f;
    private const float PlayerDeceleration = 18f;
    private const float RatingCritical = 15f;
    private const float GameplayCameraSize = 4.8f;
    private static readonly Color GameplayCameraBackground = new Color(0.070f, 0.076f, 0.086f);
    private const float PlayerAttackRange = 1.75f;
    private const float PlayerAttackConeMinDot = 0.08f;
    private const float PlayerAttackCooldown = 0.42f;
    private const float EnemyAttackRange = 0.96f;
    private const float EnemyAttackConeMinDot = 0.12f;
    private const float EnemyAttackWindup = 0.45f;
    private const float EnemyAttackStrike = 0.12f;
    private const float EnemyAttackRecovery = 0.76f;
    private const float BossSpecialAttackCooldown = 2.55f;
    private const float BossBroadcastRangeMultiplier = 1.82f;
    private const float BossBroadcastConeMinDot = 0.72f;
    private const float BossStaticRingRangeMultiplier = 1.58f;
    private const float BossCollisionRadius = 0.48f;
    private const float BossSummonCooldown = 18f;
    private const float BossSummonInterruptChance = 0.60f;
    private const float BossSummonInterruptStunDuration = 3f;
    private const float BossInterruptedSummonCooldownMultiplier = 0.45f;
    private const float BossMineRadius = 0.58f;
    private const float BossMineDamageDelay = 0.672f;
    private const int EnemyBaseLevel = 3;
    private const int EnemyMinLevel = 1;
    private const int EnemyMaxLevel = 99;
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
    private const float EnemyBaseSearchDuration = 3.2f;
    private const float EnemyAlertRadius = 5.5f;
    private const float EnemyDamageAlertRadius = 7.0f;
    private const float EnemySeparationRadius = 0.74f;
    private const float EnemySeparationStrength = 0.46f;
    private const float EnemyCallHelpCooldown = 5.8f;
    private const float EnemyFlankCooldown = 1.15f;
    private const float RemoteCooldown = 18f;
    private const float RemoteJamDuration = 3f;
    private const float RemoteRatingRestore = 18f;
    private const float RemoteEnemySpeedMultiplier = 0.18f;
    private const float RemoteBossChasePlayerSpeedMultiplier = 1.3f;
    private const int RemoteDamageMultiplier = 2;
    private const float FlashlightIntensity = 0.94f;
    private const float FlashlightRadius = 7.0f;
    private const float FlashlightOuterAngle = 104f;
    private const float FlashlightInnerAngle = 52f;
    private const float FlashlightAimResponsiveness = 14f;
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
    private const float DamageSignalPulseDuration = 0.38f;
    private const float SignalNoiseRefreshInterval = 0.055f;

    public Texture2D CharacterAtlas;
    public Texture2D BossAtlas;
    public Texture2D EnemyAtlas;
    public Texture2D EnvironmentAtlas;
    public Texture2D WallAtlas;
    public Texture2D HudAtlas;
    public Texture2D HudHintsAtlas;
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
    private readonly HashSet<string> seenInteractionHintTypes = new HashSet<string>();
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
    private readonly System.Random signalNoiseRandom = new System.Random(2179);

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
    [SerializeField] private Sprite flashlightSprite;
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
    [SerializeField] private Sprite hunterSprite;
    [SerializeField] private Sprite hunterWalkSprite;
    [SerializeField] private Sprite hunterInvestigateSprite;
    [SerializeField] private Sprite hunterHuntSprite;
    [SerializeField] private Sprite bruteSprite;
    [SerializeField] private Sprite bruteWalkSprite;
    [SerializeField] private Sprite bruteInvestigateSprite;
    [SerializeField] private Sprite bruteHuntSprite;
    [SerializeField] private Sprite callerSprite;
    [SerializeField] private Sprite callerWalkSprite;
    [SerializeField] private Sprite callerInvestigateSprite;
    [SerializeField] private Sprite callerHuntSprite;
    [SerializeField] private Sprite bossIdleSprite;
    [SerializeField] private Sprite bossWalkSprite;
    [SerializeField] private Sprite bossAlertSprite;
    [SerializeField] private Sprite bossAttackSprite;
    [SerializeField] private Sprite bossHurtSprite;
    [SerializeField] private Sprite bossDeathSprite;
    [SerializeField] private Sprite bossShockwaveSprite;
    [SerializeField] private Sprite bossTelegraphSprite;
    [SerializeField] private Sprite bossSummonSprite;
    [SerializeField] private Sprite bossDashSprite;
    [SerializeField] private Sprite bossInterruptSprite;
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
    private Vector2 flashlightAim = Vector2.right;
    private string currentLevelId;
    private Vector2Int playerStart = new Vector2Int(3, 10);
    private Vector2Int lastEventPlayerCell = new Vector2Int(-1, -1);
    private Vector2Int lastNoiseCell;
    private int lastNoisePower;
    private int playerHp = 6;
    private int levelEnemiesKilled;
    private int camerasBroken;
    private int camerasTriggered;
    private float viewerRating = 100f;
    private float idleTimer;
    private float criticalDamageTimer;
    private float attackCooldown;
    private float remoteCooldown;
    private float remoteJamTimer;
    private AbilitySlot equippedAbility = AbilitySlot.None;
    private bool gameEnded;
    private bool runCompleted;
    private bool paused;
    private bool showPauseBindings;
    private string message = "Канал требует внимания. Соберите сигнал и выберите, как смотреть дальше.";
    private string noteMessage;
    private string noteMessageSpeaker;
    private Texture2D noteImageTexture;
    private Texture2D notePaperTexture;
    private Texture2D signalNoiseTexture;
    private Texture2D[] glassCrackTextures;
    private Texture2D interactKeyTexture;
    private Texture2D interactKeyPressedTexture;
    private Texture2D interactPointerTexture;
    private float noteMessageTimer;
    private float noteMessageAge;
    private float noteGameplayBlockTimer;
    private float damageSignalPulseTimer;
    private float damageSignalPulseStrength;
    private float nextSignalNoiseRefresh;
    private bool noteBlocksGameplay;
    private int restartPlayerHp = 6;
    private float restartViewerRating = 100f;
    private float restartRemoteCooldown;
    private AbilitySlot restartEquippedAbility = AbilitySlot.None;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Physics2D.gravity = Vector2.zero;
        SetupCamera();
        EnsurePostProcessing();
        currentLevelId = EndlessRunState.Enabled
            ? EndlessRunState.CurrentLevelId
            : StoryStartLevelId();
        BuildLevel();
        EnsureRuntimeFallbackSprites();

        if (!HasBakedAssets() || !BindSceneViews())
        {
            Debug.LogError("Prototype scene is not baked. Run Rogue > Bootstrap All Scenes before entering Play Mode.");
            enabled = false;
            return;
        }

        EnsureGameplayLighting();
        UpdatePostProcessing();
        RedrawAll();
        ResetRoomFog();
        CaptureLevelRestartState();
        EvaluateEvents("levelStart", null, null);
        if (EndlessRunState.ConsumeCompletionOverlay())
            ShowStoryCompletionAfterOutro();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        UpdateScreenSignalEffects(Time.deltaTime);

        if (Pressed(keyboard?.escapeKey) && !gameEnded)
        {
            SetPaused(!paused);
            return;
        }

        if (paused)
            return;

        if (NoteOverlayActive())
        {
            UpdateNoteMessage(Time.deltaTime);
            if (Pressed(keyboard?.eKey) || Pressed(keyboard?.spaceKey) || Pressed(keyboard?.enterKey))
                ClearNoteMessage();
            return;
        }

        if (Pressed(keyboard?.rKey))
        {
            if (gameEnded)
                RetryAfterDeath();
            else
                Restart();
            return;
        }

        UpdateNoteMessage(Time.deltaTime);
        if (GameplayBlockedByNote())
        {
            moveInput = Vector2.zero;
            currentVelocity = Vector2.zero;
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;
            UpdatePlayerSprite();
            UpdatePlayerLight();
            return;
        }

        ReadMoveInput();
        UpdatePlayerSprite();
        UpdatePlayerLight();

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

    private void LateUpdate()
    {
        UpdateCameraShake(Time.deltaTime);
        UpdateRoomFog(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (gameEnded || paused || GameplayBlockedByNote() || playerBody == null)
        {
            if (playerBody != null)
                playerBody.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetVelocity = moveInput * PlayerMoveSpeed();
        float rate = moveInput.sqrMagnitude > 0.01f ? PlayerAcceleration : PlayerDeceleration;
        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        playerBody.linearVelocity = currentVelocity;
    }

    private float PlayerMoveSpeed()
    {
        return PlayerSpeed * (RemoteBossChaseActive() ? RemoteBossChasePlayerSpeedMultiplier : 1f);
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

    private void RetryAfterDeath()
    {
        ResetCurrentLevel();
    }

    private void SetPaused(bool value)
    {
        paused = value;
        showPauseBindings = false;
        currentVelocity = Vector2.zero;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
    }

    private void ReturnToMainMenu()
    {
        EndlessRunState.StartStory();
        paused = false;
        SceneManager.LoadScene("MainMenu");
    }

    private void ResetCurrentLevel()
    {
        ClearLevelEntityViews();

        RestoreLevelRestartState();
        idleTimer = 0f;
        criticalDamageTimer = 0f;
        attackCooldown = 0f;
        remoteJamTimer = 0f;
        ClearNoteMessage();
        lastNoisePower = 0;
        currentVelocity = Vector2.zero;
        gameEnded = false;
        paused = false;
        showPauseBindings = false;
        runCompleted = false;
        camerasBroken = 0;
        camerasTriggered = 0;

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
        ResetRoomFog();
        EvaluateEvents("levelStart", null, null);
        SpawnHitBurst(ToWorld(playerStart), false);
        message = "Канал перемотан к началу текущего уровня.";
    }

    private void CaptureLevelRestartState()
    {
        restartPlayerHp = playerHp;
        restartViewerRating = viewerRating;
        restartRemoteCooldown = remoteCooldown;
        restartEquippedAbility = equippedAbility;
    }

    private void RestoreLevelRestartState()
    {
        playerHp = restartPlayerHp;
        viewerRating = restartViewerRating;
        remoteCooldown = restartRemoteCooldown;
        equippedAbility = restartEquippedAbility;
    }

    private void RestartRun()
    {
        if (EndlessRunState.Enabled)
            EndlessRunState.ResetRun();
        else
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
        equippedAbility = AbilitySlot.None;
        ClearNoteMessage();
        gameEnded = false;
        paused = false;
        showPauseBindings = false;
        runCompleted = false;
        lastNoisePower = 0;
        currentVelocity = Vector2.zero;
        currentLevelId = EndlessRunState.Enabled
            ? EndlessRunState.CurrentLevelId
            : StoryStartLevelId();
        killedEnemyIds.Clear();
        seenInteractionHintTypes.Clear();
        levelEnemiesKilled = 0;
        camerasBroken = 0;
        camerasTriggered = 0;
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
        ResetRoomFog();
        CaptureLevelRestartState();
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

    private bool RestoreRating(float amount)
    {
        float previousRating = viewerRating;
        viewerRating = Mathf.Min(100f, viewerRating + amount);
        UpdatePostProcessing();
        bool changed = !Mathf.Approximately(previousRating, viewerRating);
        if (changed)
            RefreshStatGates();
        return changed;
    }

    private void ShowNoteMessage(string text, string speaker, bool blockGameplay = true)
    {
        noteMessage = text;
        noteMessageSpeaker = speaker;
        noteMessageAge = 0f;
        noteImageTexture = null;
        noteMessageTimer = Mathf.Clamp(4.5f + (text?.Length ?? 0) * 0.045f, 5.5f, 12f);
        noteGameplayBlockTimer = blockGameplay ? ((text?.Length ?? 0) / NoteTextRevealCharactersPerSecond) + 1f : 0f;
        noteBlocksGameplay = blockGameplay;
        moveInput = Vector2.zero;
        currentVelocity = Vector2.zero;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
    }

    private void ShowNoteImage(string imagePath)
    {
        noteMessage = string.Empty;
        noteMessageSpeaker = null;
        noteMessageAge = 0f;
        noteImageTexture = LoadNoteImageTexture(imagePath);
        noteMessageTimer = noteImageTexture != null ? 14f : 0f;
        noteGameplayBlockTimer = noteImageTexture != null ? 14f : 0f;
        noteBlocksGameplay = noteImageTexture != null;
        moveInput = Vector2.zero;
        currentVelocity = Vector2.zero;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
    }

    private Texture2D LoadNoteImageTexture(string imagePath)
    {
        string resourceKey = ResourceKeyForNoteImage(imagePath);
        if (string.IsNullOrEmpty(resourceKey))
            return null;

        Texture2D texture = Resources.Load<Texture2D>(resourceKey);
        if (texture == null)
            Debug.LogWarning($"Story note image '{imagePath}' was not found in Resources.");
        return texture;
    }

    private static string ResourceKeyForNoteImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return string.Empty;

        string normalized = imagePath.Replace('\\', '/').Trim();
        const string prefix = "Assets/Resources/";
        int index = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        string key = index >= 0 ? normalized.Substring(index + prefix.Length) : normalized;
        int extensionIndex = key.LastIndexOf('.');
        if (extensionIndex > 0)
            key = key.Substring(0, extensionIndex);
        return key;
    }

    private bool NoteOverlayActive()
    {
        return noteImageTexture != null && noteMessageTimer > 0f;
    }

    private void ClearNoteMessage()
    {
        noteMessage = null;
        noteMessageSpeaker = null;
        noteImageTexture = null;
        noteMessageTimer = 0f;
        noteMessageAge = 0f;
        noteGameplayBlockTimer = 0f;
        noteBlocksGameplay = false;
    }

    private void UpdateNoteMessage(float dt)
    {
        if (noteMessageTimer <= 0f)
            return;

        noteMessageAge += dt;
        noteMessageTimer = Mathf.Max(0f, noteMessageTimer - dt);
        if (noteGameplayBlockTimer > 0f)
            noteGameplayBlockTimer = Mathf.Max(0f, noteGameplayBlockTimer - dt);
        if (noteMessageTimer <= 0f)
            ClearNoteMessage();
    }

    private bool GameplayBlockedByNote()
    {
        return noteBlocksGameplay && noteGameplayBlockTimer > 0f && (!string.IsNullOrEmpty(noteMessage) || noteImageTexture != null);
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
                PickupAbility(next, AbilitySlot.Remote);
                return;
            }

            if (tile == Tile.Flashlight)
            {
                PickupAbility(next, AbilitySlot.Flashlight);
                return;
            }

            if (tile == Tile.Story)
            {
                LevelObject story = storyObjectsByCell.TryGetValue(next, out LevelObject storyData) ? storyData : null;
                string storyId = string.IsNullOrEmpty(story?.id) ? $"story_{next.x}_{next.y}" : story.id;
                string storyType = story != null && string.Equals(story.type, "storyImage", StringComparison.OrdinalIgnoreCase) ? "storyImage" : "story";
                MarkInteractionHintSeen(storyType);
                readStoryIds.Add(storyId);
                tiles[next.x, next.y] = Tile.Floor;
                tileVariants[next.x, next.y] = -1;
                NarrativeRunState.RecordPuzzleReflection();
                RestoreRating(18f);
                if (story != null && string.Equals(story.type, "storyImage", StringComparison.OrdinalIgnoreCase))
                {
                    message = string.Empty;
                    ShowNoteImage(story.imagePath);
                }
                else
                {
                    message = string.IsNullOrEmpty(story?.text) ? "В монтажной заметке написано: смотреть не значит соглашаться." : story.text;
                    ShowNoteMessage(message, "Записка");
                }
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

    private bool TryGetInteractionHint(out Vector2 worldPosition)
    {
        worldPosition = Vector2.zero;

        if (gameEnded || paused || NoteOverlayActive() || GameplayBlockedByNote())
            return false;

        Vector2Int playerCell = PlayerCell();
        Vector2Int direction = Cardinal(lastAim);
        if (direction != Vector2Int.zero)
        {
            Stone stone = StoneAt(playerCell + direction);
            if (stone != null && !stone.Moving && ShouldShowInteractionHint("stone"))
            {
                worldPosition = (Vector2)(stone.View != null ? stone.View.transform.position : ToWorld(stone.Cell)) + Vector2.up * 0.72f;
                return true;
            }
        }

        foreach (Vector2Int next in NeighborCells(playerCell))
        {
            if (!Inside(next))
                continue;

            Tile tile = tiles[next.x, next.y];
            switch (tile)
            {
                case Tile.Story:
                    string storyType = storyObjectsByCell.TryGetValue(next, out LevelObject story) && string.Equals(story.type, "storyImage", StringComparison.OrdinalIgnoreCase)
                        ? "storyImage"
                        : "story";
                    if (!ShouldShowInteractionHint(storyType))
                        continue;
                    worldPosition = (Vector2)ToWorld(next) + Vector2.up * 0.72f;
                    return true;
                case Tile.Heal:
                    if (playerHp >= 6 || !ShouldShowInteractionHint("heal"))
                        continue;
                    worldPosition = (Vector2)ToWorld(next) + Vector2.up * 0.72f;
                    return true;
                case Tile.Remote:
                    if (!ShouldShowInteractionHint("remote"))
                        continue;
                    worldPosition = (Vector2)ToWorld(next) + Vector2.up * 0.72f;
                    return true;
                case Tile.Flashlight:
                    if (!ShouldShowInteractionHint("flashlight"))
                        continue;
                    worldPosition = (Vector2)ToWorld(next) + Vector2.up * 0.72f;
                    return true;
            }
        }

        return false;
    }

    private bool ShouldShowInteractionHint(string type)
    {
        return !string.IsNullOrEmpty(type) && !seenInteractionHintTypes.Contains(type);
    }

    private void MarkInteractionHintSeen(string type)
    {
        if (!string.IsNullOrEmpty(type))
            seenInteractionHintTypes.Add(type);
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

        if (!HasRemote)
        {
            message = HasFlashlight ? "Сейчас в слоте фонарь: он светит сам, а Q не использует пульт." : "Пульт ещё где-то в эфире. Найдите его и нажмите E.";
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
        message = RemoteBossChaseActive()
            ? "Пульт активен. Босс не теряет сигнал, но вы успеваете двигаться быстрее."
            : "Пульт активен. Дикторы вязнут в помехах.";
    }

    private void PickupAbility(Vector2Int cell, AbilitySlot ability)
    {
        MarkInteractionHintSeen(ability == AbilitySlot.Remote ? "remote" : ability == AbilitySlot.Flashlight ? "flashlight" : null);
        AbilitySlot previous = equippedAbility;
        equippedAbility = ability;
        if (ability != AbilitySlot.Remote || previous != AbilitySlot.Remote)
        {
            remoteCooldown = 0f;
            remoteJamTimer = 0f;
        }

        Tile droppedTile = AbilityTile(previous);
        bool swappedAbility = previous != AbilitySlot.None && previous != ability && droppedTile != Tile.Floor;
        tiles[cell.x, cell.y] = swappedAbility ? droppedTile : Tile.Floor;
        tileVariants[cell.x, cell.y] = -1;
        NarrativeRunState.RecordSignalInsight();
        RestoreRating(ability == AbilitySlot.Remote ? 10f : 6f);
        RedrawTile(cell);
        UpdatePlayerLight();

        if (ability == AbilitySlot.Remote)
            message = previous == AbilitySlot.Flashlight ? "Вы сменили фонарь на пульт. Фонарь остался лежать рядом." : "Пульт: Q активирует помехи на 3 секунды. КД 18 секунд.";
        else
            message = previous == AbilitySlot.Remote ? "Вы сменили пульт на фонарь. Пульт остался лежать рядом." : "Фонарь: пассивно светит по направлению движения. Слот занят фонарём.";
    }

    private static Tile AbilityTile(AbilitySlot ability)
    {
        return ability switch
        {
            AbilitySlot.Remote => Tile.Remote,
            AbilitySlot.Flashlight => Tile.Flashlight,
            _ => Tile.Floor,
        };
    }

    private bool HasRemote => equippedAbility == AbilitySlot.Remote;

    private bool HasFlashlight => equippedAbility == AbilitySlot.Flashlight;

    private bool TryPushStone()
    {
        Vector2Int direction = Cardinal(lastAim);
        if (direction == Vector2Int.zero)
            return false;

        Vector2Int from = PlayerCell();
        Stone stone = StoneAt(from + direction);
        if (stone == null || stone.Moving)
            return false;

        MarkInteractionHintSeen("stone");
        if (!TryFindStonePushDestination(stone, from, direction, out Vector2Int destination))
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

    private bool TryFindStonePushDestination(Stone stone, Vector2Int playerCell, Vector2Int pushDirection, out Vector2Int destination)
    {
        Vector2Int left = new Vector2Int(-pushDirection.y, pushDirection.x);
        Vector2Int firstSide = UnityEngine.Random.value < 0.5f ? left : -left;
        Vector2Int secondSide = -firstSide;
        Vector2Int[] candidates =
        {
            stone.Cell + pushDirection,
            playerCell - pushDirection,
            stone.Cell + firstSide,
            stone.Cell + secondSide,
        };

        foreach (Vector2Int candidate in candidates)
        {
            if (candidate == playerCell - pushDirection && !SafeStoneBackThrowCell(candidate, pushDirection, playerCell))
                continue;

            if (CanStoneEnter(candidate, playerCell))
            {
                destination = candidate;
                return true;
            }
        }

        destination = stone.Cell;
        return false;
    }

    private bool SafeStoneBackThrowCell(Vector2Int candidate, Vector2Int pushDirection, Vector2Int playerCell)
    {
        if (!CanStoneEnter(candidate, playerCell))
            return false;

        int freeCells = 0;
        for (int offset = 1; offset <= 4; offset++)
        {
            Vector2Int cell = playerCell - pushDirection * offset;
            if (!CanStoneEnter(cell, playerCell))
                break;

            freeCells++;
        }

        return freeCells >= 4;
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

        bool summonInterruptRoll = target.Archetype == EnemyArchetype.Boss &&
                                   target.BossAttackKind == BossAttackKind.Summon &&
                                   target.AttackWindupTimer > 0f;
        bool summonInterrupted = summonInterruptRoll && UnityEngine.Random.value < BossSummonInterruptChance;
        int damage = PlayerAttackDamage();
        target.Hp = Mathf.Max(0, target.Hp - damage);
        target.Mode = EnemyMode.Hunt;
        target.LastSeen = PlayerCell();
        target.LostSightTimer = 0f;
        target.SearchTimer = EnemySearchDurationFor(target);
        BuildEnemySearchPoints(target, target.LastSeen);
        target.StunTimer = summonInterrupted ? BossSummonInterruptStunDuration : summonInterruptRoll ? 0f : EnemyStunDuration;
        target.HitFlashTimer = EnemyHitFlashDuration;
        target.KnockbackVelocity = summonInterruptRoll ? Vector2.zero : away * EnemyKnockbackSpeed;
        if (!summonInterruptRoll || summonInterrupted)
            CancelEnemyAttack(target);
        if (summonInterrupted)
        {
            target.SummonCooldown = BossSummonCooldown * BossInterruptedSummonCooldownMultiplier;
            target.BossInterruptPoseTimer = BossSummonInterruptStunDuration;
            SpawnBossFlash(target.Position, 2.8f, 3.2f);
        }
        AlertEnemiesAround(PlayerCell(), target.Position, EnemyDamageAlertRadius);
        TryAlertEnemies(target, PlayerCell(), target.Archetype == EnemyArchetype.Caller);
        SpawnHitBurst(target.Position, target.Hp <= 0);
        if (target.Hp <= 0 && target.Archetype == EnemyArchetype.Boss)
            SpawnBossDeathEffect(target.Position);
        else if (target.Hp <= 0)
            SpawnEnemyDeathFlash(target.Position);
        message = target.Hp <= 0
            ? target.Archetype == EnemyArchetype.Boss ? "Босс разваливается на мёртвый эфир." : "Диктор рассыпался в белый шум."
            : summonInterrupted ? "Вы сбиваете призыв. Эфир захлёбывается помехой."
            : summonInterruptRoll ? "Удар проходит, но босс удерживает канал призыва."
            : RemoteJamActive() && target.Archetype == EnemyArchetype.Boss ? "Пульт усиливает удар, но босс не теряет сигнал."
            : RemoteJamActive() ? "Пульт усиливает удар. Диктор теряет сигнал." : "Диктор сбился с текста.";

        if (target.Hp > 0)
        {
            if (target.Archetype == EnemyArchetype.Boss)
                RefreshStatGates();
            return;
        }

        NarrativeRunState.RecordKill();
        levelEnemiesKilled += 1;
        if (!string.IsNullOrEmpty(target.Id))
            killedEnemyIds.Add(target.Id);
        bool ratingChanged = RestoreRating(12f);
        DestroyEnemyTelegraph(target);
        if (target.View != null)
            target.View.SetActive(false);
        enemies.Remove(target);
        EvaluateEvents("enemyKilled", target.Id, target.Group);
        CheckEnemyGroupCleared(target.Group);
        if (target.Archetype == EnemyArchetype.Boss && !ratingChanged)
            RefreshStatGates();
        RedrawGateGroups();
        RedrawExits();
    }

    private int PlayerAttackDamage()
    {
        return RemoteJamActive() ? RemoteDamageMultiplier : 1;
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
        if (!RestoreRating(8f))
            RefreshStatGates();
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
        Vector2 playerPosition = playerView != null ? (Vector2)playerView.transform.position : ToWorld(playerStart);
        TriggerCameraShake(amount);
        TriggerScreenSignalPulse(amount);
        SpawnPlayerDamageBurst(playerPosition, amount, playerHp <= 0);
        message = playerHp <= 0 ? "Game Over: эфир оставил только шум." : text;
        RefreshStatGates();
        if (playerHp <= 0)
        {
            gameEnded = true;
            paused = false;
            showPauseBindings = false;
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

        MarkInteractionHintSeen("heal");
        playerHp = Mathf.Min(6, playerHp + 2);
        tiles[cell.x, cell.y] = Tile.Floor;
        tileVariants[cell.x, cell.y] = -1;
        message = "Кассета перематывает боль назад. HP +2.";
        if (!RestoreRating(4f))
            RefreshStatGates();
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
            camerasTriggered += 1;
            NarrativeRunState.RecordTrapMistake();
            MakeNoise(playerCell, 9);
            DamagePlayer(1, "Камера ослепляет вспышкой. Рейтинг вздрагивает.");
            viewerRating = Mathf.Max(0f, viewerRating - 8f);
            UpdatePostProcessing();
            RedrawTile(playerCell);
            RefreshStatGates();
            EvaluateEvents("statsChanged", null, null);
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
        {
            if (EndlessRunState.Enabled && exitsByCell.ContainsKey(cell))
                message = $"Выход включится после зачистки: осталось врагов {enemies.Count}.";
            return;
        }

        if (EndlessRunState.Enabled)
        {
            LoadNextEndlessLevel();
            return;
        }

        LevelExit exit = exitsByCell.TryGetValue(cell, out LevelExit data) ? data : null;
        string targetLevel = LevelAssetResolver.NormalizeLevelId(exit?.targetLevel);
        if (!string.IsNullOrEmpty(targetLevel))
        {
            LoadNextLevel(targetLevel);
            return;
        }

        StartStoryOutro();
    }

    private void StartStoryOutro()
    {
        currentVelocity = Vector2.zero;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        EndlessRunState.RequestStoryOutro();
        GameMusic.Stop();
        SceneManager.LoadScene("Intro");
    }

    private void ShowStoryCompletionAfterOutro()
    {
        gameEnded = true;
        runCompleted = true;
        paused = false;
        showPauseBindings = false;
        ClearNoteMessage();
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
        ResetRoomFog();
        CaptureLevelRestartState();
        EvaluateEvents("levelStart", null, null);
        message = $"Канал переключён: {currentLevelId}.";
    }

    private void ResetLevelLocalState()
    {
        attackCooldown = 0f;
        remoteJamTimer = 0f;
        runCompleted = false;
        lastNoisePower = 0;
        ClearNoteMessage();
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

        ClearSceneRuntimeLevelObjects();
        combatVfxRoot = null;
    }

    private void ClearSceneRuntimeLevelObjects()
    {
        DestroySceneObjectsWithPrefix("Enemy ");
        DestroySceneObjectsWithPrefix("Signal Blocker ");
        DestroySceneObjectsWithPrefix(LevelVisualRootName);
        DestroySceneObjectsWithPrefix("Combat VFX");
        DestroySceneObjectsWithPrefix("Attack Swing");
        DestroySceneObjectsWithPrefix("White Noise Burst");
        DestroySceneObjectsWithPrefix("Hit Spark");
        DestroySceneObjectsWithPrefix("Camera Flash Light");
        DestroySceneObjectsWithPrefix("Enemy Death Flash");
    }

    private static void DestroyRuntimeObject(GameObject obj)
    {
        if (obj == null)
            return;

        obj.SetActive(false);
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private bool CanUseExit(Vector2Int cell)
    {
        if (!exitsByCell.TryGetValue(cell, out LevelExit exit))
            return false;

        if (EndlessRunState.Enabled)
            return enemies.Count == 0;

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
        MarkRoomFogDirty();
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
        return new GateStatSnapshot(NarrativeRunState.EnemiesKilled, levelEnemiesKilled, camerasBroken, camerasTriggered, viewerRating, playerHp, NearestBossHp());
    }

    private int NearestBossHp()
    {
        Vector2 origin = playerView != null ? (Vector2)playerView.transform.position : ToWorld(playerStart);
        float bestDistance = float.PositiveInfinity;
        int hp = 0;
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.Archetype != EnemyArchetype.Boss || enemy.Hp <= 0)
                continue;

            float distance = Vector2.SqrMagnitude(enemy.Position - origin);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            hp = enemy.Hp;
        }

        return hp;
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
        MarkRoomFogDirty();
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
        if (rebuildColliders)
            MarkRoomFogDirty();
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
                SpawnEventRubble(new Vector2Int(action.x, action.y), action.variant, true);
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
            case "showMonologue":
                ShowNoteMessage(action.text, "Вы");
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
            type = action.enemy?.type ?? action.objectType,
            alertGroup = action.enemy?.alertGroup ?? action.group,
            x = action.x,
            y = action.y,
            level = action.enemy?.level ?? 3,
            hp = action.enemy?.hp ?? 2,
            hearing = action.enemy?.hearing ?? 0f,
            vision = action.enemy?.vision ?? 0f,
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

    private void SpawnEventRubble(Vector2Int cell, int variant, bool falling)
    {
        if (!Inside(cell))
            return;

        if (StoneAt(cell) is Stone stone)
        {
            DestroyRuntimeObject(stone.View);
            stones.Remove(stone);
        }

        tiles[cell.x, cell.y] = Tile.Rubble;
        tileVariants[cell.x, cell.y] = variant;
        if (falling)
            SpawnHitBurst(ToWorld(cell), false);
        RedrawTile(cell);
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
            if (enemy.SpecialAttackCooldown > 0f)
                enemy.SpecialAttackCooldown = Mathf.Max(0f, enemy.SpecialAttackCooldown - dt);
            if (enemy.SummonCooldown > 0f)
                enemy.SummonCooldown = Mathf.Max(0f, enemy.SummonCooldown - dt);
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
            float speed = EnemyBaseMoveSpeed(enemy);
            speed *= EnemySpeedScale(enemy);
            if (EnemyBlockedByRemote(enemy))
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

            if (!EnemyBlockedByRemote(enemy) && EnemyCanStartAttack(enemy))
                StartEnemyAttack(enemy);
        }

        if (lastNoisePower > 0)
            lastNoisePower = Mathf.Max(0, lastNoisePower - Mathf.CeilToInt(dt * 2f));
    }

    private void UpdateEnemyHitTimers(Enemy enemy, float dt)
    {
        enemy.HitFlashTimer = Mathf.Max(0f, enemy.HitFlashTimer - dt);
        enemy.StunTimer = Mathf.Max(0f, enemy.StunTimer - dt);
        enemy.SearchTimer = Mathf.Max(0f, enemy.SearchTimer - dt);
        enemy.AlertTimer = Mathf.Max(0f, enemy.AlertTimer - dt);
        enemy.FlankCooldown = Mathf.Max(0f, enemy.FlankCooldown - dt);
        enemy.CallHelpCooldown = Mathf.Max(0f, enemy.CallHelpCooldown - dt);
        enemy.BossInterruptPoseTimer = Mathf.Max(0f, enemy.BossInterruptPoseTimer - dt);
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
                if (enemy.Archetype == EnemyArchetype.Boss)
                    SpawnBossAttackPulse(enemy);
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
                if (!EnemyBlockedByRemote(enemy))
                    ApplyEnemyStrike(enemy);
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

        float range = EnemyAttackRangeFor(enemy);
        if (enemy.Archetype == EnemyArchetype.Boss)
            range *= BossBroadcastRangeMultiplier;
        return Vector2.Distance(enemy.Position, playerView.transform.position) <= range;
    }

    private void StartEnemyAttack(Enemy enemy)
    {
        Vector2 direction = (Vector2)playerView.transform.position - enemy.Position;
        enemy.AttackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        enemy.LookDirection = enemy.AttackDirection;
        enemy.BossAttackKind = ChooseBossAttackKind(enemy);
        enemy.AttackWindupTimer = EnemyAttackWindupFor(enemy);
        enemy.AttackStrikeTimer = 0f;
        enemy.AttackRecoveryTimer = 0f;
        enemy.AttackApplied = false;
        enemy.KnockbackVelocity = Vector2.zero;
        UpdateEnemyTelegraph(enemy, false);
    }

    private BossAttackKind ChooseBossAttackKind(Enemy enemy)
    {
        if (enemy.Archetype != EnemyArchetype.Boss || enemy.SpecialAttackCooldown > 0f)
            return BossAttackKind.Slam;

        float distance = playerView == null ? 0f : Vector2.Distance(enemy.Position, playerView.transform.position);
        enemy.SpecialAttackCooldown = BossSpecialAttackCooldown;
        bool close = distance <= EnemyAttackRangeFor(enemy) * 1.05f;
        BossAttackKind choice = PickBossSpecialAttack(close, enemy.SummonCooldown <= 0f);
        if (choice == BossAttackKind.Summon && !TryPickBossSummonSpawn(enemy, out _, out _))
            choice = PickBossSpecialAttack(close, false);
        return choice;
    }

    private BossAttackKind PickBossSpecialAttack(bool close, bool allowSummon)
    {
        float ring = close ? 0.35f : 0.15f;
        float mines = close ? 0.30f : 0.30f;
        float summon = allowSummon ? 0.20f : 0f;
        float cone = close ? 0.15f : 0.35f;
        float total = ring + mines + summon + cone;
        float roll = UnityEngine.Random.value * total;
        if (roll < ring)
            return BossAttackKind.StaticRing;
        roll -= ring;
        if (roll < mines)
            return BossAttackKind.StaticMines;
        roll -= mines;
        if (roll < summon)
            return BossAttackKind.Summon;
        return BossAttackKind.BroadcastCone;
    }

    private void ApplyEnemyStrike(Enemy enemy)
    {
        if (enemy.Archetype == EnemyArchetype.Boss)
        {
            ApplyBossStrike(enemy);
            return;
        }

        if (PlayerInEnemyAttackZone(enemy))
            DamagePlayer(EnemyAttackDamageFor(enemy), enemy.Mode == EnemyMode.Hunt ? "Диктор ловит вас в кадре после замаха." : "Диктор бьёт микрофоном после паузы.");
    }

    private void ApplyBossStrike(Enemy enemy)
    {
        bool hit = enemy.BossAttackKind switch
        {
            BossAttackKind.BroadcastCone => PlayerInBossBroadcastCone(enemy),
            BossAttackKind.StaticRing => PlayerInBossStaticRing(enemy),
            BossAttackKind.StaticMines => false,
            BossAttackKind.Summon => false,
            _ => PlayerInEnemyAttackZone(enemy),
        };
        if (!hit)
            return;

        int damage = enemy.BossAttackKind == BossAttackKind.StaticRing
            ? Mathf.Max(1, EnemyAttackDamageFor(enemy) - 1)
            : EnemyAttackDamageFor(enemy);
        string text = enemy.BossAttackKind switch
        {
            BossAttackKind.BroadcastCone => "Босс прожигает комнату эфирным лучом. Уклоняться нужно заранее.",
            BossAttackKind.StaticRing => "Статика расходится кольцом. Рядом с боссом теперь смертельно.",
            _ => "Босс сбивает вас тяжёлым ударом.",
        };
        DamagePlayer(damage, text);
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
        if (toPlayer.magnitude > EnemyAttackRangeFor(enemy))
            return false;
        if (toPlayer.sqrMagnitude < 0.001f)
            return true;

        Vector2 direction = enemy.AttackDirection.sqrMagnitude > 0.001f ? enemy.AttackDirection.normalized : toPlayer.normalized;
        return Vector2.Dot(direction, toPlayer.normalized) >= EnemyAttackConeMinDot;
    }

    private bool PlayerInBossBroadcastCone(Enemy enemy)
    {
        if (playerView == null)
            return false;

        Vector2 toPlayer = (Vector2)playerView.transform.position - enemy.Position;
        float range = EnemyAttackRangeFor(enemy) * BossBroadcastRangeMultiplier;
        if (toPlayer.magnitude > range)
            return false;
        if (toPlayer.sqrMagnitude < 0.001f)
            return true;

        Vector2 direction = DirectionOrFallback(enemy.AttackDirection, toPlayer);
        return Vector2.Dot(direction, toPlayer.normalized) >= BossBroadcastConeMinDot;
    }

    private bool PlayerInBossStaticRing(Enemy enemy)
    {
        if (playerView == null)
            return false;

        float range = EnemyAttackRangeFor(enemy) * BossStaticRingRangeMultiplier;
        return Vector2.Distance(enemy.Position, playerView.transform.position) <= range;
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
        EnemyArchetype kind = enemy == null ? EnemyArchetype.Patrol : enemy.Archetype;
        float archetype = kind switch
        {
            EnemyArchetype.Hunter => 1.16f,
            EnemyArchetype.Brute => 0.82f,
            EnemyArchetype.Caller => 0.95f,
            EnemyArchetype.Boss => 0.982f,
            _ => 1f,
        };
        return Mathf.Clamp((1f + EnemyLevelOffset(enemy) * 0.10f) * archetype, 0.70f, 1.68f);
    }

    private static float EnemyAttackWindupFor(Enemy enemy)
    {
        EnemyArchetype kind = enemy == null ? EnemyArchetype.Patrol : enemy.Archetype;
        float archetype = kind switch
        {
            EnemyArchetype.Hunter => 0.88f,
            EnemyArchetype.Brute => 1.24f,
            EnemyArchetype.Caller => 1.08f,
            EnemyArchetype.Boss => enemy.BossAttackKind switch
            {
                BossAttackKind.BroadcastCone => 1.66f,
                BossAttackKind.StaticRing => 1.58f,
                BossAttackKind.StaticMines => 1.54f,
                BossAttackKind.Summon => 9.00f,
                _ => 1.11f,
            },
            _ => 1f,
        };
        return EnemyAttackWindup * archetype / Mathf.Clamp(1f + EnemyLevelOffset(enemy) * 0.08f, 0.70f, 1.60f);
    }

    private static float EnemyAttackRecoveryFor(Enemy enemy)
    {
        EnemyArchetype kind = enemy == null ? EnemyArchetype.Patrol : enemy.Archetype;
        float archetype = kind switch
        {
            EnemyArchetype.Hunter => 0.88f,
            EnemyArchetype.Brute => 1.18f,
            EnemyArchetype.Caller => 1.05f,
            EnemyArchetype.Boss => 0.75f,
            _ => 1f,
        };
        return EnemyAttackRecovery * archetype / Mathf.Clamp(1f + EnemyLevelOffset(enemy) * 0.15f, 0.60f, 1.90f);
    }

    private static int EnemyAttackDamageFor(Enemy enemy)
    {
        int damage = 1 + Mathf.FloorToInt((EnemyLevel(enemy) - 1) / 4f);
        if (enemy != null && enemy.Archetype == EnemyArchetype.Brute)
            damage += 1;
        else if (enemy != null && enemy.Archetype == EnemyArchetype.Boss)
            damage += 1;
        return damage;
    }

    private static float EnemyAttackRangeFor(Enemy enemy)
    {
        if (enemy == null)
            return EnemyAttackRange;

        return enemy.Archetype switch
        {
            EnemyArchetype.Brute => EnemyAttackRange * 1.12f,
            EnemyArchetype.Boss => EnemyAttackRange * 1.42f,
            _ => EnemyAttackRange,
        };
    }

    private static float EnemyBaseMoveSpeed(Enemy enemy)
    {
        return enemy.Mode switch
        {
            EnemyMode.Hunt => 2.55f,
            EnemyMode.Investigate => 2.1f,
            _ => 1.55f,
        };
    }

    private void UpdateEnemyVisual(Enemy enemy)
    {
        SpriteRenderer enemyRenderer = enemy.View.GetComponent<SpriteRenderer>();
        if (enemyRenderer == null)
            return;

        enemyRenderer.sprite = SpriteForEnemyMode(enemy);
        enemyRenderer.flipX = EnemySpriteFlipX(enemy);
        Color color = EnemyColor(enemy);
        if (EnemyBlockedByRemote(enemy))
            color = Color.Lerp(color, new Color(0.48f, 0.92f, 1.00f), 0.62f);

        if (enemy.HitFlashTimer > 0f)
        {
            float pulse = Mathf.PingPong(enemy.HitFlashTimer * 34f, 1f);
            color = Color.Lerp(color, Color.white, 0.55f + pulse * 0.35f);
        }

        enemyRenderer.color = color;
        float archetypeScale = enemy.Archetype switch
        {
            EnemyArchetype.Boss => 1.72f,
            EnemyArchetype.Brute => 1.14f,
            EnemyArchetype.Hunter => 0.96f,
            _ => 1f,
        };
        float punch = enemy.HitFlashTimer > 0f ? Mathf.Lerp(1.0f, 1.18f, enemy.HitFlashTimer / EnemyHitFlashDuration) : 1f;
        if (enemy.AttackWindupTimer > 0f)
            punch += Mathf.PingPong(Time.time * 10f, 0.05f);
        enemy.View.transform.localScale = new Vector3(punch * archetypeScale, punch * archetypeScale, 1f);
        ConfigureEnemyLight(enemy);
    }

    private static bool EnemySpriteFlipX(Enemy enemy)
    {
        if (enemy == null || enemy.Archetype != EnemyArchetype.Boss)
            return false;

        Vector2 direction = enemy.AttackWindupTimer > 0f || enemy.AttackStrikeTimer > 0f
            ? enemy.AttackDirection
            : enemy.LookDirection;
        return direction.x < -0.12f;
    }

    private void UpdateEnemyTelegraph(Enemy enemy, bool striking)
    {
        if (enemy.TelegraphView == null)
            enemy.TelegraphView = CreateTelegraphView(enemy);

        Vector2 direction = enemy.AttackDirection.sqrMagnitude > 0.001f ? enemy.AttackDirection.normalized : Vector2.down;
        float range = EnemyAttackRangeFor(enemy);
        if (enemy.Archetype == EnemyArchetype.Boss)
            range *= enemy.BossAttackKind == BossAttackKind.BroadcastCone ? BossBroadcastRangeMultiplier : enemy.BossAttackKind == BossAttackKind.StaticRing ? BossStaticRingRangeMultiplier : 1f;
        enemy.TelegraphView.SetActive(true);
        bool bossRing = enemy.Archetype == EnemyArchetype.Boss && enemy.BossAttackKind == BossAttackKind.StaticRing;
        bool bossSummon = enemy.Archetype == EnemyArchetype.Boss && enemy.BossAttackKind == BossAttackKind.Summon;
        enemy.TelegraphView.transform.position = bossRing || bossSummon ? enemy.Position : enemy.Position + direction * (range * 0.48f);
        float telegraphWidth = enemy.Archetype == EnemyArchetype.Boss
            ? enemy.BossAttackKind == BossAttackKind.BroadcastCone ? 1.55f : enemy.BossAttackKind == BossAttackKind.StaticRing ? range * 1.55f : bossSummon ? 1.25f : 0.95f
            : enemy.Archetype == EnemyArchetype.Brute ? 0.78f : 0.62f;
        enemy.TelegraphView.transform.localScale = bossRing || bossSummon ? new Vector3(telegraphWidth, telegraphWidth, 1f) : new Vector3(telegraphWidth, range * 0.92f, 1f);
        enemy.TelegraphView.transform.localRotation = bossRing || bossSummon ? Quaternion.identity : Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        SpriteRenderer renderer = enemy.TelegraphView.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        float windup = EnemyAttackWindupFor(enemy);
        float windupRatio = enemy.AttackWindupTimer > 0f ? 1f - enemy.AttackWindupTimer / windup : 1f;
        renderer.sprite = bossSummon && bossSummonSprite != null ? bossSummonSprite : bossRing && bossTelegraphSprite != null ? bossTelegraphSprite : EnsureEffectSprite();
        Color warning = enemy.Archetype == EnemyArchetype.Boss
            ? new Color(1f, 0.12f, 0.12f, 0.34f)
            : new Color(1f, 0.80f, 0.20f, 0.22f);
        Color danger = enemy.Archetype == EnemyArchetype.Boss
            ? new Color(1f, 0.08f, 0.06f, 0.68f)
            : new Color(1f, 0.28f, 0.14f, 0.46f);
        renderer.color = striking
            ? new Color(1f, 0.18f, 0.12f, enemy.Archetype == EnemyArchetype.Boss ? 0.72f : 0.58f)
            : Color.Lerp(warning, danger, windupRatio);
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

        DestroyRuntimeObject(enemy.TelegraphView);
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

    private void SpawnPlayerDamageBurst(Vector2 position, int damage, bool fatal)
    {
        int count = Mathf.Clamp(9 + damage * 4 + (fatal ? 7 : 0), 9, 24);
        Color primary = fatal ? new Color(0.95f, 0.98f, 1f, 0.88f) : new Color(1f, 0.30f, 0.24f, 0.78f);
        Color secondary = new Color(0.64f, 0.88f, 1f, fatal ? 0.72f : 0.52f);
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = UnityEngine.Random.Range(fatal ? 2.2f : 1.4f, fatal ? 4.3f : 3.0f);
            float size = UnityEngine.Random.Range(fatal ? 0.08f : 0.055f, fatal ? 0.20f : 0.145f);
            float duration = UnityEngine.Random.Range(fatal ? 0.34f : 0.20f, fatal ? 0.62f : 0.42f);
            Color color = Color.Lerp(primary, secondary, UnityEngine.Random.value * 0.55f);
            CombatEffect effect = CreateCombatEffect(
                fatal ? "Player Signal Collapse" : "Player Damage Spark",
                position + direction * UnityEngine.Random.Range(0.03f, 0.24f),
                Vector3.one * size,
                color,
                duration,
                28);
            effect.Velocity = direction * speed + UnityEngine.Random.insideUnitCircle * 0.35f;
            effect.EndScale = effect.StartScale * UnityEngine.Random.Range(0.15f, 0.48f);
            effect.RotationSpeed = UnityEngine.Random.Range(-360f, 360f);
        }
    }

    private void SpawnBossAttackPulse(Enemy enemy)
    {
        switch (enemy.BossAttackKind)
        {
            case BossAttackKind.BroadcastCone:
                SpawnBossBroadcastCone(enemy);
                return;
            case BossAttackKind.StaticRing:
                SpawnBossStaticRing(enemy);
                return;
            case BossAttackKind.StaticMines:
                SpawnBossStaticMines(enemy);
                return;
            case BossAttackKind.Summon:
                TrySpawnBossMinion(enemy);
                return;
            default:
                SpawnBossSlamPulse(enemy);
                return;
        }
    }

    private void SpawnBossSlamPulse(Enemy enemy)
    {
        Vector2 direction = DirectionOrFallback(enemy.AttackDirection, Vector2.down);
        CombatEffect effect = CreateCombatEffect(
            "Boss Slam Shockwave",
            enemy.Position + direction * 0.92f,
            Vector3.one * 0.92f,
            new Color(1f, 0.30f, 0.24f, 0.70f),
            0.26f,
            26,
            bossDashSprite != null ? bossDashSprite : bossShockwaveSprite != null ? bossShockwaveSprite : bossTelegraphSprite);
        effect.EndScale = Vector3.one * 1.75f;
        effect.Velocity = direction * 1.25f;
        effect.View.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        SpawnBossFlash(enemy.Position + direction * 0.45f, 2.8f, 3.0f);
    }

    private void SpawnBossBroadcastCone(Enemy enemy)
    {
        Vector2 direction = DirectionOrFallback(enemy.AttackDirection, Vector2.down);
        CombatEffect effect = CreateCombatEffect(
            "Boss Broadcast Cone",
            enemy.Position + direction * 1.7f,
            new Vector3(1.25f, 3.8f, 1f),
            new Color(1f, 0.18f, 0.12f, 0.58f),
            0.34f,
            26,
            bossShockwaveSprite != null ? bossShockwaveSprite : EnsureEffectSprite());
        effect.EndScale = new Vector3(2.25f, 5.4f, 1f);
        effect.Velocity = direction * 1.9f;
        effect.View.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        SpawnBossFlash(enemy.Position + direction * 1.1f, 3.9f, 5.6f);
        SpawnHitBurst(enemy.Position + direction * 1.35f, false);
    }

    private void SpawnBossStaticRing(Enemy enemy)
    {
        CombatEffect effect = CreateCombatEffect(
            "Boss Static Ring",
            enemy.Position,
            Vector3.one * 1.1f,
            new Color(1f, 0.12f, 0.08f, 0.72f),
            0.38f,
            26,
            bossTelegraphSprite != null ? bossTelegraphSprite : bossShockwaveSprite);
        effect.EndScale = Vector3.one * (EnemyAttackRangeFor(enemy) * BossStaticRingRangeMultiplier * 1.9f);
        effect.RotationSpeed = 220f;
        SpawnBossFlash(enemy.Position, 3.4f, 4.4f);
        SpawnHitBurst(enemy.Position, false);
    }

    private void SpawnBossStaticMines(Enemy enemy)
    {
        var cells = new List<Vector2Int>();
        Vector2Int playerCell = PlayerCell();
        AddBossMineCell(cells, playerCell);
        Vector2 aim = DirectionOrFallback(moveInput.sqrMagnitude > 0.01f ? moveInput : lastAim, Vector2.down);
        AddBossMineCell(cells, playerCell + Cardinal(aim));

        Vector2 towardPlayer = DirectionOrFallback((Vector2)(playerCell - WorldToCell(enemy.Position)), Vector2.down);
        Vector2 side = new Vector2(-towardPlayer.y, towardPlayer.x);
        AddBossMineCell(cells, playerCell + Cardinal(side));
        AddBossMineCell(cells, playerCell + Cardinal(-side));

        foreach (Vector2Int cell in cells)
            SpawnBossMineAt(cell);
    }

    private void AddBossMineCell(List<Vector2Int> cells, Vector2Int cell)
    {
        if (!BossMineCellPassable(cell) || cells.Contains(cell))
            return;

        cells.Add(cell);
    }

    private bool BossMineCellPassable(Vector2Int cell)
    {
        return Inside(cell) && !IsSolidCell(cell) && StoneAt(cell) == null;
    }

    private void SpawnBossMineAt(Vector2Int cell)
    {
        CombatEffect effect = CreateCombatEffect(
            "Boss Static Mine",
            ToWorld(cell),
            Vector3.one * 0.74f,
            new Color(1f, 0.18f, 0.10f, 0.58f),
            BossMineDamageDelay + 0.34f,
            26,
            bossTelegraphSprite != null ? bossTelegraphSprite : bossShockwaveSprite);
        effect.EndScale = Vector3.one * 1.22f;
        effect.RotationSpeed = 150f;
        effect.DamageDelay = BossMineDamageDelay;
        effect.DamageRadius = BossMineRadius;
        effect.DamageAmount = 1;
        effect.DamageMessage = "Статическая мина босса вспыхивает под ногами.";
    }

    private bool TrySpawnBossMinion(Enemy boss)
    {
        if (!TryPickBossSummonSpawn(boss, out Vector2Int spawnCell, out bool farSpawn))
            return false;

        EnemyArchetype archetype = PickBossMinionArchetype();
        int hp = UnityEngine.Random.Range(2, 4);
        int level = Mathf.Clamp(EnemyLevel(boss) + UnityEngine.Random.Range(-1, 2), EnemyMinLevel, EnemyMaxLevel);
        string id = $"boss_minion_{spawnCell.x}_{spawnCell.y}_{enemies.Count}";
        Enemy minion = AddEnemy(id, boss.Group, boss.AlertGroup, BranchChoice.None, archetype, level, hp, 0f, 0f, spawnCell, spawnCell);
        if (Application.isPlaying || playerView != null)
            CreateEnemyView(minion, enemies.Count - 1);

        Vector2Int playerCell = PlayerCell();
        Vector2Int bossCell = WorldToCell(boss.Position);
        if (farSpawn)
        {
            minion.Patrol.Clear();
            minion.Patrol.Add(spawnCell);
            minion.Patrol.Add(bossCell);
            minion.Patrol.Add(playerCell);
            minion.PatrolIndex = 1;
            minion.Mode = EnemyMode.Patrol;
            minion.LastSeen = playerCell;
        }
        else if (CanEnemyReachCell(minion, playerCell))
        {
            minion.Mode = EnemyMode.Hunt;
            minion.LastSeen = playerCell;
        }
        else
        {
            minion.Mode = EnemyMode.Investigate;
            minion.LastSeen = bossCell;
        }
        if (minion.Mode == EnemyMode.Patrol)
        {
            minion.SearchTimer = 0f;
            minion.SearchPoints.Clear();
        }
        else
        {
            minion.SearchTimer = EnemySearchDurationFor(minion);
            BuildEnemySearchPoints(minion, minion.LastSeen);
        }
        boss.SummonCooldown = BossSummonCooldown;
        SpawnBossSummonEffect(boss.Position, ToWorld(spawnCell));
        SpawnHitBurst(ToWorld(spawnCell), false);
        SpawnBossFlash(boss.Position, 2.6f, 3.2f);
        return true;
    }

    private void SpawnBossSummonEffect(Vector2 bossPosition, Vector2 spawnPosition)
    {
        Sprite sprite = bossSummonSprite != null ? bossSummonSprite : bossTelegraphSprite;
        CombatEffect channel = CreateCombatEffect(
            "Boss Summon Channel",
            bossPosition,
            Vector3.one * 1.05f,
            new Color(1f, 0.20f, 0.12f, 0.74f),
            0.52f,
            27,
            sprite);
        channel.EndScale = Vector3.one * 1.82f;
        channel.RotationSpeed = -180f;

        CombatEffect arrival = CreateCombatEffect(
            "Boss Summon Arrival",
            spawnPosition,
            Vector3.one * 0.86f,
            new Color(1f, 0.32f, 0.16f, 0.70f),
            0.42f,
            27,
            sprite);
        arrival.EndScale = Vector3.one * 1.36f;
        arrival.RotationSpeed = 240f;
    }

    private EnemyArchetype PickBossMinionArchetype()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.45f)
            return EnemyArchetype.Patrol;
        if (roll < 0.75f)
            return EnemyArchetype.Hunter;
        if (roll < 0.90f)
            return EnemyArchetype.Brute;
        return EnemyArchetype.Caller;
    }

    private bool TryPickBossSummonSpawn(Enemy boss, out Vector2Int spawnCell, out bool farSpawn)
    {
        bool preferFar = UnityEngine.Random.value < 0.68f;
        if (preferFar && TryPickFarBossSummonSpawn(boss, out spawnCell))
        {
            farSpawn = true;
            return true;
        }
        if (TryPickNearBossSummonSpawn(boss, out spawnCell))
        {
            farSpawn = false;
            return true;
        }
        if (!preferFar && TryPickFarBossSummonSpawn(boss, out spawnCell))
        {
            farSpawn = true;
            return true;
        }

        spawnCell = WorldToCell(boss.Position);
        farSpawn = false;
        return false;
    }

    private bool TryPickNearBossSummonSpawn(Enemy boss, out Vector2Int spawnCell)
    {
        Vector2Int bossCell = WorldToCell(boss.Position);
        Vector2Int[] candidates =
        {
            bossCell + Vector2Int.up,
            bossCell + Vector2Int.right,
            bossCell + Vector2Int.down,
            bossCell + Vector2Int.left,
            bossCell + new Vector2Int(1, 1),
            bossCell + new Vector2Int(-1, 1),
            bossCell + new Vector2Int(1, -1),
            bossCell + new Vector2Int(-1, -1),
        };
        Shuffle(candidates);
        foreach (Vector2Int candidate in candidates)
        {
            if (BossSummonCellPassable(candidate, boss))
            {
                spawnCell = candidate;
                return true;
            }
        }

        spawnCell = bossCell;
        return false;
    }

    private bool TryPickFarBossSummonSpawn(Enemy boss, out Vector2Int spawnCell)
    {
        Vector2Int playerCell = PlayerCell();
        Vector2Int bossCell = WorldToCell(boss.Position);
        for (int attempt = 0; attempt < 48; attempt++)
        {
            Vector2Int candidate = new Vector2Int(UnityEngine.Random.Range(1, Width - 1), UnityEngine.Random.Range(1, Height - 1));
            if (Manhattan(candidate, playerCell) < 8 || Manhattan(candidate, bossCell) < 3)
                continue;
            if (!BossSummonCellPassable(candidate, boss))
                continue;

            spawnCell = candidate;
            return true;
        }

        spawnCell = bossCell;
        return false;
    }

    private bool BossSummonCellPassable(Vector2Int cell, Enemy boss)
    {
        if (!Inside(cell) || IsSolidCell(cell) || StoneAt(cell) != null || EnemyAt(cell) != null || cell == PlayerCell())
            return false;

        Vector2Int bossCell = WorldToCell(boss.Position);
        return TryFindNextPathCell(cell, PlayerCell(), out _) || TryFindNextPathCell(cell, bossCell, out _);
    }

    private static void Shuffle(Vector2Int[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private void SpawnBossFlash(Vector2 position, float intensity, float radius)
    {
        GameObject view = new GameObject("Boss Attack Flash");
        view.transform.SetParent(EnsureCombatVfxRoot());
        view.transform.position = position;

        var effect = new CombatEffect
        {
            View = view,
            StartScale = Vector3.one,
            EndScale = Vector3.one,
            Duration = 0.22f,
        };
        effect.Light = Urp2DLighting.AddPointLight(view, new Color(1f, 0.28f, 0.18f), intensity, radius, 0.40f);
        effect.LightStartIntensity = effect.Light.intensity;
        Urp2DLighting.ConfigurePointLightShadows(effect.Light, 0.18f, 0.35f, 0.58f);
        combatEffects.Add(effect);
    }

    private void SpawnBossDeathEffect(Vector2 position)
    {
        CombatEffect effect = CreateCombatEffect(
            "Boss Collapse",
            position,
            Vector3.one * 1.85f,
            new Color(1f, 0.74f, 0.66f, 0.86f),
            2.2f,
            27,
            bossDeathSprite);
        effect.EndScale = Vector3.one * 2.18f;
        SpawnEnemyDeathFlash(position);
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

    private CombatEffect CreateCombatEffect(string name, Vector2 position, Vector3 scale, Color color, float duration, int sortingOrder, Sprite sprite = null)
    {
        GameObject view = new GameObject(name);
        view.transform.SetParent(EnsureCombatVfxRoot());
        view.transform.position = position;
        view.transform.localScale = scale;

        SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : EnsureEffectSprite();
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
            if (!effect.DamageApplied && effect.DamageAmount > 0 && effect.Age >= effect.DamageDelay)
            {
                effect.DamageApplied = true;
                if (playerView != null && Vector2.Distance(effect.View.transform.position, playerView.transform.position) <= effect.DamageRadius)
                    DamagePlayer(effect.DamageAmount, string.IsNullOrEmpty(effect.DamageMessage) ? "Статика босса пробивает экран." : effect.DamageMessage);
                SpawnBossFlash(effect.View.transform.position, 2.2f, 2.1f);
            }
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

            DestroyRuntimeObject(effect.View);
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
                DestroyRuntimeObject(combatEffects[i].View);
        }
        combatEffects.Clear();

        if (combatVfxRoot != null)
        {
            DestroyRuntimeObject(combatVfxRoot.gameObject);
            combatVfxRoot = null;
        }

        GameObject staleRoot = FindSceneObjectIncludingInactive("Combat VFX");
        if (staleRoot != null)
            DestroyRuntimeObject(staleRoot);
    }

    private bool RemoteJamActive()
    {
        return remoteJamTimer > 0f;
    }

    private bool RemoteBossChaseActive()
    {
        if (!RemoteJamActive())
            return false;

        foreach (Enemy enemy in enemies)
        {
            if (enemy.Archetype == EnemyArchetype.Boss && enemy.Mode == EnemyMode.Hunt)
                return true;
        }

        return false;
    }

    private bool EnemyBlockedByRemote(Enemy enemy)
    {
        return RemoteJamActive() && (enemy == null || enemy.Archetype != EnemyArchetype.Boss);
    }

    private void UpdateEnemyState(Enemy enemy, float dt)
    {
        Vector2Int enemyCell = WorldToCell(enemy.Position);
        Vector2Int playerCell = PlayerCell();
        if (CanSeePlayer(enemy) && CanEnemyReachCell(enemy, playerCell))
        {
            bool newlyAlerted = enemy.Mode != EnemyMode.Hunt;
            enemy.Mode = EnemyMode.Hunt;
            enemy.LastSeen = playerCell;
            enemy.LostSightTimer = 0f;
            enemy.SearchTimer = EnemySearchDurationFor(enemy);
            BuildEnemySearchPoints(enemy, enemy.LastSeen);
            if (newlyAlerted || enemy.Archetype == EnemyArchetype.Caller)
                TryAlertEnemies(enemy, enemy.LastSeen, enemy.Archetype == EnemyArchetype.Caller);
            return;
        }

        if (enemy.Mode == EnemyMode.Hunt)
        {
            if (!CanEnemyReachCell(enemy, playerCell))
            {
                ResetEnemyAggro(enemy);
                return;
            }

            enemy.LostSightTimer += dt;
            float playerDistance = playerView != null ? Vector2.Distance(enemy.Position, playerView.transform.position) : float.PositiveInfinity;
            if (playerDistance > EnemyAggroResetDistance && enemy.LostSightTimer >= EnemyAggroResetDelay)
            {
                enemy.Mode = EnemyMode.Investigate;
                enemy.SearchTimer = EnemySearchDurationFor(enemy);
                BuildEnemySearchPoints(enemy, enemy.LastSeen);
                CancelEnemyAttack(enemy);
            }
        }

        if (lastNoisePower > 0 && EnemyCanHearNoise(enemy, enemyCell, lastNoiseCell, lastNoisePower))
        {
            if (enemy.Mode != EnemyMode.Hunt)
                enemy.Mode = EnemyMode.Investigate;
            enemy.LastSeen = lastNoiseCell;
            enemy.SearchTimer = EnemySearchDurationFor(enemy);
            BuildEnemySearchPoints(enemy, lastNoiseCell);
            enemy.LostSightTimer = 0f;
            return;
        }

        if (enemy.Mode == EnemyMode.Investigate && ReachedEnemySearchPoint(enemy))
        {
            enemy.SearchIndex++;
            if (enemy.SearchIndex < enemy.SearchPoints.Count)
            {
                enemy.LastSeen = enemy.SearchPoints[enemy.SearchIndex];
                return;
            }
        }

        if (enemy.Mode != EnemyMode.Patrol && enemy.SearchTimer <= 0f && Vector2.Distance(enemy.Position, ToWorld(enemy.LastSeen)) <= 0.16f)
        {
            enemy.Mode = EnemyMode.Patrol;
            enemy.LostSightTimer = 0f;
            enemy.SearchPoints.Clear();
            enemy.SearchIndex = 0;
        }
    }

    private void ResetEnemyAggro(Enemy enemy)
    {
        enemy.Mode = EnemyMode.Patrol;
        enemy.LostSightTimer = 0f;
        enemy.SearchTimer = 0f;
        enemy.SearchPoints.Clear();
        enemy.SearchIndex = 0;
        CancelEnemyAttack(enemy);
    }

    private Vector2 ChooseEnemyTarget(Enemy enemy)
    {
        if (enemy.Mode == EnemyMode.Hunt)
        {
            if (playerView != null && enemy.LostSightTimer <= EnemyDirectChaseGrace)
                return EnemyEngagementTarget(enemy, playerView.transform.position);

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

    private bool EnemyCanHearNoise(Enemy enemy, Vector2Int enemyCell, Vector2Int noiseCell, int power)
    {
        if (CountSightBlockersBetween(enemyCell, noiseCell) >= 3)
            return false;
        if (!CanEnemyReachCell(enemy, noiseCell))
            return false;

        float hearing = enemy.HearingOverride > 0f ? enemy.HearingOverride : EnemyHearingScale(enemy);
        float effectivePower = power * hearing;
        return Manhattan(enemyCell, noiseCell) <= Mathf.CeilToInt(effectivePower);
    }

    private static float EnemyHearingScale(Enemy enemy)
    {
        EnemyArchetype kind = enemy == null ? EnemyArchetype.Patrol : enemy.Archetype;
        return kind switch
        {
            EnemyArchetype.Hunter => 1.28f,
            EnemyArchetype.Caller => 1.18f,
            EnemyArchetype.Brute => 0.88f,
            EnemyArchetype.Boss => 1.22f,
            _ => 1f,
        };
    }

    private static float EnemyVisionScale(Enemy enemy)
    {
        if (enemy != null && enemy.VisionOverride > 0f)
            return enemy.VisionOverride;

        EnemyArchetype kind = enemy == null ? EnemyArchetype.Patrol : enemy.Archetype;
        return kind switch
        {
            EnemyArchetype.Hunter => 1.18f,
            EnemyArchetype.Caller => 1.08f,
            EnemyArchetype.Brute => 0.92f,
            EnemyArchetype.Boss => 1.24f,
            _ => 1f,
        };
    }

    private static float EnemySearchDurationFor(Enemy enemy)
    {
        EnemyArchetype kind = enemy == null ? EnemyArchetype.Patrol : enemy.Archetype;
        float archetype = kind switch
        {
            EnemyArchetype.Hunter => 1.35f,
            EnemyArchetype.Caller => 1.20f,
            EnemyArchetype.Brute => 0.90f,
            EnemyArchetype.Boss => 1.50f,
            _ => 1f,
        };
        return Mathf.Clamp(EnemyBaseSearchDuration * archetype + EnemyLevelOffset(enemy) * 0.12f, 1.8f, 6.0f);
    }

    private void BuildEnemySearchPoints(Enemy enemy, Vector2Int origin)
    {
        enemy.SearchPoints.Clear();
        enemy.SearchIndex = 0;
        if (Inside(origin) && !IsSolidCell(origin))
            enemy.SearchPoints.Add(origin);

        Vector2Int[] candidates =
        {
            origin + Vector2Int.up,
            origin + Vector2Int.right,
            origin + Vector2Int.down,
            origin + Vector2Int.left,
            origin + new Vector2Int(1, 1),
            origin + new Vector2Int(-1, 1),
        };

        int limit = enemy.Archetype == EnemyArchetype.Hunter ? 4 : 3;
        foreach (Vector2Int candidate in candidates)
        {
            if (enemy.SearchPoints.Count >= limit)
                break;
            if (!Inside(candidate) || IsSolidCell(candidate) || StoneAt(candidate) != null)
                continue;
            if (!enemy.SearchPoints.Contains(candidate))
                enemy.SearchPoints.Add(candidate);
        }
    }

    private bool ReachedEnemySearchPoint(Enemy enemy)
    {
        if (enemy.SearchPoints.Count == 0 || enemy.SearchIndex >= enemy.SearchPoints.Count)
            return Vector2.Distance(enemy.Position, ToWorld(enemy.LastSeen)) <= 0.14f;

        return Vector2.Distance(enemy.Position, ToWorld(enemy.SearchPoints[enemy.SearchIndex])) <= 0.14f;
    }

    private void TryAlertEnemies(Enemy source, Vector2Int alertCell, bool callerPulse)
    {
        if (source == null)
            return;
        if (source.AlertTimer > 0f && !callerPulse)
            return;
        if (callerPulse && source.CallHelpCooldown > 0f)
            return;

        source.AlertTimer = 1.1f;
        if (callerPulse)
            source.CallHelpCooldown = EnemyCallHelpCooldown;

        string sourceGroup = !string.IsNullOrWhiteSpace(source.AlertGroup) ? source.AlertGroup : source.Group;
        float radius = callerPulse ? EnemyAlertRadius * 1.45f : EnemyAlertRadius;
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy == source || enemy.View == null)
                continue;

            bool sameGroup = !string.IsNullOrWhiteSpace(sourceGroup) && (enemy.Group == sourceGroup || enemy.AlertGroup == sourceGroup);
            bool nearby = Vector2.Distance(enemy.Position, source.Position) <= radius;
            if (!sameGroup && !nearby)
                continue;
            if (!CanEnemyReachCell(enemy, alertCell))
                continue;

            if (enemy.Mode != EnemyMode.Hunt)
                enemy.Mode = callerPulse && sameGroup && enemy.Archetype == EnemyArchetype.Hunter ? EnemyMode.Hunt : EnemyMode.Investigate;
            enemy.LastSeen = alertCell;
            enemy.SearchTimer = EnemySearchDurationFor(enemy);
            enemy.LostSightTimer = 0f;
            BuildEnemySearchPoints(enemy, alertCell);
        }
    }

    private void AlertEnemiesAround(Vector2Int alertCell, Vector2 position, float radius)
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.View == null)
                continue;
            if (Vector2.Distance(enemy.Position, position) > radius)
                continue;
            if (!CanEnemyReachCell(enemy, alertCell))
                continue;

            enemy.Mode = enemy.Archetype == EnemyArchetype.Hunter ? EnemyMode.Hunt : EnemyMode.Investigate;
            enemy.LastSeen = alertCell;
            enemy.SearchTimer = EnemySearchDurationFor(enemy);
            enemy.LostSightTimer = 0f;
            BuildEnemySearchPoints(enemy, alertCell);
        }
    }

    private Vector2 EnemyEngagementTarget(Enemy enemy, Vector2 playerPosition)
    {
        if (enemy.Archetype == EnemyArchetype.Caller && Vector2.Distance(enemy.Position, playerPosition) < EnemyAttackRangeFor(enemy) * 1.6f)
            return enemy.Position;
        float playerDistance = Vector2.Distance(enemy.Position, playerPosition);
        if (playerDistance <= EnemyAttackRangeFor(enemy) * 0.92f)
            return playerPosition;

        Vector2Int playerCell = WorldToCell(playerPosition);
        Vector2Int currentCell = WorldToCell(enemy.Position);
        Vector2Int bestCell = playerCell;
        float bestScore = float.PositiveInfinity;
        foreach (Vector2Int candidate in CardinalCells(playerCell))
        {
            if (!EnemyPathPassable(candidate, playerCell))
                continue;
            Enemy occupant = EnemyAt(candidate);
            if (occupant != null && occupant != enemy)
                continue;

            float score = Vector2.Distance(enemy.Position, ToWorld(candidate));
            if (enemy.Archetype == EnemyArchetype.Hunter)
            {
                Vector2 fromPlayer = DirectionOrFallback((Vector2)(candidate - playerCell), Vector2.down);
                score += Mathf.Abs(Vector2.Dot(fromPlayer, DirectionOrFallback(lastAim, Vector2.down))) * 0.55f;
            }
            if (candidate == currentCell && playerDistance <= EnemyAttackRangeFor(enemy))
                score -= 0.45f;

            if (score < bestScore)
            {
                bestScore = score;
                bestCell = candidate;
            }
        }

        if (bestCell != playerCell)
        {
            if (bestCell == currentCell && playerDistance > EnemyAttackRangeFor(enemy) * 0.92f)
                return playerPosition;
            if (enemy.Archetype == EnemyArchetype.Hunter && enemy.FlankCooldown <= 0f)
                enemy.FlankCooldown = EnemyFlankCooldown;
            return ToWorld(bestCell);
        }

        return playerPosition;
    }

    private Vector2 EnemySteeringTarget(Enemy enemy, Vector2 directTarget)
    {
        Vector2Int from = WorldToCell(enemy.Position);
        Vector2Int to = WorldToCell(directTarget);
        if (from == to || HasStraightWalkLine(from, to))
            return ApplyEnemySeparation(enemy, directTarget);

        if (TryFindNextPathCell(from, to, out Vector2Int nextCell))
            return ApplyEnemySeparation(enemy, ToWorld(nextCell));

        return ApplyEnemySeparation(enemy, enemy.Position);
    }

    private Vector2 ApplyEnemySeparation(Enemy self, Vector2 target)
    {
        Vector2 separation = Vector2.zero;
        foreach (Enemy other in enemies)
        {
            if (other == self)
                continue;

            Vector2 away = self.Position - other.Position;
            float distance = away.magnitude;
            if (distance <= 0.001f || distance >= EnemySeparationRadius)
                continue;

            separation += away.normalized * ((EnemySeparationRadius - distance) / EnemySeparationRadius);
        }

        if (separation.sqrMagnitude <= 0.001f)
            return target;

        return target + separation.normalized * EnemySeparationStrength;
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
        if (start == goal)
        {
            nextCell = start;
            return true;
        }

        return pathfinder.TryFindNextPathCell(start, goal, EnemyPathPassable, out nextCell);
    }

    private bool CanEnemyReachCell(Enemy enemy, Vector2Int target)
    {
        if (enemy == null || !Inside(target))
            return false;

        Vector2Int start = WorldToCell(enemy.Position);
        return TryFindNextPathCell(start, target, out _);
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
        if (self != null && self.Archetype == EnemyArchetype.Boss && !BossFootprintPassable(position, self))
            return false;

        return true;
    }

    private bool BossFootprintPassable(Vector2 position, Enemy self)
    {
        return BossFootprintSamplePassable(position, self, BossCollisionRadius, 0f) &&
               BossFootprintSamplePassable(position, self, -BossCollisionRadius, 0f) &&
               BossFootprintSamplePassable(position, self, 0f, BossCollisionRadius) &&
               BossFootprintSamplePassable(position, self, 0f, -BossCollisionRadius) &&
               BossFootprintSamplePassable(position, self, BossCollisionRadius, BossCollisionRadius) &&
               BossFootprintSamplePassable(position, self, -BossCollisionRadius, BossCollisionRadius) &&
               BossFootprintSamplePassable(position, self, BossCollisionRadius, -BossCollisionRadius) &&
               BossFootprintSamplePassable(position, self, -BossCollisionRadius, -BossCollisionRadius);
    }

    private bool BossFootprintSamplePassable(Vector2 position, Enemy self, float x, float y)
    {
        return CanEnemyEnterCell(WorldToCell(position + new Vector2(x, y)), self);
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
        float vision = EnemyVisionScale(enemy);
        if (distance > EnemyForwardSightRange * vision)
            return false;

        Vector2Int enemyCell = WorldToCell(enemy.Position);
        Vector2Int playerCell = PlayerCell();
        if (!HasSightLine(enemyCell, playerCell))
            return false;

        if (distance <= EnemyCloseDetectionRange * Mathf.Lerp(1f, vision, 0.55f))
            return true;

        Vector2 look = DirectionOrFallback(enemy.LookDirection, Vector2.down);
        float forward = Vector2.Dot(toPlayer, look);
        Vector2 sideAxis = new Vector2(-look.y, look.x);
        float side = Mathf.Abs(Vector2.Dot(toPlayer, sideAxis));
        float forwardRange = (forward >= 0f ? EnemyForwardSightRange : EnemyBackSightRange) * vision;
        float sideRatio = side / (EnemySideSightRange * Mathf.Lerp(1f, vision, 0.65f));
        float forwardRatio = forward / forwardRange;
        return forwardRatio * forwardRatio + sideRatio * sideRatio <= 1f;
    }

    private bool HasSightLine(Vector2Int from, Vector2Int to)
    {
        return CountSightBlockersBetween(from, to) == 0;
    }

    private int CountSightBlockersBetween(Vector2Int from, Vector2Int to)
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
        int blockers = 0;

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
                blockers++;
        }

        return blockers;
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

    private bool CanStoneEnter(Vector2Int cell, Vector2Int playerCell)
    {
        return cell != playerCell && Inside(cell) && !IsSolidCell(cell) && StoneAt(cell) == null && EnemyAt(cell) == null;
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
        if (EndlessRunState.Enabled)
            return GenerateEndlessLevelDefinition();

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

    private string StoryStartLevelId()
    {
        string overrideLevel = LevelAssetResolver.NormalizeLevelId(EndlessRunState.StoryStartLevelId);
        if (!string.IsNullOrEmpty(overrideLevel))
            return overrideLevel;

        return LevelAssetResolver.NormalizeLevelId(string.IsNullOrEmpty(StartingLevelId) ? LevelAsset?.name : StartingLevelId);
    }

    private bool StoryStartLevelOverrideActive()
    {
        return !EndlessRunState.Enabled && !string.IsNullOrEmpty(LevelAssetResolver.NormalizeLevelId(EndlessRunState.StoryStartLevelId));
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
            "flashlight" => Tile.Flashlight,
            "story" => Tile.Story,
            "storyImage" => Tile.Story,
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
            case "flashlight":
                tiles[cell.x, cell.y] = Tile.Flashlight;
                tileVariants[cell.x, cell.y] = obj.variant;
                break;
            case "trap":
                tiles[cell.x, cell.y] = Tile.Trap;
                tileVariants[cell.x, cell.y] = obj.variant;
                if (obj.direction != null)
                    cameraDirectionsByCell[cell] = DirectionOrFallback(obj.direction.ToVector2(), Vector2.down);
                break;
            case "story":
            case "storyImage":
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
        EnemyArchetype archetype = ParseEnemyArchetype(data.type);
        int level = Mathf.Clamp(data.level <= 0 ? EnemyBaseLevel : data.level, EnemyMinLevel, EnemyMaxLevel);
        int hp = Mathf.Max(1, data.hp <= 0 ? 2 : data.hp);
        if (archetype == EnemyArchetype.Brute)
            hp += 2;
        else if (archetype == EnemyArchetype.Caller)
            hp = Mathf.Max(1, hp - 1);
        else if (archetype == EnemyArchetype.Boss)
            hp = Mathf.Max(hp, 14);

        AddEnemy(id, data.group, data.alertGroup, ParseBranch(data.branch), archetype, level, hp, data.hearing, data.vision, start, patrol.ToArray());
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

    private Enemy AddEnemy(string id, string group, string alertGroup, BranchChoice branch, EnemyArchetype archetype, int level, int hp, float hearing, float vision, Vector2Int start, params Vector2Int[] patrol)
    {
        var enemy = new Enemy
        {
            Id = id,
            Group = string.IsNullOrWhiteSpace(group) ? string.Empty : group.Trim(),
            AlertGroup = string.IsNullOrWhiteSpace(alertGroup) ? string.Empty : alertGroup.Trim(),
            Position = ToWorld(start),
            LastSeen = start,
            Mode = EnemyMode.Patrol,
            Branch = branch,
            Archetype = archetype,
            Level = level,
            Hp = hp,
            HearingOverride = Mathf.Max(0f, hearing),
            VisionOverride = Mathf.Max(0f, vision),
            LookDirection = InitialEnemyLookDirection(start, patrol),
        };
        enemy.Patrol.AddRange(patrol);
        enemies.Add(enemy);
        if (!string.IsNullOrEmpty(enemy.Group))
            activeEnemyGroups.Add(enemy.Group);
        return enemy;
    }

    private static EnemyArchetype ParseEnemyArchetype(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "":
            case "patrol":
            case "announcer":
                return EnemyArchetype.Patrol;
            case "hunter":
                return EnemyArchetype.Hunter;
            case "brute":
                return EnemyArchetype.Brute;
            case "caller":
                return EnemyArchetype.Caller;
            case "boss":
                return EnemyArchetype.Boss;
            default:
                return EnemyArchetype.Patrol;
        }
    }

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
