using System;
using UnityEngine;

public sealed partial class PrototypeGame
{
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
            healSprite = CreateFixedAtlasSprite(EnvironmentAtlas, 5, 4, "heal_cassette", true);
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
        int walkTwoCol = direction == FacingDirection.Down ? 5 : 2;
        int attackCol = direction == FacingDirection.Down ? 6 : 3;

        playerIdleSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, 0, $"player_{direction}_idle");
        playerWalkOneSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, 1, $"player_{direction}_walk_1");
        playerWalkTwoSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, walkTwoCol, $"player_{direction}_walk_2");
        playerAttackSprites[index] = CreateFixedAtlasSprite(CharacterAtlas, row, attackCol, $"player_{direction}_attack");
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
        bool isStandardGreen = color.g > 0.22f && color.g - maxOther > 0.10f && color.r < 0.50f && color.b < 0.50f;
        bool isLimeGreen = color.g > 0.50f && color.g - color.b > 0.30f && color.r > 0.50f && color.r < 0.85f && color.b < 0.50f;
        return isStandardGreen || isLimeGreen;
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
        healSprite = CreateSprite(new Color(0.10f, 0.20f, 0.18f), new Color(0.04f, 0.08f, 0.07f), new Color(0.74f, 1.00f, 0.74f), SpriteMark.Heal);
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
        enemyBeamSprite = CreateEnemyBeamSprite();
    }

    private static Sprite CreateEnemyBeamSprite()
    {
        ThrowIfPlayingBake("CreateEnemyBeamSprite");
        const int width = 128;
        const int height = 256;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
        };

        Fill(texture, Color.clear);
        Color beamColor = new Color(0.76f, 0.92f, 1f, 1f);
        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            float halfWidth = Mathf.Lerp(1.5f, 60f, Mathf.SmoothStep(0f, 1f, t));
            float center = (width - 1) * 0.5f;
            float distanceFade = Mathf.SmoothStep(1f, 0.08f, t);
            float originFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.08f) / 0.28f));
            for (int x = 0; x < width; x++)
            {
                float edge = Mathf.Abs(x - center) / halfWidth;
                if (edge > 1f)
                    continue;

                float sideFade = Mathf.SmoothStep(1f, 0f, edge);
                float centerLift = Mathf.Lerp(0.62f, 1f, sideFade);
                float alpha = 0.34f * sideFade * distanceFade * centerLift * originFade;
                texture.SetPixel(x, y, new Color(beamColor.r, beamColor.g, beamColor.b, alpha));
            }
        }

        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
        sprite.name = "enemy_beam";
        return sprite;
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
            case SpriteMark.Heal:
                DrawRect(texture, 3, 5, 10, 6, color, false);
                DrawRect(texture, 5, 7, 2, 2, color, false);
                DrawRect(texture, 9, 7, 2, 2, color, false);
                DrawLine(texture, 6, 12, 10, 12, color);
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
                hudPanelTexture = CreateHudAtlasTexture(1, 0, "hud_panel", true);
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
}
