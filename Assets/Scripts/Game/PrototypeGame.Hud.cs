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
        float ratingLabelHeight = (compact ? 18f : 20f) * ui;
        float meterHeight = Mathf.Min((compact ? 230f : 308f) * ui, Mathf.Max(148f * ui, screenHeight - margin * 2f - ratingLabelHeight - 10f * ui));
        Rect ratingRect = new Rect(screenWidth - margin - meterWidth, margin, meterWidth, meterHeight);

        var noteStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt((compact ? 18 : 22) * ui),
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
        DrawRatingLabel(ratingRect, compact, ui);

        float skillWidth = Mathf.Min((compact ? 178f : 218f) * ui, Mathf.Max(150f * ui, screenWidth - margin * 2f));
        float skillHeight = (compact ? 62f : 70f) * ui;
        Rect skillRect = PixelRect(new Rect(screenWidth - margin - skillWidth, screenHeight - margin - skillHeight, skillWidth, skillHeight));
        DrawActiveSkillPanel(skillRect);

        float hpWidth = Mathf.Min(292f * ui, screenWidth - margin * 2f);
        Rect hpRect = PixelRect(new Rect(margin, margin, hpWidth, 52f * ui));
        DrawHpHeartPanel(hpRect);

        if (NoteOverlayActive())
        {
            DrawStoryNoteOverlay(screenWidth, screenHeight, noteStyle, hintStyle);
            GUI.matrix = previousMatrix;
            return;
        }

        if (runCompleted)
            DrawCompletionOverlay(screenWidth, screenHeight);

        if (!string.IsNullOrEmpty(noteMessage) && noteMessageTimer > 0f)
        {
            float noteHeight = (compact ? 108f : 126f) * ui;
            float maxNoteWidth = Mathf.Max(220f * ui, skillRect.x - margin * 2f);
            float noteWidth = Mathf.Min(maxNoteWidth, (compact ? 620f : 860f) * ui);
            DrawNotePanel(PixelRect(new Rect(margin, screenHeight - margin - noteHeight, noteWidth, noteHeight)), noteStyle);
        }

        if (gameEnded && !runCompleted)
            DrawGameOverOverlay(screenWidth, screenHeight);
        else if (paused)
            DrawPauseOverlay(screenWidth, screenHeight);

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
        DrawAbilityIcon(remoteIcon);
        DrawLabelWithShadow(new Rect(remoteX, inner.y, inner.xMax - remoteX, 18f * ui), AbilityTitle(), titleStyle);
        DrawLabelWithShadow(new Rect(remoteX + 34f * ui, inner.y + 22f * ui, inner.xMax - remoteX - 34f * ui, 24f * ui), AbilityHudText(), valueStyle);
        DrawLabelWithShadow(new Rect(remoteX, inner.y + 52f * ui, inner.xMax - remoteX, 20f * ui), BranchHudText(), titleStyle);
    }

    private void DrawActiveSkillPanel(Rect rect)
    {
        bool hasAbility = equippedAbility != AbilitySlot.None;
        Color accent = HasFlashlight ? new Color(1.00f, 0.88f, 0.42f, 0.95f) : HasRemote ? new Color(0.52f, 0.96f, 1f, 0.95f) : new Color(0.48f, 0.58f, 0.64f, 0.72f);
        DrawHudPanel(rect, hasAbility ? new Color(0.034f, 0.030f, 0.014f, 0.96f) : new Color(0.010f, 0.014f, 0.020f, 0.94f), accent, true);

        float ui = HudScale;
        float iconSize = Mathf.Min(rect.height - 14f * ui, 38f * ui);
        Rect abilityRect = PixelRect(new Rect(rect.x + 10f * ui, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize));
        DrawAbilityIcon(abilityRect);

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(11f * ui),
            normal = { textColor = hasAbility ? accent : new Color(0.58f, 0.66f, 0.70f) },
        };
        PixelGui.Apply(titleStyle);

        var valueStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt((rect.width < 185f * ui ? 16f : 18f) * ui),
            normal = { textColor = new Color(0.92f, 0.98f, 1f) },
        };
        PixelGui.Apply(valueStyle);

        float textX = abilityRect.xMax + 9f * ui;
        DrawLabelWithShadow(new Rect(textX, rect.y + 7f * ui, rect.xMax - textX - 10f * ui, 17f * ui), AbilityTitle(), titleStyle);
        DrawLabelWithShadow(new Rect(textX, rect.y + 24f * ui, rect.xMax - textX - 10f * ui, 24f * ui), AbilitySkillHudText(), valueStyle);
        DrawRemoteTimerBar(new Rect(textX, rect.yMax - 15f * ui, rect.xMax - textX - 10f * ui, 8f * ui), ui);
    }

    private void DrawRemoteTimerBar(Rect rect, float ui)
    {
        if (!HasRemote || (remoteCooldown <= 0f && !RemoteJamActive()))
            return;

        bool active = RemoteJamActive();
        float cooldownOnlyDuration = Mathf.Max(0.01f, RemoteCooldown - RemoteJamDuration);
        float ratio = active
            ? Mathf.Clamp01(remoteJamTimer / RemoteJamDuration)
            : 1f - Mathf.Clamp01(Mathf.Min(remoteCooldown, cooldownOnlyDuration) / cooldownOnlyDuration);
        Color fill = active ? new Color(0.44f, 0.96f, 1f, 0.96f) : new Color(1.00f, 0.18f, 0.14f, 0.94f);
        Color border = active ? new Color(0.62f, 1f, 1f, 0.74f) : new Color(1f, 0.36f, 0.30f, 0.72f);

        DrawFilledRect(rect, new Color(0.018f, 0.020f, 0.026f, 0.94f));
        DrawFilledRect(new Rect(rect.x, rect.y, rect.width, 1f * ui), border);
        DrawFilledRect(new Rect(rect.x, rect.yMax - 1f * ui, rect.width, 1f * ui), border);
        DrawFilledRect(new Rect(rect.x, rect.y, 1f * ui, rect.height), border);
        DrawFilledRect(new Rect(rect.xMax - 1f * ui, rect.y, 1f * ui, rect.height), border);

        float inset = 2f * ui;
        Rect fillRect = new Rect(rect.x + inset, rect.y + inset, Mathf.Max(0f, rect.width - inset * 2f) * ratio, Mathf.Max(0f, rect.height - inset * 2f));
        if (fillRect.width > 0.5f)
            DrawFilledRect(fillRect, fill);
    }

    private void DrawHpHeartPanel(Rect rect)
    {
        Texture2D heartTexture = GetRuntimeAtlasCell(EnvironmentAtlas, FixedAtlasRows, FixedAtlasColumns, HeartAtlasRow, HeartAtlasColumn, "hp_heart", true);
        float ui = HudScale;
        float heartSize = 30f * ui;
        float gap = 7f * ui;
        float totalWidth = heartSize * 6f + gap * 5f;
        float x = rect.x + Mathf.Max(0f, (rect.width - totalWidth) * 0.5f);
        float y = rect.y + (rect.height - heartSize) * 0.5f;

        for (int i = 0; i < 6; i++)
        {
            Rect heartRect = PixelRect(new Rect(x + i * (heartSize + gap), y, heartSize, heartSize));
            bool filled = i < playerHp;
            DrawFilledRect(new Rect(heartRect.x + 2f * ui, heartRect.y + 3f * ui, heartRect.width, heartRect.height), new Color(0f, 0f, 0f, 0.46f));
            DrawPanelBacking(heartRect, filled ? new Color(0.22f, 0.015f, 0.025f, 0.92f) : new Color(0.035f, 0.040f, 0.050f, 0.96f), filled ? new Color(1f, 0.18f, 0.24f, 0.78f) : new Color(0.22f, 0.26f, 0.30f, 0.74f));
            if (heartTexture != null)
            {
                DrawTexturePreservingAtlasPart(new Rect(heartRect.x + 2f * ui, heartRect.y + 2f * ui, heartRect.width - 4f * ui, heartRect.height - 4f * ui), heartTexture, filled ? new Color(1f, 0.96f, 0.98f, 1f) : new Color(0.36f, 0.38f, 0.42f, 0.68f));
            }
            else
            {
                DrawFilledRect(new Rect(heartRect.x + 4f * ui, heartRect.y + 4f * ui, heartRect.width - 8f * ui, heartRect.height - 8f * ui), filled ? new Color(1.00f, 0.06f, 0.12f, 0.98f) : new Color(0.18f, 0.20f, 0.23f, 0.72f));
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

    private void DrawAbilityIcon(Rect rect)
    {
        bool hasAbility = equippedAbility != AbilitySlot.None;
        DrawFilledRect(rect, hasAbility ? new Color(0.05f, 0.07f, 0.08f, 0.92f) : new Color(0.03f, 0.035f, 0.04f, 0.78f));
        float inset = 3f * HudScale;
        Sprite sprite = HasFlashlight ? flashlightSprite : remoteSprite;
        DrawSpritePreservingAtlasPart(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f), sprite, hasAbility ? Color.white : new Color(0.40f, 0.43f, 0.46f, 0.72f));

        GUI.color = Color.white;
    }

    private void DrawNotePanel(Rect rect, GUIStyle noteStyle)
    {
        DrawHudPanel(rect, new Color(0.010f, 0.018f, 0.026f, 0.96f), new Color(0.58f, 0.92f, 1f, 0.82f), true);
        float ui = HudScale;
        float padX = 18f * ui;
        float padY = 12f * ui;
        var titleStyle = new GUIStyle(noteStyle)
        {
            fontSize = Mathf.RoundToInt(20f * ui),
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = new Color(0.64f, 0.94f, 1f) },
        };
        PixelGui.Apply(titleStyle);

        DrawLabelWithShadow(new Rect(rect.x + padX, rect.y + 8f * ui, rect.width - padX * 2f, 28f * ui), string.IsNullOrEmpty(noteMessageSpeaker) ? "Вы" : noteMessageSpeaker, titleStyle);
        DrawLabelWithShadow(new Rect(rect.x + padX, rect.y + padY + 32f * ui, rect.width - padX * 2f, rect.height - padY * 2f - 32f * ui), VisibleNoteText(), noteStyle);
    }

    private void DrawStoryNoteOverlay(float screenWidth, float screenHeight, GUIStyle noteStyle, GUIStyle hintStyle)
    {
        EnsureNotePaperTexture();
        DrawFilledRect(new Rect(0f, 0f, screenWidth, screenHeight), new Color(0f, 0f, 0f, 0.72f));

        float ui = HudScale;
        float frameSize = Mathf.Min(screenWidth - 36f * ui, screenHeight - 34f * ui, 620f * ui);
        Rect frameRect = PixelRect(new Rect((screenWidth - frameSize) * 0.5f, (screenHeight - frameSize) * 0.5f, frameSize, frameSize));

        if (notePaperTexture != null)
            DrawTexturePreservingAtlasPart(frameRect, notePaperTexture, Color.white);
        else
            DrawHudPanel(frameRect, new Color(0.86f, 0.80f, 0.68f, 0.98f), new Color(0.26f, 0.18f, 0.12f, 0.80f), false);

        bool hasText = !string.IsNullOrEmpty(noteMessage);
        Rect imageRect = hasText
            ? new Rect(frameRect.x + frameRect.width * 0.17f, frameRect.y + frameRect.height * 0.16f, frameRect.width * 0.66f, frameRect.height * 0.48f)
            : new Rect(frameRect.x + frameRect.width * 0.15f, frameRect.y + frameRect.height * 0.15f, frameRect.width * 0.70f, frameRect.height * 0.64f);
        if (noteImageTexture != null)
        {
            Rect fitted = FitRectToAspect(imageRect, noteImageTexture.width / Mathf.Max(1f, noteImageTexture.height));
            GUI.DrawTexture(fitted, noteImageTexture, ScaleMode.ScaleToFit, true);
        }

        if (hasText)
        {
            var paperTextStyle = new GUIStyle(noteStyle)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.RoundToInt((screenWidth < 860f ? 17 : 20) * ui),
                normal = { textColor = new Color(0.16f, 0.12f, 0.10f, 0.96f) },
            };
            PixelGui.Apply(paperTextStyle);
            Rect textRect = new Rect(frameRect.x + frameRect.width * 0.15f, frameRect.y + frameRect.height * 0.66f, frameRect.width * 0.70f, frameRect.height * 0.18f);
            GUI.Label(textRect, VisibleNoteText(), paperTextStyle);
        }

        var closeStyle = new GUIStyle(hintStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.22f, 0.18f, 0.16f, 0.82f) },
        };
        PixelGui.Apply(closeStyle);
        GUI.Label(new Rect(frameRect.x + frameRect.width * 0.18f, frameRect.yMax - frameRect.height * 0.13f, frameRect.width * 0.64f, 26f * ui), "E / Space / Enter", closeStyle);
    }

    private void EnsureNotePaperTexture()
    {
        if (notePaperTexture == null)
            notePaperTexture = Resources.Load<Texture2D>("UI/note_paper_frame");
    }

    private void DrawCompletionOverlay(float screenWidth, float screenHeight)
    {
        DrawFilledRect(new Rect(0f, 0f, screenWidth, screenHeight), new Color(0f, 0f, 0f, 0.58f));
        float ui = HudScale;
        float panelWidth = Mathf.Min(560f * ui, screenWidth - 32f * ui);
        float panelHeight = 164f * ui;
        Rect panel = PixelRect(new Rect((screenWidth - panelWidth) * 0.5f, screenHeight * 0.5f - panelHeight * 0.5f, panelWidth, panelHeight));
        DrawHudPanel(panel, new Color(0.014f, 0.020f, 0.028f, 0.96f), new Color(0.72f, 0.92f, 1f, 0.80f), true);

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
        DrawLabelWithShadow(new Rect(panel.x + 18f * ui, panel.y + 92f * ui, panel.width - 36f * ui, 34f * ui), "Нажмите R, чтобы переиграть", hintStyle);
    }

    private string AbilityHudText()
    {
        if (HasFlashlight)
            return "светит";
        if (!HasRemote)
            return "нет";
        if (RemoteJamActive())
            return "Активен";
        if (remoteCooldown > 0f)
            return $"{Mathf.CeilToInt(remoteCooldown)} сек.";

        return "Q готов";
    }

    private void DrawPauseOverlay(float screenWidth, float screenHeight)
    {
        DrawMenuOverlay(screenWidth, screenHeight, "Пауза", showPauseBindings ? "Бинды" : "Вы можете возобновить игру", false);
    }

    private void DrawGameOverOverlay(float screenWidth, float screenHeight)
    {
        DrawMenuOverlay(screenWidth, screenHeight, "Вы умерли", EndlessRunState.Enabled ? "Попытка закончена. Повтор начнётся с первого уровня." : "Попробуйте переиграть.", true);
    }

    private void DrawMenuOverlay(float screenWidth, float screenHeight, string title, string subtitle, bool gameOver)
    {
        DrawFilledRect(new Rect(0f, 0f, screenWidth, screenHeight), new Color(0f, 0f, 0f, 0.62f));
        float ui = HudScale;
        float panelWidth = Mathf.Min(420f * ui, screenWidth - 32f * ui);
        float panelHeight = (showPauseBindings && !gameOver ? 330f : gameOver ? 250f : 420f) * ui;
        Rect panel = PixelRect(new Rect((screenWidth - panelWidth) * 0.5f, (screenHeight - panelHeight) * 0.5f, panelWidth, panelHeight));
        DrawHudPanel(panel, new Color(0.014f, 0.020f, 0.028f, 0.97f), gameOver ? new Color(1f, 0.22f, 0.28f, 0.82f) : new Color(0.72f, 0.92f, 1f, 0.80f), true);

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(28f * ui),
            normal = { textColor = Color.white },
        };
        PixelGui.Apply(titleStyle);

        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(13f * ui),
            wordWrap = true,
            normal = { textColor = new Color(0.78f, 0.88f, 0.92f) },
        };
        PixelGui.Apply(textStyle);

        var buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(16f * ui),
        };
        PixelGui.Apply(buttonStyle);

        DrawLabelWithShadow(new Rect(panel.x + 18f * ui, panel.y + 22f * ui, panel.width - 36f * ui, 42f * ui), title, titleStyle);
        DrawLabelWithShadow(new Rect(panel.x + 24f * ui, panel.y + 68f * ui, panel.width - 48f * ui, 38f * ui), subtitle, textStyle);

        if (showPauseBindings && !gameOver)
        {
            DrawBindingsList(panel, textStyle, buttonStyle, ui);
            return;
        }

        float buttonWidth = panel.width - 70f * ui;
        float buttonHeight = 38f * ui;
        float buttonX = panel.x + (panel.width - buttonWidth) * 0.5f;
        float y = panel.y + 122f * ui;

        if (gameOver)
        {
            if (GUI.Button(PixelRect(new Rect(buttonX, y, buttonWidth, buttonHeight)), "Переиграть", buttonStyle))
                RetryAfterDeath();
            y += buttonHeight + 12f * ui;
        }
        else
        {
            if (GUI.Button(PixelRect(new Rect(buttonX, y, buttonWidth, buttonHeight)), "Продолжить", buttonStyle))
                SetPaused(false);
            y += buttonHeight + 12f * ui;
            if (GUI.Button(PixelRect(new Rect(buttonX, y, buttonWidth, buttonHeight)), "Бинды", buttonStyle))
                showPauseBindings = true;
            y += buttonHeight + 12f * ui;
            DrawPauseLightingToggle(PixelRect(new Rect(buttonX, y, buttonWidth, 42f * ui)), textStyle, buttonStyle, ui);
            y += 42f * ui + 12f * ui;
            DrawPauseMusicVolume(PixelRect(new Rect(buttonX, y, buttonWidth, 52f * ui)), textStyle, ui);
            y += 52f * ui + 12f * ui;
        }

        if (GUI.Button(PixelRect(new Rect(buttonX, y, buttonWidth, buttonHeight)), "Выйти в главное меню", buttonStyle))
            ReturnToMainMenu();
    }

    private void DrawPauseMusicVolume(Rect rect, GUIStyle textStyle, float ui)
    {
        DrawPanelBacking(rect, new Color(0.012f, 0.018f, 0.024f, 0.82f), new Color(0.34f, 0.50f, 0.56f, 0.42f));

        var labelStyle = new GUIStyle(textStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(12f * ui),
            normal = { textColor = new Color(0.86f, 0.92f, 0.94f) },
        };
        PixelGui.Apply(labelStyle);

        Rect labelRect = new Rect(rect.x + 12f * ui, rect.y + 5f * ui, rect.width - 24f * ui, 18f * ui);
        Rect sliderRect = new Rect(rect.x + 12f * ui, rect.y + 27f * ui, rect.width - 24f * ui, 18f * ui);
        DrawLabelWithShadow(labelRect, $"Музыка: {Mathf.RoundToInt(GameMusic.Volume * 100f)}%", labelStyle);

        float nextVolume = GUI.HorizontalSlider(sliderRect, GameMusic.Volume, 0f, 1f);
        if (!Mathf.Approximately(nextVolume, GameMusic.Volume))
            GameMusic.Volume = nextVolume;
    }

    private void DrawPauseLightingToggle(Rect rect, GUIStyle textStyle, GUIStyle buttonStyle, float ui)
    {
        DrawPanelBacking(rect, new Color(0.012f, 0.018f, 0.024f, 0.82f), new Color(0.34f, 0.50f, 0.56f, 0.42f));

        bool shadowsEnabled = GameLightingSettings.ShadowsEnabled;
        var labelStyle = new GUIStyle(textStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(12f * ui),
            normal = { textColor = new Color(0.86f, 0.92f, 0.94f) },
        };
        PixelGui.Apply(labelStyle);

        var hintStyle = new GUIStyle(textStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Mathf.RoundToInt(10f * ui),
            normal = { textColor = new Color(0.62f, 0.72f, 0.76f) },
        };
        PixelGui.Apply(hintStyle);

        Rect labelRect = new Rect(rect.x + 12f * ui, rect.y + 4f * ui, rect.width - 138f * ui, 18f * ui);
        Rect hintRect = new Rect(rect.x + 12f * ui, rect.y + 22f * ui, rect.width - 138f * ui, 16f * ui);
        Rect buttonRect = new Rect(rect.xMax - 118f * ui, rect.y + 7f * ui, 106f * ui, 28f * ui);

        DrawLabelWithShadow(labelRect, "Тени", labelStyle);
        DrawLabelWithShadow(hintRect, shadowsEnabled ? "Киношный свет включён." : "Тени отключены.", hintStyle);

        if (GUI.Button(buttonRect, shadowsEnabled ? "Выключить" : "Включить", buttonStyle))
        {
            GameLightingSettings.NormalLighting = shadowsEnabled;
            ApplyGameplayLightingSettings();
        }
    }

    private void DrawBindingsList(Rect panel, GUIStyle textStyle, GUIStyle buttonStyle, float ui)
    {
        string binds = "WASD / стрелки - движение\nSpace / ЛКМ - атака\nE - действие / толкнуть\nQ - пульт, если выбран\nФонарь светит пассивно\nR - рестарт\nEsc - пауза";
        DrawLabelWithShadow(new Rect(panel.x + 34f * ui, panel.y + 116f * ui, panel.width - 68f * ui, 128f * ui), binds, textStyle);

        float buttonWidth = panel.width - 70f * ui;
        float buttonHeight = 38f * ui;
        float buttonX = panel.x + (panel.width - buttonWidth) * 0.5f;
        if (GUI.Button(PixelRect(new Rect(buttonX, panel.yMax - 58f * ui, buttonWidth, buttonHeight)), "Назад", buttonStyle))
            showPauseBindings = false;
    }

    private string VisibleNoteText()
    {
        if (string.IsNullOrEmpty(noteMessage))
            return string.Empty;

        int visible = Mathf.Clamp(Mathf.FloorToInt(noteMessageAge * NoteTextRevealCharactersPerSecond), 1, noteMessage.Length);
        return noteMessage.Substring(0, visible);
    }

    private string AbilitySkillHudText()
    {
        if (HasFlashlight)
            return "пассивно";
        if (!HasRemote)
            return "нет";
        if (RemoteJamActive())
            return "Активен";
        if (remoteCooldown > 0f)
            return $"КД {Mathf.CeilToInt(remoteCooldown)} с";

        return "Q готов";
    }

    private string AbilityTitle()
    {
        return equippedAbility switch
        {
            AbilitySlot.Remote => "ПУЛЬТ",
            AbilitySlot.Flashlight => "ФОНАРЬ",
            _ => "СЛОТ",
        };
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

    private void DrawHudPanel(Rect rect, Color fill, Color accent, bool scanlines)
    {
        float ui = HudScale;
        Color softAccent = new Color(accent.r, accent.g, accent.b, accent.a * 0.48f);

        DrawFilledRect(new Rect(rect.x + 3f * ui, rect.y + 4f * ui, rect.width, rect.height), new Color(0f, 0f, 0f, 0.44f));
        DrawFilledRect(rect, fill);
        DrawFilledRect(new Rect(rect.x + 2f * ui, rect.y + 2f * ui, rect.width - 4f * ui, rect.height - 4f * ui), new Color(1f, 1f, 1f, 0.018f));

        DrawFilledRect(new Rect(rect.x, rect.y, rect.width, 2f * ui), accent);
        DrawFilledRect(new Rect(rect.x, rect.yMax - 2f * ui, rect.width, 2f * ui), softAccent);
        DrawFilledRect(new Rect(rect.x, rect.y, 2f * ui, rect.height), softAccent);
        DrawFilledRect(new Rect(rect.xMax - 2f * ui, rect.y, 2f * ui, rect.height), softAccent);

        if (!scanlines)
            return;

        Color line = new Color(accent.r, accent.g, accent.b, 0.07f);
        for (float y = rect.y + 9f * ui; y < rect.yMax - 5f * ui; y += 8f * ui)
            DrawFilledRect(new Rect(rect.x + 6f * ui, y, rect.width - 12f * ui, 1f), line);
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
        SetTextStateColors(style, new Color(0f, 0f, 0f, 0.82f));
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
        SetTextStateColors(style, textColor);
        GUI.Label(rect, text, style);
    }

    private static void SetTextStateColors(GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        style.onNormal.textColor = color;
        style.onHover.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
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
        float ui = HudScale;
        Color ratingTone = RatingColor();

        Rect inner = new Rect(frameRect.x + frameRect.width * 0.34f, frameRect.y + frameRect.height * 0.13f, frameRect.width * 0.32f, frameRect.height * 0.71f);
        DrawFilledRect(inner, new Color(0.018f, 0.018f, 0.024f, 0.98f));

        float fill = inner.height * Mathf.Clamp01(viewerRating / 100f);
        if (fill > 1f)
        {
            Rect fillRect = new Rect(inner.x, inner.yMax - fill, inner.width, fill);
            DrawFilledRect(fillRect, new Color(ratingTone.r, ratingTone.g, ratingTone.b, 0.96f));
            DrawFilledRect(new Rect(fillRect.x + 1f * ui, fillRect.y, Mathf.Max(1f, fillRect.width * 0.28f), fillRect.height), new Color(1f, 1f, 1f, 0.18f));
        }

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

    private void DrawRatingLabel(Rect ratingRect, bool compact, float ui)
    {
        Texture2D frame = RatingFrameTexture();
        Rect frameRect = frame != null ? FitRectToAspect(ratingRect, VisibleTextureBounds(frame).width / Mathf.Max(1f, VisibleTextureBounds(frame).height)) : PixelRect(ratingRect);
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = Mathf.RoundToInt((compact ? 13f : 15f) * ui),
            normal = { textColor = new Color(0.78f, 0.90f, 0.94f, 0.96f) },
        };
        PixelGui.Apply(labelStyle);

        DrawLabelWithShadow(new Rect(frameRect.x - 14f * ui, frameRect.yMax + 1f * ui, frameRect.width + 28f * ui, 24f * ui), "Рейтинг", labelStyle);
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
