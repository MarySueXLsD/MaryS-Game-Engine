using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MarySGameEngine.Modules.CharacterCreation
{
    /// <summary>Partial: list/grid views for All tab and per-tab (Characters, Traits, Skills, Effects, Stats, Tags).</summary>
    public partial class CharacterCreation
    {
        private const int VIEW_BAR_HEIGHT = 32;

        private void DrawTabView(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont, int scrollOffset)
        {
            if (_currentView == "All")
            {
                DrawAllTab(spriteBatch, scissorRect, uiFont, scrollOffset);
            }
            else if (_currentView == "Characters")
            {
                if (_characters.Count > 0)
                {
                    DrawToolbar(spriteBatch, uiFont, "+ Create Character", scrollOffset);
                    int viewBarY = _toolbarBounds.Bottom + PANEL_PADDING;
                    DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
                    int listStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;
                    DrawCharacterList(spriteBatch, scissorRect, uiFont, listStartY, _allTabViewIsGrid, scrollOffset);
                }
                else
                {
                    _allTabViewListButtonBounds = Rectangle.Empty;
                    _allTabViewGridButtonBounds = Rectangle.Empty;
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "characters", "Create Character");
                }
            }
            else if (_currentView == "Traits")
            {
                if (_traits.Count > 0)
                {
                    DrawToolbar(spriteBatch, uiFont, "+ Create Trait", scrollOffset);
                    int viewBarY = _toolbarBounds.Bottom + PANEL_PADDING;
                    DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
                    int sectionStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;
                    DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionStartY, "Traits", _traits, _traitListBounds, _traitDeleteBounds, _allTabViewIsGrid, scrollOffset);
                }
                else
                {
                    _allTabViewListButtonBounds = Rectangle.Empty;
                    _allTabViewGridButtonBounds = Rectangle.Empty;
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "traits", "Create Trait");
                }
            }
            else if (_currentView == "Skills")
            {
                if (_skills.Count > 0)
                {
                    DrawToolbar(spriteBatch, uiFont, "+ Create Skill", scrollOffset);
                    int viewBarY = _toolbarBounds.Bottom + PANEL_PADDING;
                    DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
                    int sectionStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;
                    DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionStartY, "Skills", _skills, _skillListBounds, _skillDeleteBounds, _allTabViewIsGrid, scrollOffset);
                }
                else
                {
                    _allTabViewListButtonBounds = Rectangle.Empty;
                    _allTabViewGridButtonBounds = Rectangle.Empty;
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "skills", "Create Skill");
                }
            }
            else if (_currentView == "Effects")
            {
                if (_effects.Count > 0)
                {
                    DrawToolbar(spriteBatch, uiFont, "+ Create Effect", scrollOffset);
                    int viewBarY = _toolbarBounds.Bottom + PANEL_PADDING;
                    DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
                    int sectionStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;
                    DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionStartY, "Effects", _effects, _effectListBounds, _effectDeleteBounds, _allTabViewIsGrid, scrollOffset);
                }
                else
                {
                    _allTabViewListButtonBounds = Rectangle.Empty;
                    _allTabViewGridButtonBounds = Rectangle.Empty;
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "effects", "Create Effect");
                }
            }
            else if (_currentView == "Stats")
            {
                if (_stats.Count > 0)
                {
                    DrawToolbar(spriteBatch, uiFont, "+ Create Stat", scrollOffset);
                    int viewBarY = _toolbarBounds.Bottom + PANEL_PADDING;
                    DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
                    int sectionStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;
                    DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionStartY, "Stats", _stats, _statListBounds, _statDeleteBounds, _allTabViewIsGrid, scrollOffset);
                }
                else
                {
                    _allTabViewListButtonBounds = Rectangle.Empty;
                    _allTabViewGridButtonBounds = Rectangle.Empty;
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "stats", "Create Stat");
                }
            }
            else if (_currentView == "Tags")
            {
                if (_tags.Count > 0)
                {
                    DrawToolbar(spriteBatch, uiFont, "+ Create Tag", scrollOffset);
                    int viewBarY = _toolbarBounds.Bottom + PANEL_PADDING;
                    DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
                    int sectionStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;
                    DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionStartY, "Tags", _tags, _tagListBounds, _tagDeleteBounds, _allTabViewIsGrid, scrollOffset);
                }
                else
                {
                    _allTabViewListButtonBounds = Rectangle.Empty;
                    _allTabViewGridButtonBounds = Rectangle.Empty;
                    DrawEmptyTab(spriteBatch, scissorRect, uiFont, "tags", "Create Tag");
                }
            }
        }

        private void DrawAllTab(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont, int scrollOffset)
        {
            for (int i = 0; i < _allTabCreateButtonBounds.Length; i++)
                _allTabCreateButtonBounds[i] = Rectangle.Empty;

            bool hasCharacters = _characters.Count > 0;
            bool hasAnyEntities = hasCharacters || _traits.Count > 0 || _skills.Count > 0 || _effects.Count > 0 || _stats.Count > 0 || _tags.Count > 0;

            int gridStartY;
            if (hasAnyEntities)
            {
                _emptyTabCreateButtonBounds = Rectangle.Empty;
                gridStartY = _centerPanelBounds.Y + PANEL_PADDING;
            }
            else
            {
                string message = "Nothing is there";
                string subMessage = "Create your first entity to get started";
                Vector2 messageSize = uiFont.MeasureString(message);
                Vector2 subMessageSize = uiFont.MeasureString(subMessage);
                Vector2 messagePosition = new Vector2(_centerPanelBounds.X + (_centerPanelBounds.Width - messageSize.X) / 2, _centerPanelBounds.Y + PANEL_PADDING * 2);
                Vector2 subMessagePosition = new Vector2(_centerPanelBounds.X + (_centerPanelBounds.Width - subMessageSize.X) / 2, messagePosition.Y + messageSize.Y + 20);
                spriteBatch.DrawString(uiFont, message, new Vector2(messagePosition.X, messagePosition.Y - scrollOffset), TEXT_COLOR);
                spriteBatch.DrawString(uiFont, subMessage, new Vector2(subMessagePosition.X, subMessagePosition.Y - scrollOffset), TEXT_SECONDARY);
                gridStartY = (int)subMessagePosition.Y + (int)subMessageSize.Y + 40;
            }

            // Grid of 6 Create buttons (all enabled in All tab)
            int buttonWidth = 180;
            int buttonHeight = 35;
            int buttonSpacing = 15;
            int buttonsPerRow = 3;
            int totalWidth = (buttonsPerRow * buttonWidth) + ((buttonsPerRow - 1) * buttonSpacing);
            int startX = _centerPanelBounds.X + (_centerPanelBounds.Width - totalWidth) / 2;
            string[] buttonLabels = { "Create Character", "Create Trait", "Create Skill", "Create Effect", "Create Stat", "Create Tag" };
            var mousePos = _hoverMousePosition;
            for (int i = 0; i < 6; i++)
            {
                int row = i / buttonsPerRow;
                int col = i % buttonsPerRow;
                int buttonX = startX + col * (buttonWidth + buttonSpacing);
                int buttonY = gridStartY + row * (buttonHeight + buttonSpacing) - scrollOffset;
                Rectangle buttonBounds = new Rectangle(buttonX, buttonY, buttonWidth, buttonHeight);
                _allTabCreateButtonBounds[i] = buttonBounds;
                if (i == 0 && !hasAnyEntities)
                    _emptyTabCreateButtonBounds = buttonBounds;
                bool isHovered = buttonBounds.Contains(mousePos);
                Color btnColor = isHovered ? new Color(120, 90, 200) : BUTTON_ACTIVE;
                spriteBatch.Draw(_pixel, buttonBounds, btnColor);
                DrawBorder(spriteBatch, buttonBounds, new Color(btnColor.R + 30, btnColor.G + 30, btnColor.B + 30));
                Vector2 labelSize = uiFont.MeasureString(buttonLabels[i]);
                Vector2 labelPos = new Vector2(buttonBounds.X + (buttonBounds.Width - labelSize.X) / 2, buttonBounds.Y + (buttonBounds.Height - labelSize.Y) / 2);
                spriteBatch.DrawString(uiFont, buttonLabels[i], labelPos, Color.White);
            }
            int viewBarY = gridStartY + 2 * (buttonHeight + buttonSpacing) + PANEL_PADDING;
            int sectionsStartY = viewBarY + VIEW_BAR_HEIGHT + PANEL_PADDING;

            // View style bar: List | Grid (only when we have entities to show)
            if (hasAnyEntities)
                DrawViewStyleBar(spriteBatch, uiFont, viewBarY - scrollOffset);
            else
            {
                _allTabViewListButtonBounds = Rectangle.Empty;
                _allTabViewGridButtonBounds = Rectangle.Empty;
            }

            if (!hasAnyEntities)
                return;

            // Sections for each entity type that has items (use List or Grid based on view bar)
            if (hasCharacters)
            {
                const float headerBoldScale = 1.2f;
                string sectionHeader = "Characters";
                Vector2 headerSize = uiFont.MeasureString(sectionHeader) * headerBoldScale;
                int headerHeight = (int)headerSize.Y + 8;
                Rectangle headerBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, sectionsStartY - scrollOffset, _centerPanelBounds.Width - PANEL_PADDING * 2, headerHeight);
                if (IsVisible(headerBounds, scissorRect))
                {
                    spriteBatch.Draw(_pixel, headerBounds, SECTION_HEADER);
                    DrawBorder(spriteBatch, headerBounds, PANEL_BORDER);
                    Vector2 headerPos = new Vector2(headerBounds.X + 10, headerBounds.Y + (headerHeight - headerSize.Y) / 2);
                    spriteBatch.DrawString(uiFont, sectionHeader, headerPos, TEXT_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                }
                int listY = sectionsStartY + headerHeight + PANEL_PADDING;
                DrawCharacterList(spriteBatch, scissorRect, uiFont, listY, _allTabViewIsGrid, scrollOffset);
                if (_allTabViewIsGrid)
                {
                    const int cardHeight = 72;
                    const int cardSpacingY = 12;
                    int availableWidth = _centerPanelBounds.Width - PANEL_PADDING * 2;
                    int slotWidth = 220 + 12;
                    int cardsPerRow = Math.Max(1, availableWidth / slotWidth);
                    int rows = (_characters.Count + cardsPerRow - 1) / cardsPerRow;
                    sectionsStartY = listY + (rows > 0 ? rows * (cardHeight + cardSpacingY) : 0) + PANEL_PADDING;
                }
                else
                {
                    int rowHeight = 40;
                    sectionsStartY = listY + _characters.Count * (rowHeight + 4) + PANEL_PADDING;
                }
            }
            if (_traits.Count > 0) DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionsStartY, "Traits", _traits, _traitListBounds, _traitDeleteBounds, _allTabViewIsGrid, scrollOffset);
            if (_skills.Count > 0) DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionsStartY, "Skills", _skills, _skillListBounds, _skillDeleteBounds, _allTabViewIsGrid, scrollOffset);
            if (_effects.Count > 0) DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionsStartY, "Effects", _effects, _effectListBounds, _effectDeleteBounds, _allTabViewIsGrid, scrollOffset);
            if (_stats.Count > 0) DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionsStartY, "Stats", _stats, _statListBounds, _statDeleteBounds, _allTabViewIsGrid, scrollOffset);
            if (_tags.Count > 0) DrawEntityListSection(spriteBatch, scissorRect, uiFont, ref sectionsStartY, "Tags", _tags, _tagListBounds, _tagDeleteBounds, _allTabViewIsGrid, scrollOffset);
        }

        private void DrawViewStyleBar(SpriteBatch spriteBatch, SpriteFont uiFont, int barY)
        {
            int barLeft = _centerPanelBounds.X + PANEL_PADDING;
            int barWidth = _centerPanelBounds.Width - PANEL_PADDING * 2;
            Rectangle viewBarBounds = new Rectangle(barLeft, barY, barWidth, VIEW_BAR_HEIGHT);
            spriteBatch.Draw(_pixel, viewBarBounds, new Color(45, 45, 45));
            DrawBorder(spriteBatch, viewBarBounds, PANEL_BORDER);
            int listBtnWidth = 70;
            int gridBtnWidth = 70;
            int viewBtnSpacing = 8;
            int viewBtnX = barLeft + 12;
            _allTabViewListButtonBounds = new Rectangle(viewBtnX, barY + 4, listBtnWidth, VIEW_BAR_HEIGHT - 8);
            _allTabViewGridButtonBounds = new Rectangle(viewBtnX + listBtnWidth + viewBtnSpacing, barY + 4, gridBtnWidth, VIEW_BAR_HEIGHT - 8);
            var mousePos = _hoverMousePosition;
            Color listBg = _allTabViewIsGrid ? BUTTON_COLOR : BUTTON_ACTIVE;
            Color gridBg = _allTabViewIsGrid ? BUTTON_ACTIVE : BUTTON_COLOR;
            if (_allTabViewListButtonBounds.Contains(mousePos) && _allTabViewIsGrid) listBg = BUTTON_HOVER;
            if (_allTabViewGridButtonBounds.Contains(mousePos) && !_allTabViewIsGrid) gridBg = BUTTON_HOVER;
            spriteBatch.Draw(_pixel, _allTabViewListButtonBounds, listBg);
            DrawBorder(spriteBatch, _allTabViewListButtonBounds, listBg == BUTTON_ACTIVE ? new Color(180, 150, 255) : PANEL_BORDER);
            spriteBatch.Draw(_pixel, _allTabViewGridButtonBounds, gridBg);
            DrawBorder(spriteBatch, _allTabViewGridButtonBounds, gridBg == BUTTON_ACTIVE ? new Color(180, 150, 255) : PANEL_BORDER);
            Vector2 listLabelSize = uiFont.MeasureString("List");
            Vector2 gridLabelSize = uiFont.MeasureString("Grid");
            spriteBatch.DrawString(uiFont, "List", new Vector2(_allTabViewListButtonBounds.X + (listBtnWidth - listLabelSize.X) / 2, _allTabViewListButtonBounds.Y + (VIEW_BAR_HEIGHT - 8 - listLabelSize.Y) / 2), TEXT_COLOR);
            spriteBatch.DrawString(uiFont, "Grid", new Vector2(_allTabViewGridButtonBounds.X + (gridBtnWidth - gridLabelSize.X) / 2, _allTabViewGridButtonBounds.Y + (VIEW_BAR_HEIGHT - 8 - gridLabelSize.Y) / 2), TEXT_COLOR);
        }

        private void DrawEmptyTab(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont, string entityType, string buttonText)
        {
            string workspaceName = !string.IsNullOrEmpty(_currentWorkspaceName) && _currentWorkspaceName != "No workspace"
                ? _currentWorkspaceName
                : "this project";

            string messagePrefix = $"No {entityType} in the project ";
            string messageSuffix = " there yet";
            string subMessage = $"Create first one to get started";

            Vector2 prefixSize = uiFont.MeasureString(messagePrefix);
            Vector2 workspaceNameSize = uiFont.MeasureString(workspaceName);
            Vector2 suffixSize = uiFont.MeasureString(messageSuffix);
            Vector2 subMessageSize = uiFont.MeasureString(subMessage);

            float boldScale = 1.1f;
            float totalMessageWidth = prefixSize.X + (workspaceNameSize.X * boldScale) + suffixSize.X;

            Vector2 messagePosition = new Vector2(
                _centerPanelBounds.X + (_centerPanelBounds.Width - totalMessageWidth) / 2,
                _centerPanelBounds.Y + (_centerPanelBounds.Height - prefixSize.Y) / 2 - 30
            );

            Vector2 currentPos = messagePosition;
            spriteBatch.DrawString(uiFont, messagePrefix, currentPos, TEXT_COLOR);
            currentPos.X += prefixSize.X;

            Vector2 workspaceNamePos = new Vector2(
                currentPos.X,
                currentPos.Y - (workspaceNameSize.Y * (boldScale - 1.0f)) / 2
            );
            spriteBatch.DrawString(uiFont, workspaceName, workspaceNamePos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, boldScale, SpriteEffects.None, 0f);
            currentPos.X += workspaceNameSize.X * boldScale;

            spriteBatch.DrawString(uiFont, messageSuffix, currentPos, TEXT_COLOR);

            Vector2 subMessagePosition = new Vector2(
                _centerPanelBounds.X + (_centerPanelBounds.Width - subMessageSize.X) / 2,
                messagePosition.Y + prefixSize.Y + 20
            );

            spriteBatch.DrawString(uiFont, subMessage, subMessagePosition, TEXT_SECONDARY);

            int buttonY = (int)subMessagePosition.Y + (int)subMessageSize.Y + 30;
            int buttonX = _centerPanelBounds.X + (_centerPanelBounds.Width - 200) / 2;

            bool enableCreateButton = _currentView == "Characters" || _currentView == "Traits" || _currentView == "Skills"
                || _currentView == "Effects" || _currentView == "Stats" || _currentView == "Tags";
            if (enableCreateButton)
            {
                _emptyTabCreateButtonBounds = new Rectangle(buttonX, buttonY, 200, 35);
                var mousePos = _hoverMousePosition;
                bool isHovered = _emptyTabCreateButtonBounds.Contains(mousePos);
                Color btnColor = isHovered ? new Color(120, 90, 200) : BUTTON_ACTIVE;
                spriteBatch.Draw(_pixel, _emptyTabCreateButtonBounds, btnColor);
                DrawBorder(spriteBatch, _emptyTabCreateButtonBounds, new Color(btnColor.R + 30, btnColor.G + 30, btnColor.B + 30));

                Vector2 btnTextSize = uiFont.MeasureString(buttonText);
                Vector2 btnTextPos = new Vector2(
                    _emptyTabCreateButtonBounds.X + (_emptyTabCreateButtonBounds.Width - btnTextSize.X) / 2,
                    _emptyTabCreateButtonBounds.Y + (_emptyTabCreateButtonBounds.Height - btnTextSize.Y) / 2
                );
                spriteBatch.DrawString(uiFont, buttonText, btnTextPos, Color.White);
            }
            else
            {
                _emptyTabCreateButtonBounds = Rectangle.Empty;
                DrawDisabledButton(spriteBatch, buttonText, buttonX, buttonY, 200, uiFont);
            }
        }

        private void DrawToolbar(SpriteBatch spriteBatch, SpriteFont uiFont, string buttonLabel, int scrollOffset = 0)
        {
            int drawY = _toolbarBounds.Y - scrollOffset;
            var toolbarDrawBounds = new Rectangle(_toolbarBounds.X, drawY, _toolbarBounds.Width, _toolbarBounds.Height);
            spriteBatch.Draw(_pixel, toolbarDrawBounds, new Color(35, 35, 35));

            spriteBatch.Draw(_pixel, new Rectangle(
                _toolbarBounds.X, drawY + _toolbarBounds.Height - 1,
                _toolbarBounds.Width, 1
            ), PANEL_BORDER);

            int createBtnHeight = 30;
            int createBtnY = drawY + (TOOLBAR_HEIGHT - createBtnHeight) / 2;
            var createButtonDrawBounds = new Rectangle(_toolbarBounds.X + PANEL_PADDING, createBtnY, 200, createBtnHeight);
            _createCharacterButtonBounds = createButtonDrawBounds;

            var mousePos = _hoverMousePosition;
            bool isHovered = createButtonDrawBounds.Contains(mousePos);
            Color btnColor = isHovered ? new Color(120, 90, 200) : BUTTON_ACTIVE;
            spriteBatch.Draw(_pixel, createButtonDrawBounds, btnColor);
            DrawBorder(spriteBatch, createButtonDrawBounds, new Color(btnColor.R + 30, btnColor.G + 30, btnColor.B + 30));

            Vector2 labelSize = uiFont.MeasureString(buttonLabel);
            Vector2 labelPos = new Vector2(
                createButtonDrawBounds.X + (createButtonDrawBounds.Width - labelSize.X) / 2,
                createButtonDrawBounds.Y + (createButtonDrawBounds.Height - labelSize.Y) / 2
            );
            spriteBatch.DrawString(uiFont, buttonLabel, labelPos, Color.White);
        }

        private void DrawCharacterList(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont, int startY, bool asGrid = false, int scrollOffset = 0)
        {
            _characterListBounds.Clear();
            _characterDeleteButtonBounds.Clear();
            int padding = PANEL_PADDING;
            var mousePos = _hoverMousePosition;
            int drawY = startY - scrollOffset;

            if (asGrid)
            {
                const int cardWidth = 220;
                const int cardHeight = 72;
                const int cardSpacingX = 12;
                const int cardSpacingY = 12;
                int deleteBtnSize = 28;
                int availableWidth = _centerPanelBounds.Width - padding * 2;
                int slotWidth = cardWidth + cardSpacingX;
                int cardsPerRow = Math.Max(1, availableWidth / slotWidth);
                int x = _centerPanelBounds.X + padding;
                int y = drawY;
                int col = 0;
                foreach (string id in _characters)
                {
                    string name = _characterNames.TryGetValue(id, out string n) ? n : id;
                    string tags = _characterTags.TryGetValue(id, out string t) ? t : "";
                    string displayLine = string.IsNullOrWhiteSpace(tags) ? $"{id}: {name}" : $"{id}: {name}";
                    if (displayLine.Length > 24) displayLine = displayLine.Substring(0, 21) + "...";
                    Rectangle cardBounds = new Rectangle(x, y, cardWidth, cardHeight);
                    _characterListBounds.Add(cardBounds);
                    Rectangle deleteBounds = new Rectangle(cardBounds.Right - deleteBtnSize - 6, cardBounds.Y + 6, deleteBtnSize, deleteBtnSize);
                    _characterDeleteButtonBounds.Add(deleteBounds);
                    if (IsVisible(cardBounds, scissorRect))
                    {
                        bool cardHover = cardBounds.Contains(mousePos) && !deleteBounds.Contains(mousePos);
                        Color bg = cardHover ? BUTTON_HOVER : BUTTON_COLOR;
                        spriteBatch.Draw(_pixel, cardBounds, bg);
                        DrawBorder(spriteBatch, cardBounds, PANEL_BORDER);
                        Vector2 textPos = new Vector2(cardBounds.X + 10, cardBounds.Y + (cardHeight - uiFont.MeasureString(displayLine).Y) / 2);
                        spriteBatch.DrawString(uiFont, displayLine, textPos, TEXT_COLOR);
                        bool delHover = deleteBounds.Contains(mousePos);
                        spriteBatch.Draw(_pixel, deleteBounds, delHover ? new Color(180, 60, 60) : new Color(120, 40, 40));
                        DrawBorder(spriteBatch, deleteBounds, PANEL_BORDER);
                        Vector2 delSize = uiFont.MeasureString("X");
                        spriteBatch.DrawString(uiFont, "X", new Vector2(deleteBounds.X + (deleteBtnSize - delSize.X) / 2, deleteBounds.Y + (deleteBtnSize - delSize.Y) / 2), TEXT_COLOR);
                    }
                    col++;
                    if (col >= cardsPerRow)
                    {
                        col = 0;
                        x = _centerPanelBounds.X + padding;
                        y += cardHeight + cardSpacingY;
                    }
                    else
                    {
                        x += cardWidth + cardSpacingX;
                    }
                }
                return;
            }

            int rowHeight = 40;
            int deleteBtnWidth = 80;
            int deleteBtnMargin = 8;
            int yPos = drawY;
            foreach (string id in _characters)
            {
                string name = _characterNames.TryGetValue(id, out string n) ? n : id;
                string tags = _characterTags.TryGetValue(id, out string t) ? t : "";
                string displayLine = string.IsNullOrWhiteSpace(tags) ? $"{id}: {name}" : $"{id}: {name} - {tags}";
                Rectangle rowBounds = new Rectangle(
                    _centerPanelBounds.X + padding,
                    yPos,
                    _centerPanelBounds.Width - padding * 2,
                    rowHeight
                );
                _characterListBounds.Add(rowBounds);
                Rectangle deleteBounds = new Rectangle(
                    rowBounds.Right - deleteBtnWidth - deleteBtnMargin,
                    rowBounds.Y + (rowHeight - 28) / 2,
                    deleteBtnWidth,
                    28
                );
                _characterDeleteButtonBounds.Add(deleteBounds);
                if (IsVisible(rowBounds, scissorRect))
                {
                    bool rowHover = rowBounds.Contains(mousePos) && !deleteBounds.Contains(mousePos);
                    Color bg = rowHover ? BUTTON_HOVER : BUTTON_COLOR;
                    spriteBatch.Draw(_pixel, rowBounds, bg);
                    DrawBorder(spriteBatch, rowBounds, PANEL_BORDER);
                    Vector2 textPos = new Vector2(rowBounds.X + 12, rowBounds.Y + (rowHeight - uiFont.MeasureString(displayLine).Y) / 2);
                    spriteBatch.DrawString(uiFont, displayLine, textPos, TEXT_COLOR);
                    bool deleteHover = deleteBounds.Contains(mousePos);
                    Color deleteBg = deleteHover ? new Color(180, 60, 60) : new Color(120, 40, 40);
                    spriteBatch.Draw(_pixel, deleteBounds, deleteBg);
                    DrawBorder(spriteBatch, deleteBounds, PANEL_BORDER);
                    string deleteLabel = "Delete";
                    Vector2 deleteTextPos = new Vector2(
                        deleteBounds.X + (deleteBounds.Width - uiFont.MeasureString(deleteLabel).X) / 2,
                        deleteBounds.Y + (deleteBounds.Height - uiFont.MeasureString(deleteLabel).Y) / 2
                    );
                    spriteBatch.DrawString(uiFont, deleteLabel, deleteTextPos, TEXT_COLOR);
                }
                yPos += rowHeight + 4;
            }
        }

        private void DrawCharactersListTab(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont)
        {
            int padding = PANEL_PADDING;
            DrawCharacterList(spriteBatch, scissorRect, uiFont, _toolbarBounds.Bottom + padding);
        }

        private void DrawEntityListSection(SpriteBatch spriteBatch, Rectangle scissorRect, SpriteFont uiFont, ref int startY, string sectionTitle, List<string> items, List<Rectangle> rowBounds, List<Rectangle> deleteBounds, bool asGrid = false, int scrollOffset = 0)
        {
            rowBounds.Clear();
            deleteBounds.Clear();
            const float headerBoldScale = 1.2f;
            int padding = PANEL_PADDING;
            Vector2 headerSize = uiFont.MeasureString(sectionTitle) * headerBoldScale;
            int headerHeight = (int)headerSize.Y + 8;
            Rectangle headerBounds = new Rectangle(_centerPanelBounds.X + padding, startY - scrollOffset, _centerPanelBounds.Width - padding * 2, headerHeight);
            if (IsVisible(headerBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, headerBounds, SECTION_HEADER);
                DrawBorder(spriteBatch, headerBounds, PANEL_BORDER);
                Vector2 headerPos = new Vector2(headerBounds.X + 10, headerBounds.Y + (headerHeight - headerSize.Y) / 2);
                spriteBatch.DrawString(uiFont, sectionTitle, headerPos, TEXT_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
            }
            startY += headerHeight + padding;
            var mousePos = _hoverMousePosition;
            int drawY = startY - scrollOffset;

            if (asGrid)
            {
                const int cardWidth = 220;
                const int cardHeight = 72;
                const int cardSpacingX = 12;
                const int cardSpacingY = 12;
                int deleteBtnSize = 28;
                int availableWidth = _centerPanelBounds.Width - padding * 2;
                int slotWidth = cardWidth + cardSpacingX;
                int cardsPerRow = Math.Max(1, availableWidth / slotWidth);
                int x = _centerPanelBounds.X + padding;
                int y = drawY;
                int col = 0;
                foreach (string name in items)
                {
                    string displayName = name.Length > 24 ? name.Substring(0, 21) + "..." : name;
                    Rectangle cardBounds = new Rectangle(x, y, cardWidth, cardHeight);
                    rowBounds.Add(cardBounds);
                    Rectangle delRect = new Rectangle(cardBounds.Right - deleteBtnSize - 6, cardBounds.Y + 6, deleteBtnSize, deleteBtnSize);
                    deleteBounds.Add(delRect);
                    if (IsVisible(cardBounds, scissorRect))
                    {
                        bool cardHover = cardBounds.Contains(mousePos) && !delRect.Contains(mousePos);
                        Color bg = cardHover ? BUTTON_HOVER : BUTTON_COLOR;
                        spriteBatch.Draw(_pixel, cardBounds, bg);
                        DrawBorder(spriteBatch, cardBounds, PANEL_BORDER);
                        Vector2 textPos = new Vector2(cardBounds.X + 10, cardBounds.Y + (cardHeight - uiFont.MeasureString(displayName).Y) / 2);
                        spriteBatch.DrawString(uiFont, displayName, textPos, TEXT_COLOR);
                        bool delHover = delRect.Contains(mousePos);
                        spriteBatch.Draw(_pixel, delRect, delHover ? new Color(180, 60, 60) : new Color(120, 40, 40));
                        DrawBorder(spriteBatch, delRect, PANEL_BORDER);
                        Vector2 delSize = uiFont.MeasureString("X");
                        spriteBatch.DrawString(uiFont, "X", new Vector2(delRect.X + (deleteBtnSize - delSize.X) / 2, delRect.Y + (deleteBtnSize - delSize.Y) / 2), TEXT_COLOR);
                    }
                    col++;
                    if (col >= cardsPerRow)
                    {
                        col = 0;
                        x = _centerPanelBounds.X + padding;
                        y += cardHeight + cardSpacingY;
                    }
                    else
                    {
                        x += cardWidth + cardSpacingX;
                    }
                }
                if (items.Count > 0)
                {
                    int rows = (items.Count + cardsPerRow - 1) / cardsPerRow;
                    startY += rows * (cardHeight + cardSpacingY) + padding;
                }
                return;
            }

            int rowHeight = 40;
            int deleteBtnWidth = 80;
            int deleteBtnMargin = 8;
            int yPos = drawY;
            foreach (string name in items)
            {
                Rectangle rowRect = new Rectangle(_centerPanelBounds.X + padding, yPos, _centerPanelBounds.Width - padding * 2, rowHeight);
                rowBounds.Add(rowRect);
                Rectangle delRect = new Rectangle(rowRect.Right - deleteBtnWidth - deleteBtnMargin, rowRect.Y + (rowHeight - 28) / 2, deleteBtnWidth, 28);
                deleteBounds.Add(delRect);
                if (IsVisible(rowRect, scissorRect))
                {
                    bool rowHover = rowRect.Contains(mousePos) && !delRect.Contains(mousePos);
                    Color bg = rowHover ? BUTTON_HOVER : BUTTON_COLOR;
                    spriteBatch.Draw(_pixel, rowRect, bg);
                    DrawBorder(spriteBatch, rowRect, PANEL_BORDER);
                    Vector2 textPos = new Vector2(rowRect.X + 12, rowRect.Y + (rowHeight - uiFont.MeasureString(name).Y) / 2);
                    spriteBatch.DrawString(uiFont, name, textPos, TEXT_COLOR);
                    bool deleteHover = delRect.Contains(mousePos);
                    Color deleteBg = deleteHover ? new Color(180, 60, 60) : new Color(120, 40, 40);
                    spriteBatch.Draw(_pixel, delRect, deleteBg);
                    DrawBorder(spriteBatch, delRect, PANEL_BORDER);
                    string deleteLabel = "Delete";
                    Vector2 deleteTextPos = new Vector2(delRect.X + (delRect.Width - uiFont.MeasureString(deleteLabel).X) / 2, delRect.Y + (delRect.Height - uiFont.MeasureString(deleteLabel).Y) / 2);
                    spriteBatch.DrawString(uiFont, deleteLabel, deleteTextPos, TEXT_COLOR);
                }
                yPos += rowHeight + 4;
                startY += rowHeight + 4;
            }
            startY += padding;
        }

        private void DrawDisabledButton(SpriteBatch spriteBatch, string text, int x, int y, int width, SpriteFont font)
        {
            Rectangle buttonBounds = new Rectangle(x, y, width, 35);
            Color disabledColor = new Color(BUTTON_COLOR.R / 2, BUTTON_COLOR.G / 2, BUTTON_COLOR.B / 2);
            spriteBatch.Draw(_pixel, buttonBounds, disabledColor);
            DrawBorder(spriteBatch, buttonBounds, new Color(PANEL_BORDER.R / 2, PANEL_BORDER.G / 2, PANEL_BORDER.B / 2));

            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(
                buttonBounds.X + (buttonBounds.Width - textSize.X) / 2,
                buttonBounds.Y + (buttonBounds.Height - textSize.Y) / 2
            );
            spriteBatch.DrawString(font, text, textPos, new Color(TEXT_COLOR.R / 2, TEXT_COLOR.G / 2, TEXT_COLOR.B / 2));
        }

        /// <summary>Computes total content height for the list/grid view (All, Characters, Traits, etc.) for scrolling.</summary>
        private int CalculateListGridContentHeight()
        {
            const int sectionHeaderHeight = 32;
            const int buttonWidth = 180;
            const int buttonHeight = 35;
            const int buttonSpacing = 15;
            const int cardHeight = 72;
            const int cardSpacingY = 12;
            const int listRowHeight = 44;
            int padding = PANEL_PADDING;
            int centerWidth = Math.Max(1, _centerPanelBounds.Width - padding * 2);
            int slotWidth = 220 + 12;
            int cardsPerRow = Math.Max(1, centerWidth / slotWidth);

            if (_currentView == "All")
            {
                bool hasCharacters = _characters.Count > 0;
                bool hasAnyEntities = hasCharacters || _traits.Count > 0 || _skills.Count > 0 || _effects.Count > 0 || _stats.Count > 0 || _tags.Count > 0;
                if (!hasAnyEntities)
                    return _centerPanelBounds.Height;
                int gridStartY = _centerPanelBounds.Y + padding;
                int buttonsHeight = 2 * (buttonHeight + buttonSpacing) - buttonSpacing;
                int viewBarY = gridStartY + buttonsHeight + padding;
                int sectionsStartY = viewBarY + VIEW_BAR_HEIGHT + padding;
                int contentBottom = sectionsStartY;

                if (hasCharacters)
                {
                    int listY = sectionsStartY + sectionHeaderHeight + padding;
                    if (_allTabViewIsGrid)
                    {
                        int rows = (_characters.Count + cardsPerRow - 1) / cardsPerRow;
                        contentBottom = listY + (rows > 0 ? rows * (cardHeight + cardSpacingY) : 0) + padding;
                    }
                    else
                        contentBottom = listY + _characters.Count * listRowHeight + padding;
                    sectionsStartY = contentBottom;
                }
                if (_traits.Count > 0)
                {
                    int listY = sectionsStartY + sectionHeaderHeight + padding;
                    if (_allTabViewIsGrid)
                    {
                        int rows = (_traits.Count + cardsPerRow - 1) / cardsPerRow;
                        contentBottom = listY + rows * (cardHeight + cardSpacingY) + padding;
                    }
                    else
                        contentBottom = listY + _traits.Count * listRowHeight + padding;
                    sectionsStartY = contentBottom;
                }
                if (_skills.Count > 0)
                {
                    int listY = sectionsStartY + sectionHeaderHeight + padding;
                    if (_allTabViewIsGrid)
                    {
                        int rows = (_skills.Count + cardsPerRow - 1) / cardsPerRow;
                        contentBottom = listY + rows * (cardHeight + cardSpacingY) + padding;
                    }
                    else
                        contentBottom = listY + _skills.Count * listRowHeight + padding;
                    sectionsStartY = contentBottom;
                }
                if (_effects.Count > 0)
                {
                    int listY = sectionsStartY + sectionHeaderHeight + padding;
                    if (_allTabViewIsGrid)
                    {
                        int rows = (_effects.Count + cardsPerRow - 1) / cardsPerRow;
                        contentBottom = listY + rows * (cardHeight + cardSpacingY) + padding;
                    }
                    else
                        contentBottom = listY + _effects.Count * listRowHeight + padding;
                    sectionsStartY = contentBottom;
                }
                if (_stats.Count > 0)
                {
                    int listY = sectionsStartY + sectionHeaderHeight + padding;
                    if (_allTabViewIsGrid)
                    {
                        int rows = (_stats.Count + cardsPerRow - 1) / cardsPerRow;
                        contentBottom = listY + rows * (cardHeight + cardSpacingY) + padding;
                    }
                    else
                        contentBottom = listY + _stats.Count * listRowHeight + padding;
                    sectionsStartY = contentBottom;
                }
                if (_tags.Count > 0)
                {
                    int listY = sectionsStartY + sectionHeaderHeight + padding;
                    if (_allTabViewIsGrid)
                    {
                        int rows = (_tags.Count + cardsPerRow - 1) / cardsPerRow;
                        contentBottom = listY + rows * (cardHeight + cardSpacingY) + padding;
                    }
                    else
                        contentBottom = listY + _tags.Count * listRowHeight + padding;
                }
                return contentBottom - _centerPanelBounds.Y + padding;
            }

            if (_currentView == "Characters" && _characters.Count > 0)
            {
                int listStartY = _centerPanelBounds.Y + TOOLBAR_HEIGHT + padding + VIEW_BAR_HEIGHT + padding;
                if (_allTabViewIsGrid)
                {
                    int rows = (_characters.Count + cardsPerRow - 1) / cardsPerRow;
                    return listStartY - _centerPanelBounds.Y + rows * (cardHeight + cardSpacingY) + padding;
                }
                return listStartY - _centerPanelBounds.Y + _characters.Count * listRowHeight + padding;
            }

            if ((_currentView == "Traits" && _traits.Count > 0) || (_currentView == "Skills" && _skills.Count > 0) ||
                (_currentView == "Effects" && _effects.Count > 0) || (_currentView == "Stats" && _stats.Count > 0) || (_currentView == "Tags" && _tags.Count > 0))
            {
                int sectionStartY = _centerPanelBounds.Y + TOOLBAR_HEIGHT + padding + VIEW_BAR_HEIGHT + padding;
                int count = _currentView == "Traits" ? _traits.Count : _currentView == "Skills" ? _skills.Count :
                    _currentView == "Effects" ? _effects.Count : _currentView == "Stats" ? _stats.Count : _tags.Count;
                int listY = sectionStartY + sectionHeaderHeight + padding;
                if (_allTabViewIsGrid)
                {
                    int rows = (count + cardsPerRow - 1) / cardsPerRow;
                    return listY - _centerPanelBounds.Y + rows * (cardHeight + cardSpacingY) + padding;
                }
                return listY - _centerPanelBounds.Y + count * listRowHeight + padding;
            }

            return _centerPanelBounds.Height;
        }
    }
}
