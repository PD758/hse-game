using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed partial class PrototypeGame
{
    private const string LevelVisualRootName = "Level Visuals";

    private void CreateViews()
    {
        ThrowIfPlayingBake("CreateViews");
        var tileRoot = new GameObject("Tiles");
        tileRoot.AddComponent<CompositeShadowCaster2D>().enabled = GameLightingSettings.ShadowsEnabled;
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
        CreateLevelVisualViews();
        RebuildTileColliders();
    }

    private void CreateEntityViews()
    {
        if (playerView == null)
        {
            playerView = new GameObject("Player");
            playerView.transform.position = ToWorld(playerStart);
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
            playerView.transform.position = ToWorld(playerStart);
        }

        foreach (Stone stone in stones)
            CreateStoneView(stone);

        for (int i = 0; i < enemies.Count; i++)
            CreateEnemyView(enemies[i], i);
    }

    private void CreateEnemyView(Enemy enemy, int index)
    {
        enemy.View = new GameObject($"Enemy {index}");
        enemy.View.transform.position = enemy.Position;
        var renderer = enemy.View.AddComponent<SpriteRenderer>();
        renderer.sprite = enemySprite;
        SetLitMaterial(renderer);
        renderer.sortingOrder = 15;
        enemy.Light = EnsureEnemyLight(enemy.View);
        enemy.BeamRenderer = EnsureEnemyBeam(enemy.View);
        ConfigureEnemyLight(enemy);
    }

    private void CreateStoneView(Stone stone)
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
        Urp2DLighting.AddShadowCaster(view);
        stone.View = view;
    }

    private void CreateLighting()
    {
        ThrowIfPlayingBake("CreateLighting");
        Urp2DLighting.AddGlobalLight(gameObject, new Color(0.50f, 0.55f, 0.62f), 0.42f);

        var channelLightObject = new GameObject("Channel Light");
        channelLightObject.transform.SetParent(transform);
        channelLightObject.transform.position = new Vector3(14f, 10f, 0f);
        Light2D channelLight = Urp2DLighting.AddPointLight(channelLightObject, new Color(0.58f, 0.74f, 0.94f), 0.20f, 7.0f, 1.4f);
        Urp2DLighting.ConfigurePointLightShadows(channelLight, 0.30f, 0.42f, 0.58f);

        var playerLightObject = new GameObject("Player Light");
        playerLightObject.transform.SetParent(playerView != null ? playerView.transform : transform);
        playerLightObject.transform.localPosition = Vector3.zero;
        playerLight = Urp2DLighting.AddPointLight(playerLightObject, new Color(0.82f, 0.94f, 1.00f), 0.44f, 2.5f, 0.55f);
        Urp2DLighting.ConfigurePointLightShadows(playerLight, 0.36f, 0.52f, 0.64f);
    }

    private bool BindSceneViews()
    {
        Transform tileRoot = FindSceneObjectIncludingInactive("Tiles")?.transform;
        if (tileRoot == null)
            return false;
        if (!tileRoot.TryGetComponent(out CompositeShadowCaster2D compositeShadowCaster))
            compositeShadowCaster = tileRoot.gameObject.AddComponent<CompositeShadowCaster2D>();
        compositeShadowCaster.enabled = GameLightingSettings.ShadowsEnabled;

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

        playerView = FindSceneObjectIncludingInactive("Player");
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

        playerView.transform.position = ToWorld(playerStart);
        playerBody.linearVelocity = Vector2.zero;

        if (EndlessRunState.Enabled)
        {
            DestroySceneObjectsWithPrefix("Enemy ");
            DestroySceneObjectsWithPrefix("Signal Blocker ");
            foreach (Stone stone in stones)
                CreateStoneView(stone);
            for (int i = 0; i < enemies.Count; i++)
                CreateEnemyView(enemies[i], i);
            CreateLevelVisualViews();
            return true;
        }

        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            stone.View = FindSceneObjectIncludingInactive($"Signal Blocker {stone.Cell.x},{stone.Cell.y}");
            if (stone.View == null)
                return false;

            stone.View.transform.position = ToWorld(stone.Cell);
            stone.View.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
            Urp2DLighting.AddShadowCaster(stone.View);
            stone.View.SetActive(true);
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            enemy.View = FindSceneObjectIncludingInactive($"Enemy {i}");
            if (enemy.View == null)
                return false;

            enemy.View.transform.position = enemy.Position;
            enemy.View.transform.localScale = Vector3.one;
            enemy.Light = EnsureEnemyLight(enemy.View);
            enemy.BeamRenderer = EnsureEnemyBeam(enemy.View);
            ConfigureEnemyLight(enemy);
            enemy.View.SetActive(true);
        }

        CreateLevelVisualViews();
        return true;
    }

    private void DestroySceneObjectsWithPrefix(string namePrefix)
    {
        foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (obj == null || obj.scene != gameObject.scene || !obj.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                continue;

            obj.SetActive(false);
            DestroyRuntimeObject(obj);
        }
    }

    private void EnsureGameplayLighting()
    {
        bool hasGlobalLight = false;
        foreach (Light2D light in GetComponents<Light2D>())
        {
            if (light.lightType == Light2D.LightType.Global)
            {
                light.color = new Color(0.50f, 0.55f, 0.62f);
                light.intensity = GameLightingSettings.GameplayGlobalIntensity(0.42f);
                hasGlobalLight = true;
            }
        }
        if (!hasGlobalLight)
            Urp2DLighting.AddGlobalLight(gameObject, new Color(0.50f, 0.55f, 0.62f), 0.42f);

        GameObject channelLightObject = FindSceneObjectIncludingInactive("Channel Light");
        if (channelLightObject == null)
        {
            channelLightObject = new GameObject("Channel Light");
            channelLightObject.transform.SetParent(transform);
        }
        channelLightObject.SetActive(true);
        channelLightObject.transform.position = new Vector3(14f, 10f, 0f);
        Light2D channelLight = channelLightObject.GetComponent<Light2D>() ??
                               Urp2DLighting.AddPointLight(channelLightObject, new Color(0.58f, 0.74f, 0.94f), 0.20f, 7.0f, 1.4f);
        channelLight.lightType = Light2D.LightType.Point;
        channelLight.color = new Color(0.58f, 0.74f, 0.94f);
        channelLight.intensity = 0.20f;
        channelLight.pointLightOuterRadius = 7.0f;
        channelLight.pointLightInnerRadius = 1.4f;
        Urp2DLighting.ConfigurePointLightShadows(channelLight, 0.30f, 0.42f, 0.58f);

        GameObject playerLightObject = FindSceneObjectIncludingInactive("Player Light");
        if (playerLightObject == null)
        {
            playerLightObject = new GameObject("Player Light");
        }
        playerLightObject.SetActive(true);
        if (playerView != null)
            playerLightObject.transform.SetParent(playerView.transform);
        playerLightObject.transform.localPosition = Vector3.zero;
        playerLight = playerLightObject.GetComponent<Light2D>() ??
                      Urp2DLighting.AddPointLight(playerLightObject, new Color(0.82f, 0.94f, 1.00f), 0.44f, 2.5f, 0.55f);
        UpdatePlayerLight();
    }

    private void UpdatePlayerLight()
    {
        if (playerLight == null)
            return;

        if (HasFlashlight)
        {
            Vector2 direction = DirectionOrFallback(lastAim, Vector2.right);
            Urp2DLighting.ConfigureConeLight(playerLight, new Color(1.00f, 0.94f, 0.72f), FlashlightIntensity, FlashlightRadius, 0.45f, FlashlightOuterAngle, FlashlightInnerAngle, direction);
            Urp2DLighting.ConfigurePointLightShadows(playerLight, 0.46f, 0.46f, 0.66f);
            return;
        }

        playerLight.lightType = Light2D.LightType.Point;
        playerLight.color = new Color(0.82f, 0.94f, 1.00f);
        playerLight.intensity = 0.44f;
        playerLight.pointLightOuterRadius = 2.5f;
        playerLight.pointLightInnerRadius = 0.55f;
        playerLight.pointLightOuterAngle = 360f;
        playerLight.pointLightInnerAngle = 360f;
        Urp2DLighting.ConfigurePointLightShadows(playerLight, 0.36f, 0.52f, 0.64f);
    }

    private GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        foreach (GameObject obj in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (obj.scene == gameObject.scene && obj.name == objectName)
                return obj;
        }

        return null;
    }

    private static GameObject FindChildObject(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        return child == null ? null : child.gameObject;
    }

    private void CreateLevelVisualViews()
    {
        GameObject existingRoot = FindSceneObjectIncludingInactive(LevelVisualRootName);
        if (existingRoot != null)
            DestroyRuntimeObject(existingRoot);
        levelVisualObjects.Clear();

        if ((levelDecorations == null || levelDecorations.Count == 0) && (levelLights == null || levelLights.Count == 0))
            return;

        var root = new GameObject(LevelVisualRootName);
        root.transform.SetParent(transform);
        levelVisualObjects.Add(root);

        if (levelDecorations != null)
        {
            for (int i = 0; i < levelDecorations.Count; i++)
                CreateLevelDecorationView(root.transform, levelDecorations[i], i);
        }

        if (levelLights != null)
        {
            for (int i = 0; i < levelLights.Count; i++)
                CreateLevelLightView(root.transform, levelLights[i], i);
        }
    }

    private void CreateLevelDecorationView(Transform root, LevelDecoration data, int index)
    {
        if (data == null)
            return;

        var view = new GameObject(string.IsNullOrWhiteSpace(data.id) ? $"Texture {index}" : $"Texture {data.id}");
        view.transform.SetParent(root);
        view.transform.position = ToWorld(data.x, data.y);
        view.transform.localRotation = Quaternion.Euler(0f, 0f, data.rotation);
        float scale = Mathf.Clamp(data.scale <= 0f ? 1f : data.scale, 0.05f, 12f);
        view.transform.localScale = new Vector3(scale, scale, 1f);

        var renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadLevelSprite(data.texturePath);
        renderer.sortingOrder = data.sortingOrder;
        SetLitMaterial(renderer);

        if (data.castsShadow)
            Urp2DLighting.AddShadowCaster(view);
    }

    private void CreateLevelLightView(Transform root, LevelLight data, int index)
    {
        if (data == null)
            return;

        var view = new GameObject(string.IsNullOrWhiteSpace(data.id) ? $"Light {index}" : $"Light {data.id}");
        view.transform.SetParent(root);
        view.transform.position = ToWorld(data.x, data.y);

        Color color = ParseLevelColor(data.color, new Color(0.84f, 0.94f, 1f));
        float intensity = Mathf.Max(0.01f, data.intensity);
        float radius = Mathf.Max(0.1f, data.radius);
        if (string.Equals(data.type, "cone", System.StringComparison.OrdinalIgnoreCase))
        {
            Vector2 direction = new Vector2(Mathf.Cos(data.rotation * Mathf.Deg2Rad), Mathf.Sin(data.rotation * Mathf.Deg2Rad));
            Light2D light = Urp2DLighting.AddConeLight(view, color, intensity, radius, 0.2f, Mathf.Clamp(data.outerAngle, 1f, 360f), Mathf.Clamp(data.innerAngle, 0f, 360f), direction);
            Urp2DLighting.ConfigurePointLightShadows(light, Mathf.Min(0.65f, intensity * 0.45f), 0.55f, 0.70f);
        }
        else
        {
            Light2D light = Urp2DLighting.AddPointLight(view, color, intensity, radius, Mathf.Min(radius * 0.25f, 1.2f));
            Urp2DLighting.ConfigurePointLightShadows(light, Mathf.Min(0.65f, intensity * 0.45f), 0.50f, 0.66f);
        }
    }

    private static Vector3 ToWorld(float x, float y)
    {
        return new Vector3(x * CellSize, y * CellSize, 0f);
    }

    private Sprite LoadLevelSprite(string texturePath)
    {
        string resourceKey = ResourceKeyForTexturePath(texturePath);
        if (!string.IsNullOrEmpty(resourceKey))
        {
            Sprite sprite = Resources.Load<Sprite>(resourceKey);
            if (sprite != null)
                return sprite;

            Texture2D texture = Resources.Load<Texture2D>(resourceKey);
            if (texture != null)
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
        }

        Debug.LogWarning($"Level texture '{texturePath}' was not found in Resources.");
        return null;
    }

    private static string ResourceKeyForTexturePath(string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
            return string.Empty;

        string normalized = texturePath.Replace('\\', '/').Trim();
        const string prefix = "Assets/Resources/";
        int index = normalized.IndexOf(prefix, System.StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return string.Empty;

        string key = normalized.Substring(index + prefix.Length);
        int extensionIndex = key.LastIndexOf('.');
        if (extensionIndex > 0)
            key = key.Substring(0, extensionIndex);
        return key;
    }

    private static Color ParseLevelColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        if (ColorUtility.TryParseHtmlString(value, out Color color))
            return color;
        return fallback;
    }

    private Light2D EnsureEnemyLight(GameObject enemyView)
    {
        if (enemyView == null)
            return null;

        GameObject lightObject = FindChildObject(enemyView.transform, EnemyLightName);
        if (lightObject == null)
        {
            lightObject = new GameObject(EnemyLightName);
            lightObject.transform.SetParent(enemyView.transform);
        }

        lightObject.SetActive(true);
        lightObject.transform.localPosition = Vector3.zero;
        Light2D light = lightObject.GetComponent<Light2D>();
        if (light == null)
            light = Urp2DLighting.AddConeLight(lightObject, Color.white, 0.42f, 4.2f, 0.35f, 110f, 78f, Vector2.down);
        return light;
    }

    private SpriteRenderer EnsureEnemyBeam(GameObject enemyView)
    {
        if (enemyView == null)
            return null;

        GameObject beamObject = FindChildObject(enemyView.transform, EnemyBeamName);
        if (beamObject == null)
        {
            beamObject = new GameObject(EnemyBeamName);
            beamObject.transform.SetParent(enemyView.transform);
        }

        beamObject.SetActive(true);
        beamObject.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = beamObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = beamObject.AddComponent<SpriteRenderer>();
        renderer.sprite = enemyBeamSprite;
        renderer.sortingOrder = 13;
        if (Urp2DLighting.SpriteUnlitMaterial != null)
            renderer.sharedMaterial = Urp2DLighting.SpriteUnlitMaterial;
        return renderer;
    }

    private void ConfigureEnemyLight(Enemy enemy)
    {
        if (enemy == null || enemy.View == null)
            return;

        if (enemy.Light == null)
            enemy.Light = EnsureEnemyLight(enemy.View);
        if (enemy.BeamRenderer == null)
            enemy.BeamRenderer = EnsureEnemyBeam(enemy.View);
        if (enemy.Light == null)
            return;

        bool attacking = enemy.AttackWindupTimer > 0f || enemy.AttackStrikeTimer > 0f;
        bool hunting = enemy.Mode == EnemyMode.Hunt || attacking;
        Vector2 direction = DirectionOrFallback(attacking ? enemy.AttackDirection : enemy.LookDirection, Vector2.down);
        Color color = hunting ? new Color(1f, 0.58f, 0.52f) : enemy.Mode == EnemyMode.Investigate ? new Color(1f, 0.82f, 0.52f) : new Color(0.82f, 0.92f, 1f);
        color = Color.Lerp(color, EnemyArchetypeTint(enemy.Archetype), EnemyArchetypeTintWeight(enemy.Archetype));
        float intensity = hunting ? 0.72f : enemy.Mode == EnemyMode.Investigate ? 0.55f : 0.42f;
        float radius = hunting ? 4.7f : enemy.Mode == EnemyMode.Investigate ? 4.4f : 4.1f;
        Color beamColor = hunting ? new Color(1f, 0.42f, 0.34f, 0.34f) : enemy.Mode == EnemyMode.Investigate ? new Color(1f, 0.78f, 0.38f, 0.26f) : new Color(0.72f, 0.90f, 1f, 0.20f);
        if (enemy.Archetype == EnemyArchetype.Hunter)
            radius *= 1.08f;
        else if (enemy.Archetype == EnemyArchetype.Brute)
            intensity *= 1.12f;
        else if (enemy.Archetype == EnemyArchetype.Caller)
            beamColor = Color.Lerp(beamColor, new Color(0.78f, 0.46f, 1f, beamColor.a), 0.55f);
        if (RemoteJamActive())
        {
            color = Color.Lerp(color, new Color(0.48f, 0.92f, 1f), 0.72f);
            intensity *= 0.36f;
            beamColor = new Color(0.46f, 0.94f, 1f, 0.13f);
        }

        float parentScale = Mathf.Max(0.001f, enemy.View.transform.localScale.x);
        Vector3 lightOffset = new Vector3(direction.x, direction.y, 0f) * (0.45f / parentScale);
        Vector3 beamOffset = new Vector3(direction.x, direction.y, 0f) * (0.68f / parentScale);
        enemy.Light.transform.localPosition = lightOffset;
        enemy.Light.transform.localScale = Vector3.one / parentScale;
        Urp2DLighting.ConfigureConeLight(enemy.Light, color, intensity, radius, 0.35f, 115f, 80f, direction);
        Urp2DLighting.ConfigurePointLightShadows(enemy.Light, hunting ? 0.32f : 0.20f, 0.62f, 0.70f);

        if (enemy.BeamRenderer == null)
            return;

        enemy.BeamRenderer.sprite = enemyBeamSprite;
        enemy.BeamRenderer.color = beamColor;
        enemy.BeamRenderer.transform.localPosition = beamOffset;
        enemy.BeamRenderer.transform.localScale = Vector3.one / parentScale;
        Urp2DLighting.RotateToward(enemy.BeamRenderer.transform, direction);
    }

    private Vector2 CameraDirectionForCell(Vector2Int cell)
    {
        if (cameraDirectionsByCell.TryGetValue(cell, out Vector2 direction))
            return direction;

        return Vector2.down;
    }

    private static Vector2 InitialEnemyLookDirection(Vector2Int start, Vector2Int[] patrol)
    {
        if (patrol != null && patrol.Length > 0)
            return DirectionOrFallback((Vector2)(patrol[0] - start), Vector2.down);

        return Vector2.down;
    }

    private static Vector2 DirectionOrFallback(Vector2 direction, Vector2 fallback)
    {
        if (direction.sqrMagnitude < 0.001f)
            direction = fallback;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.down;

        return direction.normalized;
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
               healSprite != null &&
               playerSprite != null &&
               stoneSprite != null &&
               enemySprite != null &&
               enemyInvestigateSprite != null &&
               enemyHuntSprite != null &&
               enemyBeamSprite != null &&
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
            UpdateEnemyVisual(enemy);
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

        ConfigureTileShadowCaster(cell);
        ConfigureCameraLight(cell);
    }

    private void ConfigureTileShadowCaster(Vector2Int cell)
    {
        GameObject view = tileViews[cell.x, cell.y];
        if (view == null)
            return;

        bool blocksLight = TileBlocksLight(cell);
        ShadowCaster2D caster = view.GetComponent<ShadowCaster2D>();
        if (caster == null && blocksLight)
            caster = Urp2DLighting.AddShadowCaster(view);
        else
            Urp2DLighting.ConfigureShadowCaster(caster, blocksLight);
    }

    private bool TileBlocksLight(Vector2Int cell)
    {
        Tile tile = tiles[cell.x, cell.y];
        return tile == Tile.Wall || tile == Tile.Rubble || (tile == Tile.Gate && !GateOpenForCell(cell));
    }

    private void ConfigureCameraLight(Vector2Int cell)
    {
        GameObject view = tileViews[cell.x, cell.y];
        if (view == null)
            return;

        GameObject lightObject = FindChildObject(view.transform, CameraLightName);
        if (tiles[cell.x, cell.y] != Tile.Trap)
        {
            if (lightObject != null)
                lightObject.SetActive(false);
            return;
        }

        if (lightObject == null)
        {
            lightObject = new GameObject(CameraLightName);
            lightObject.transform.SetParent(view.transform);
        }

        lightObject.SetActive(true);
        lightObject.transform.localPosition = Vector3.zero;
        Vector2 direction = CameraDirectionForCell(cell);
        Light2D light = lightObject.GetComponent<Light2D>() ??
                        Urp2DLighting.AddConeLight(lightObject, new Color(0.72f, 0.90f, 1f), 0.9f, 4.4f, 0.35f, 58f, 28f, direction);
        Urp2DLighting.ConfigureConeLight(light, new Color(0.72f, 0.90f, 1f), 0.9f, 4.4f, 0.35f, 58f, 28f, direction);
        Urp2DLighting.ConfigurePointLightShadows(light, 0.58f, 0.40f, 0.64f);
    }

    private void RebuildTileColliders()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                RedrawTile(new Vector2Int(x, y));
        }
    }

    private static Color EnemyColor(Enemy enemy)
    {
        Color modeColor = enemy.Mode switch
        {
            EnemyMode.Hunt => new Color(1.00f, 0.42f, 0.56f),
            EnemyMode.Investigate => new Color(1.00f, 0.78f, 0.36f),
            _ => Color.white,
        };
        return Color.Lerp(modeColor, EnemyArchetypeTint(enemy.Archetype), EnemyArchetypeTintWeight(enemy.Archetype));
    }

    private static Color EnemyArchetypeTint(EnemyArchetype archetype)
    {
        return archetype switch
        {
            EnemyArchetype.Hunter => new Color(0.70f, 0.92f, 1.00f),
            EnemyArchetype.Brute => new Color(1.00f, 0.34f, 0.26f),
            EnemyArchetype.Caller => new Color(0.88f, 0.58f, 1.00f),
            _ => Color.white,
        };
    }

    private static float EnemyArchetypeTintWeight(EnemyArchetype archetype)
    {
        return archetype == EnemyArchetype.Patrol ? 0f : 0.26f;
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
        bool walking = moveInput.sqrMagnitude > 0.01f;
        bool attacking = attackCooldown > 0.18f;
        renderer.flipX = walking || attacking ? direction == FacingDirection.Right : direction == FacingDirection.Left;
        int index = (int)direction;
        if (attacking)
        {
            renderer.sprite = playerAttackSprites[index] ?? playerSprite;
            return;
        }

        if (walking)
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
            Tile.Flashlight => flashlightSprite,
            Tile.Story => storySprite,
            Tile.Heal => healSprite,
            Tile.Exit => CanUseExit(cell) ? openExitSprite ?? exitSprite : exitSprite,
            _ => null,
        };
    }

    private Color OverlayColorFor(Tile tile, Vector2Int cell)
    {
        if (tile == Tile.Exit)
            return CanUseExit(cell) ? new Color(1.35f, 1.58f, 1.74f, 1f) : new Color(0.48f, 0.55f, 0.62f, 0.72f);

        if (tile == Tile.Gate && GateOpenForCell(cell))
            return new Color(1.18f, 1.38f, 1.52f, 1f);

        if (tile == Tile.Plate && StoneAt(cell) != null)
            return new Color(1.05f, 1.34f, 1.44f, 1f);

        return Color.white;
    }

    private static Vector3 OverlayScaleFor(Tile tile)
    {
        return tile switch
        {
            Tile.Remote => new Vector3(1.25f, 1.25f, 1f),
            Tile.Flashlight => new Vector3(1.18f, 1.18f, 1f),
            Tile.Story => new Vector3(1.15f, 1.15f, 1f),
            Tile.Heal => new Vector3(1.18f, 1.18f, 1f),
            Tile.Exit => new Vector3(1.10f, 1.10f, 1f),
            _ => Vector3.one,
        };
    }

    private Sprite WallSpriteFor(Vector2Int cell)
    {
        if (!WallVisibleFor(cell))
            return null;

        if (WallHasTurnJoint(cell))
            return wallCornerSprite ?? wallSprite;

        bool verticalLine = VisibleWallAdjacent(cell, Vector2Int.up) || VisibleWallAdjacent(cell, Vector2Int.down);
        bool horizontalLine = VisibleWallAdjacent(cell, Vector2Int.left) || VisibleWallAdjacent(cell, Vector2Int.right);
        if (verticalLine && !horizontalLine)
            return wallVerticalSprite ?? wallSprite;
        if (horizontalLine && !verticalLine)
            return wallSprite;

        bool up = OpenTileAdjacent(cell + Vector2Int.up);
        bool down = OpenTileAdjacent(cell + Vector2Int.down);
        bool left = OpenTileAdjacent(cell + Vector2Int.left);
        bool right = OpenTileAdjacent(cell + Vector2Int.right);

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
        if (!WallHasTurnJoint(cell))
            return 0f;

        bool up = OpenTileAdjacent(cell + Vector2Int.up);
        bool down = OpenTileAdjacent(cell + Vector2Int.down);
        bool left = OpenTileAdjacent(cell + Vector2Int.left);
        bool right = OpenTileAdjacent(cell + Vector2Int.right);

        if (up && right && !down && !left)
            return 180f;
        if (left && up && !right && !down)
            return 270f;
        if (down && left && !up && !right)
            return 0f;
        if (right && down && !left && !up)
            return 90f;

        return 0f;
    }

    private bool WallHasTurnJoint(Vector2Int cell)
    {
        bool upOpen = OpenTileAdjacent(cell + Vector2Int.up);
        bool downOpen = OpenTileAdjacent(cell + Vector2Int.down);
        bool leftOpen = OpenTileAdjacent(cell + Vector2Int.left);
        bool rightOpen = OpenTileAdjacent(cell + Vector2Int.right);
        int openCount = BoolCount(upOpen, downOpen, leftOpen, rightOpen);
        if (openCount != 2)
            return false;

        if (upOpen && rightOpen)
            return VisibleWallAdjacent(cell, Vector2Int.down) && VisibleWallAdjacent(cell, Vector2Int.left);
        if (rightOpen && downOpen)
            return VisibleWallAdjacent(cell, Vector2Int.left) && VisibleWallAdjacent(cell, Vector2Int.up);
        if (downOpen && leftOpen)
            return VisibleWallAdjacent(cell, Vector2Int.up) && VisibleWallAdjacent(cell, Vector2Int.right);
        if (leftOpen && upOpen)
            return VisibleWallAdjacent(cell, Vector2Int.right) && VisibleWallAdjacent(cell, Vector2Int.down);

        return false;
    }

    private bool VisibleWallAdjacent(Vector2Int cell, Vector2Int direction)
    {
        Vector2Int next = cell + direction;
        return ClosedWallAdjacent(next) && WallVisibleFor(next);
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
        if (!gateGroupsByCell.TryGetValue(cell, out string group))
            return false;

        if (gateObjectsByCell.TryGetValue(cell, out LevelObject gate) && HasExplicitGateRequirements(gate))
            return GateRequirementsMet(gate);

        return false;
    }

    private static BranchChoice ParseBranch(string branch)
    {
        return branch switch
        {
            "puzzle" => BranchChoice.Puzzle,
            "combat" => BranchChoice.Combat,
            _ => BranchChoice.None,
        };
    }

    private bool WallVisibleFor(Vector2Int cell)
    {
        return OpenTileAdjacent(cell + Vector2Int.up) ||
               OpenTileAdjacent(cell + Vector2Int.down) ||
               OpenTileAdjacent(cell + Vector2Int.left) ||
               OpenTileAdjacent(cell + Vector2Int.right) ||
               OpenTileAdjacent(cell + Vector2Int.up + Vector2Int.left) ||
               OpenTileAdjacent(cell + Vector2Int.up + Vector2Int.right) ||
               OpenTileAdjacent(cell + Vector2Int.down + Vector2Int.left) ||
               OpenTileAdjacent(cell + Vector2Int.down + Vector2Int.right);
    }

    private bool OpenTileAdjacent(Vector2Int cell)
    {
        return Inside(cell) && tiles[cell.x, cell.y] != Tile.Wall;
    }

    private Sprite FloorSpriteFor(Vector2Int cell)
    {
        if (floorSprites.Length == 0)
            return floorSprite;

        int variant = tileVariants[cell.x, cell.y];
        if (variant >= 0)
            return floorSprites[0];

        return floorSprites[CellHash(cell, 11) % floorSprites.Length];
    }

    private Sprite FloorDecalFor(Vector2Int cell)
    {
        if (floorDecalSprites.Length == 0 || DecalSuppressed(cell))
            return null;

        int variant = tileVariants[cell.x, cell.y];
        if (variant == 0)
            return null;
        if (variant > 0)
            return floorDecalSprites[(variant - 1) % floorDecalSprites.Length];

        int adjacentWalls = NearbyWallWeight(cell);
        int roll = CellHash(cell, 29) % 100;
        int chance = adjacentWalls >= 4 ? 20 : adjacentWalls >= 2 ? 9 : 2;
        if (roll >= chance)
            return null;

        return floorDecalSprites[CellHash(cell, 47) % floorDecalSprites.Length];
    }

    private bool DecalSuppressed(Vector2Int cell)
    {
        if (Manhattan(cell, playerStart) <= 2 || StoneAt(cell) != null || EnemyAt(cell) != null)
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
               tile == Tile.Flashlight ||
               tile == Tile.Story ||
               tile == Tile.Heal ||
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
}
