using UnityEngine;

public sealed partial class PrototypeGame
{
    private void OnGUI()
    {
        EnsureHudTextures();
        if (hudTexture == null || whiteTexture == null)
            return;

        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = PixelGui.ScaledMatrix;
        Vector2 guiSize = PixelGui.LogicalSize;
        float screenWidth = guiSize.x;
        float screenHeight = guiSize.y;

        GUI.color = Color.white;
        bool compact = screenWidth < 860f;
        float ui = HudScale;
        float margin = (compact ? 10f : 16f) * ui;
        float meterWidth = (compact ? 54f : 64f) * ui;
        float controlsHeight = (screenWidth < 720f ? 58f : 42f) * ui;
        float meterHeight = Mathf.Min((compact ? 230f : 308f) * ui, Mathf.Max(148f * ui, screenHeight - margin * 2f - controlsHeight - 16f * ui));
        Rect ratingRect = new Rect(screenWidth - margin - meterWidth, margin, meterWidth, meterHeight);

        var noteStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt((compact ? 15 : 18) * ui),
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            normal = { textColor = new Color(0.95f, 0.98f, 1f) },
        };
        PixelGui.Apply(noteStyle);

        var hintStyle = new GUIStyle(noteStyle)
        {
            fontSize = Mathf.RoundToInt((compact ? 12 : 13) * ui),
            normal = { textColor = new Color(0.78f, 0.86f, 0.90f) },
        };
        PixelGui.Apply(hintStyle);

        DrawVerticalRatingMeter(ratingRect);

        Rect controlsRect = PixelRect(new Rect(margin, screenHeight - margin - controlsHeight, screenWidth - margin * 2f, controlsHeight));
        float skillWidth = Mathf.Min((compact ? 178f : 218f) * ui, Mathf.Max(150f * ui, ratingRect.x - margin * 2f));
        float skillHeight = (compact ? 50f : 58f) * ui;
        Rect skillRect = PixelRect(new Rect(ratingRect.x - margin - skillWidth, controlsRect.y - 8f * ui - skillHeight, skillWidth, skillHeight));
        DrawActiveSkillPanel(skillRect);

        float hpWidth = Mathf.Min(246f * ui, screenWidth - margin * 2f);
        Rect hpRect = PixelRect(new Rect(margin, controlsRect.y - 8f * ui - 40f * ui, hpWidth, 40f * ui));
        DrawHpHeartPanel(hpRect);
        DrawControlsBar(controlsRect, hintStyle, screenWidth);

        if (runCompleted)
            DrawCompletionOverlay(screenWidth, screenHeight);

        if (!string.IsNullOrEmpty(noteMessage) && noteMessageTimer > 0f)
            DrawNotePanel(PixelRect(new Rect(0f, 0f, screenWidth, (compact ? 74f : 82f) * ui)), noteStyle);

        GUI.matrix = previousMatrix;
    }

    private void DrawStatusPanel(Rect rect, GUIStyle labelStyle, GUIStyle hintStyle)
    {
        DrawPanelBacking(rect, new Color(0.015f, 0.020f, 0.026f, 0.86f), new Color(0.56f, 0.70f, 0.76f, 0.42f));

        Texture2D panel = hudPanelTexture ?? hudTexture;
        if (panel != null)
            DrawTexturePreservingAtlasPart(rect, panel, new Color(1f, 1f, 1f, 0.20f));

        float ui = HudScale;
        Rect inner = new Rect(rect.x + 12f * ui, rect.y + 9f * ui, rect.width - 24f * ui, rect.height - 18f * ui);
        var titleStyle = new GUIStyle(hintStyle)
        {
            fontSize = Mathf.RoundToInt((rect.width < 285f * ui ? 11 : 12) * ui),
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.68f, 0.78f, 0.82f) },
        };
        PixelGui.Apply(titleStyle);

        var valueStyle = new GUIStyle(labelStyle)
        {
            fontSize = Mathf.RoundToInt((rect.width < 285f * ui ? 13 : 15) * ui),
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white },
        };
        PixelGui.Apply(valueStyle);

        DrawLabelWithShadow(new Rect(inner.x, inner.y, 72f * ui, 18f * ui), "HP", titleStyle);
        DrawHpPips(new Rect(inner.x, inner.y + 22f * ui, Mathf.Min(128f * ui, inner.width * 0.46f), 18f * ui));
        DrawLabelWithShadow(new Rect(inner.x, inner.y + 48f * ui, 128f * ui, 22f * ui), $"{Mathf.Clamp(playerHp, 0, 6)}/6", valueStyle);

        float remoteX = inner.x + Mathf.Min(148f * ui, inner.width * 0.52f);
        Rect remoteIcon = new Rect(remoteX, inner.y + 24f * ui, 28f * ui, 28f * ui);
        DrawRemoteIcon(remoteIcon);
        DrawLabelWithShadow(new Rect(remoteX, inner.y, inner.xMax - remoteX, 18f * ui), "ПУЛЬТ", titleStyle);
        DrawLabelWithShadow(new Rect(remoteX + 34f * ui, inner.y + 22f * ui, inner.xMax - remoteX - 34f * ui, 24f * ui), RemoteHudText(), valueStyle);
        DrawLabelWithShadow(new Rect(remoteX, inner.y + 52f * ui, inner.xMax - remoteX, 20f * ui), BranchHudText(), titleStyle);
    }

    private void DrawActiveSkillPanel(Rect rect)
    {
        Color border = hasRemote ? new Color(0.45f, 0.92f, 1f, 0.78f) : new Color(0.38f, 0.50f, 0.56f, 0.46f);
        DrawPanelBacking(rect, hasRemote ? new Color(0.012f, 0.044f, 0.052f, 0.92f) : new Color(0.010f, 0.013f, 0.018f, 0.88f), border);

        float ui = HudScale;
        DrawFilledRect(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 2f), hasRemote ? new Color(0.62f, 1f, 1f, 0.62f) : new Color(0.42f, 0.50f, 0.54f, 0.42f));

        float iconSize = Mathf.Min(rect.height - 14f * ui, 38f * ui);
        Rect remoteRect = PixelRect(new Rect(rect.x + 10f * ui, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize));
        DrawRemoteIcon(remoteRect);

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(11f * ui),
            normal = { textColor = hasRemote ? new Color(0.66f, 0.95f, 1f) : new Color(0.58f, 0.66f, 0.70f) },
        };
        PixelGui.Apply(titleStyle);

        var valueStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt((rect.width < 185f * ui ? 16f : 18f) * ui),
            normal = { textColor = new Color(0.92f, 0.98f, 1f) },
        };
        PixelGui.Apply(valueStyle);

        float textX = remoteRect.xMax + 9f * ui;
        DrawLabelWithShadow(new Rect(textX, rect.y + 7f * ui, rect.xMax - textX - 10f * ui, 17f * ui), "ПУЛЬТ", titleStyle);
        DrawLabelWithShadow(new Rect(textX, rect.y + 24f * ui, rect.xMax - textX - 10f * ui, 28f * ui), RemoteSkillHudText(), valueStyle);
    }

    private void DrawHpHeartPanel(Rect rect)
    {
        DrawPanelBacking(rect, new Color(0.010f, 0.013f, 0.018f, 0.84f), new Color(0.38f, 0.50f, 0.56f, 0.32f));

        Texture2D heartTexture = GetRuntimeAtlasCell(EnvironmentAtlas, FixedAtlasRows, FixedAtlasColumns, HeartAtlasRow, HeartAtlasColumn, "hp_heart", true);
        float ui = HudScale;
        float heartSize = 24f * ui;
        float gap = 6f * ui;
        float totalWidth = heartSize * 6f + gap * 5f;
        float x = rect.x + Mathf.Max(8f * ui, (rect.width - totalWidth) * 0.5f);
        float y = rect.y + (rect.height - heartSize) * 0.5f;

        for (int i = 0; i < 6; i++)
        {
            Rect heartRect = PixelRect(new Rect(x + i * (heartSize + gap), y, heartSize, heartSize));
            bool filled = i < playerHp;
            if (heartTexture != null)
            {
                DrawTexturePreservingAtlasPart(heartRect, heartTexture, filled ? Color.white : new Color(0.24f, 0.27f, 0.30f, 0.62f));
            }
            else
            {
                DrawFilledRect(heartRect, filled ? new Color(0.86f, 0.12f, 0.12f, 0.95f) : new Color(0.18f, 0.20f, 0.23f, 0.72f));
            }
        }
    }

    private void DrawHpPips(Rect rect)
    {
        float gap = 3f * HudScale;
        float pipWidth = Mathf.Max(8f * HudScale, (rect.width - gap * 5f) / 6f);
        for (int i = 0; i < 6; i++)
        {
            Rect pip = PixelRect(new Rect(rect.x + i * (pipWidth + gap), rect.y, pipWidth, rect.height));
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(pip, whiteTexture);
            GUI.color = i < playerHp ? new Color(0.90f, 0.98f, 1f, 0.98f) : new Color(0.17f, 0.20f, 0.23f, 0.96f);
            GUI.DrawTexture(new Rect(pip.x + 1f, pip.y + 1f, pip.width - 2f, pip.height - 2f), whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DrawRemoteIcon(Rect rect)
    {
        DrawFilledRect(rect, hasRemote ? new Color(0.05f, 0.07f, 0.08f, 0.92f) : new Color(0.03f, 0.035f, 0.04f, 0.78f));
        float inset = 3f * HudScale;
        DrawSpritePreservingAtlasPart(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f), remoteSprite, hasRemote ? Color.white : new Color(0.40f, 0.43f, 0.46f, 0.72f));

        if (hasRemote && remoteCooldown > 0f)
        {
            float ratio = Mathf.Clamp01(remoteCooldown / RemoteCooldown);
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height * ratio), whiteTexture);
        }

        GUI.color = Color.white;
    }

    private void DrawNotePanel(Rect rect, GUIStyle noteStyle)
    {
        DrawPanelBacking(rect, new Color(0.012f, 0.016f, 0.022f, 0.93f), new Color(0.58f, 0.76f, 0.82f, 0.50f));
        float ui = HudScale;
        float padX = 18f * ui;
        float padY = 9f * ui;
        DrawLabelWithShadow(new Rect(rect.x + padX, rect.y + padY, rect.width - padX * 2f, rect.height - padY * 2f), noteMessage, noteStyle);
    }

    private void DrawControlsBar(Rect rect, GUIStyle hintStyle, float screenWidth)
    {
        DrawPanelBacking(rect, new Color(0.010f, 0.013f, 0.018f, 0.84f), new Color(0.38f, 0.50f, 0.56f, 0.32f));
        string controls = screenWidth < 720f
            ? "WASD/стрелки - движение | Space/ЛКМ - атака | E - действие | Q - пульт\nR - рестарт | Esc - меню"
            : "WASD/стрелки - движение | Space/ЛКМ - атака | E - действие/толкнуть | Q - пульт | R - рестарт | Esc - меню";
        var style = new GUIStyle(hintStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = new Color(0.86f, 0.92f, 0.96f) },
        };
        PixelGui.Apply(style);
        float ui = HudScale;
        DrawLabelWithShadow(new Rect(rect.x + 10f * ui, rect.y + 2f * ui, rect.width - 20f * ui, rect.height - 4f * ui), controls, style);
    }

    private void DrawCompletionOverlay(float screenWidth, float screenHeight)
    {
        DrawFilledRect(new Rect(0f, 0f, screenWidth, screenHeight), new Color(0f, 0f, 0f, 0.58f));
        float ui = HudScale;
        float panelWidth = Mathf.Min(560f * ui, screenWidth - 32f * ui);
        float panelHeight = 164f * ui;
        Rect panel = PixelRect(new Rect((screenWidth - panelWidth) * 0.5f, screenHeight * 0.5f - panelHeight * 0.5f, panelWidth, panelHeight));
        DrawPanelBacking(panel, new Color(0.014f, 0.020f, 0.028f, 0.94f), new Color(0.70f, 0.86f, 0.92f, 0.50f));

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt((screenWidth < 720f ? 25 : 34) * ui),
            normal = { textColor = Color.white },
        };
        PixelGui.Apply(titleStyle);

        var hintStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.RoundToInt((screenWidth < 720f ? 13 : 15) * ui),
            normal = { textColor = new Color(0.76f, 0.86f, 0.90f) },
        };
        PixelGui.Apply(hintStyle);

        DrawLabelWithShadow(new Rect(panel.x + 18f * ui, panel.y + 30f * ui, panel.width - 36f * ui, 52f * ui), "Вы прошли игру", titleStyle);
        DrawLabelWithShadow(new Rect(panel.x + 18f * ui, panel.y + 92f * ui, panel.width - 36f * ui, 34f * ui), "Нажмите R, чтобы пересмотреть канал", hintStyle);
    }

    private string RemoteHudText()
    {
        if (!hasRemote)
            return "нет";
        if (RemoteJamActive())
            return "глушит";
        if (remoteCooldown > 0f)
            return $"{Mathf.CeilToInt(remoteCooldown)} сек.";

        return "Q готов";
    }

    private string RemoteSkillHudText()
    {
        if (!hasRemote)
            return "нет";
        if (RemoteJamActive())
            return "глушит";
        if (remoteCooldown > 0f)
            return $"КД {Mathf.CeilToInt(remoteCooldown)} с";

        return "Q готов";
    }

    private string BranchHudText()
    {
        return NarrativeRunState.Branch switch
        {
            BranchChoice.Puzzle => "режим: разбор",
            BranchChoice.Combat => "режим: атака",
            _ => "режим: нейтр.",
        };
    }

    private void DrawPanelBacking(Rect rect, Color fill, Color border)
    {
        DrawFilledRect(rect, fill);
        DrawFilledRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
        DrawFilledRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
        DrawFilledRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
        DrawFilledRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
    }

    private void DrawFilledRect(Rect rect, Color color)
    {
        if (whiteTexture == null)
            return;

        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(PixelRect(rect), whiteTexture);
        GUI.color = previous;
    }

    private void DrawLabelWithShadow(Rect rect, string text, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Color textColor = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.82f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);
    }

    private void DrawTexturePreservingAtlasPart(Rect target, Texture2D texture, Color tint)
    {
        if (texture == null)
            return;

        Rect bounds = VisibleTextureBounds(texture);
        Rect drawRect = FitRectToAspect(target, bounds.width / Mathf.Max(1f, bounds.height));
        Rect uv = new Rect(bounds.x / texture.width, bounds.y / texture.height, bounds.width / texture.width, bounds.height / texture.height);
        Color previous = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
        GUI.color = previous;
    }

    private void DrawSpritePreservingAtlasPart(Rect target, Sprite sprite, Color tint)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Rect source = sprite.textureRect;
        Rect drawRect = FitRectToAspect(target, source.width / Mathf.Max(1f, source.height));
        Rect uv = new Rect(source.x / sprite.texture.width, source.y / sprite.texture.height, source.width / sprite.texture.width, source.height / sprite.texture.height);
        Color previous = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uv, true);
        GUI.color = previous;
    }

    private void DrawVerticalRatingMeter(Rect rect)
    {
        Texture2D frame = RatingFrameTexture();
        Rect frameRect = frame != null ? FitRectToAspect(rect, VisibleTextureBounds(frame).width / Mathf.Max(1f, VisibleTextureBounds(frame).height)) : PixelRect(rect);

        GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);
        GUI.DrawTexture(frameRect, whiteTexture);

        Rect inner = new Rect(frameRect.x + frameRect.width * 0.29f, frameRect.y + frameRect.height * 0.12f, frameRect.width * 0.42f, frameRect.height * 0.72f);
        GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.94f);
        GUI.DrawTexture(inner, whiteTexture);

        float fill = inner.height * Mathf.Clamp01(viewerRating / 100f);
        GUI.color = RatingColor();
        GUI.DrawTexture(new Rect(inner.x + 1f, inner.yMax - fill + 1f, inner.width - 2f, Mathf.Max(0f, fill - 2f)), whiteTexture);

        GUI.color = new Color(0.72f, 0.78f, 0.84f, 0.42f);
        for (int i = 0; i < 6; i++)
        {
            float y = Mathf.Lerp(inner.yMax - 3f, inner.y + 3f, i / 5f);
            GUI.DrawTexture(new Rect(inner.x + 1f, y, inner.width - 2f, 1f), whiteTexture);
        }

        if (frame != null)
        {
            DrawTexturePreservingAtlasPart(frameRect, frame, Color.white);
        }
        else
        {
            GUI.color = new Color(0.70f, 0.76f, 0.82f, 0.58f);
            GUI.DrawTexture(new Rect(frameRect.x + 4f, frameRect.y + 4f, frameRect.width - 8f, 2f), whiteTexture);
            GUI.DrawTexture(new Rect(frameRect.x + 4f, frameRect.yMax - 6f, frameRect.width - 8f, 2f), whiteTexture);
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
}
