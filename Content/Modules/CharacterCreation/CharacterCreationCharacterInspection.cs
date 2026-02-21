using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MarySGameEngine.Modules.CharacterCreation
{
    /// <summary>Partial: character inspection view (center panel when a character is selected).</summary>
    public partial class CharacterCreation
    {
        /// <summary>
        /// Updates hit-test bounds for Back, Edit ID/Name, Confirm, and input rectangles when in character inspection.
        /// HandleInput() uses current layout and scroll. Without this, bounds are only set in Draw() and can be one frame stale.
        /// </summary>
        private void UpdateInspectionHitTestBounds()
        {
            if (!_isInspectingCharacter || _nameInputBounds.Width <= 0 || _nameInputBounds.Height <= 0)
                return;

            SpriteFont uiFont = _uiFont ?? _menuFont;
            if (uiFont == null)
                return;

            int scrollY = -_scrollY;
            int rowY = _nameInputBounds.Y + scrollY;
            int rowH = _nameInputBounds.Height;
            const float headerBoldScale = 1.2f;
            const int idToNameGap = 12;

            string backLabel = "BACK";
            Vector2 backLabelSize = uiFont.MeasureString(backLabel);
            int backBtnPadH = 16;
            int backBtnPadV = 6;
            int backBtnW = (int)backLabelSize.X + backBtnPadH;
            int backBtnH = (int)backLabelSize.Y + backBtnPadV;
            int backBtnX = _nameInputBounds.X + 10;
            int backBtnY = rowY + (rowH - backBtnH) / 2;
            _backButtonBounds = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);

            int leftMargin = 10 + backBtnW + 8;
            int idX = _nameInputBounds.X + leftMargin;
            Vector2 idHeaderSize = uiFont.MeasureString("ID:");
            int idHeaderW = (int)(idHeaderSize.X * headerBoldScale) + 5;

            Vector2 nameHeaderSize = uiFont.MeasureString("Name:");
            int nameHeaderW = (int)(nameHeaderSize.X * headerBoldScale) + 5;
            int confirmBtnWForLayout = (int)(uiFont.MeasureString("Confirm").X) + 16 + 16;
            int nameBlockRightMax = _nameInputBounds.X + _nameInputBounds.Width - 10;

            int idContentRight;
            if (!_isEditingId)
            {
                int idValX = idX + 10 + idHeaderW + 8;
                Vector2 idValSize = uiFont.MeasureString(_characterId);
                string editLabel = "Edit";
                Vector2 editLabelSize = uiFont.MeasureString(editLabel);
                int editBtnPadH = 16;
                int editBtnPadV = 6;
                int editBtnW = (int)editLabelSize.X + editBtnPadH;
                int editBtnH = (int)editLabelSize.Y + editBtnPadV;
                int editBtnX = idValX + (int)idValSize.X + 10;
                int editBtnY = rowY + (rowH - editBtnH) / 2;
                _idEditButtonBounds = new Rectangle(editBtnX, editBtnY, editBtnW, editBtnH);
                _idTextBounds = Rectangle.Empty;
                _idConfirmButtonBounds = Rectangle.Empty;
                idContentRight = _idEditButtonBounds.Right;
            }
            else
            {
                Vector2 confirmSize = uiFont.MeasureString("Confirm");
                int confirmPadH = 16;
                int confirmPadV = 6;
                int confirmBtnW = (int)confirmSize.X + confirmPadH;
                int confirmBtnH = (int)confirmSize.Y + confirmPadV;
                int inputX = idX + 10 + idHeaderW + 8;
                int idBlockRight = nameBlockRightMax - idToNameGap;
                int maxIdInputW = Math.Max(0, idBlockRight - inputX - 8 - confirmBtnW);
                int minIdInputWidthFromText = (int)uiFont.MeasureString(_characterId).X + 24;
                int idBlockAvailableW = idBlockRight - idX - idHeaderW - confirmBtnW - 30;
                int preferredIdInputW = Math.Max(MIN_INPUT_WIDTH_ID, Math.Max(minIdInputWidthFromText, idBlockAvailableW / 4));
                int inputW = Math.Min(preferredIdInputW, maxIdInputW);
                int confirmBtnX = inputX + inputW + 8;
                int confirmBtnY = rowY + (rowH - confirmBtnH) / 2;
                int inputH = rowH - 4;
                int inputY = rowY + 2;
                _idTextBounds = new Rectangle(inputX, inputY, inputW, inputH);
                _idConfirmButtonBounds = new Rectangle(confirmBtnX, confirmBtnY, confirmBtnW, confirmBtnH);
                _idEditButtonBounds = Rectangle.Empty;
                idContentRight = _idConfirmButtonBounds.Right;
            }

            int nameBlockX = idContentRight + idToNameGap;
            int nameBlockW = Math.Max(0, nameBlockRightMax - nameBlockX);

            if (!_isEditingName)
            {
                int nameValX = nameBlockX + 10 + nameHeaderW + 8;
                Vector2 nameValSize = uiFont.MeasureString(_characterName);
                string editLabel = "Edit";
                Vector2 editLabelSize = uiFont.MeasureString(editLabel);
                int editBtnPadH = 16;
                int editBtnPadV = 6;
                int editBtnW = (int)editLabelSize.X + editBtnPadH;
                int editBtnH = (int)editLabelSize.Y + editBtnPadV;
                int editBtnX = nameValX + (int)nameValSize.X + 10;
                int editBtnY = rowY + (rowH - editBtnH) / 2;
                _nameEditButtonBounds = new Rectangle(editBtnX, editBtnY, editBtnW, editBtnH);
                _nameTextBounds = Rectangle.Empty;
                _nameConfirmButtonBounds = Rectangle.Empty;
            }
            else
            {
                Vector2 confirmSize = uiFont.MeasureString("Confirm");
                int confirmPadH = 16;
                int confirmPadV = 6;
                int confirmBtnW = (int)confirmSize.X + confirmPadH;
                int confirmBtnH = (int)confirmSize.Y + confirmPadV;
                int inputX = nameBlockX + 10 + nameHeaderW + 8;
                int nameBlockRight = nameBlockX + nameBlockW - 4;
                int maxNameInputW = Math.Max(0, nameBlockRight - inputX - 8 - confirmBtnW);
                int preferredNameInputW = Math.Max(MIN_INPUT_WIDTH_NAME, nameBlockW - nameHeaderW - confirmBtnW - 30);
                int inputW = Math.Min(preferredNameInputW, maxNameInputW);
                int confirmBtnX = inputX + inputW + 8;
                int confirmBtnY = rowY + (rowH - confirmBtnH) / 2;
                int inputH = rowH - 4;
                int inputY = rowY + 2;
                _nameTextBounds = new Rectangle(inputX, inputY, inputW, inputH);
                _nameConfirmButtonBounds = new Rectangle(confirmBtnX, confirmBtnY, confirmBtnW, confirmBtnH);
                _nameEditButtonBounds = Rectangle.Empty;
            }
        }

        private void DrawCharacterInspectionView(SpriteBatch spriteBatch, int scrollOffset, Rectangle scissorRect, SpriteFont uiFont)
        {
            int scrollY = -scrollOffset;

            int rowVisibleWidth = Math.Min(_nameInputBounds.Width, Math.Max(0, scissorRect.Right - _nameInputBounds.X));
            Rectangle nameBounds = new Rectangle(_nameInputBounds.X, _nameInputBounds.Y + scrollY, rowVisibleWidth, _nameInputBounds.Height);
            if (IsVisible(nameBounds, scissorRect))
            {
                int rowY = nameBounds.Y;
                int rowH = nameBounds.Height;
                const float headerBoldScale = 1.2f;
                const int idToNameGap = 12;

                string backLabel = "BACK";
                Vector2 backLabelSize = uiFont.MeasureString(backLabel);
                int backBtnPadH = 16;
                int backBtnPadV = 6;
                int backBtnW = (int)backLabelSize.X + backBtnPadH;
                int backBtnH = (int)backLabelSize.Y + backBtnPadV;
                int backBtnX = nameBounds.X + 10;
                int backBtnY = rowY + (rowH - backBtnH) / 2;
                Rectangle backBtnRect = new Rectangle(backBtnX, backBtnY, backBtnW, backBtnH);
                _backButtonBounds = backBtnRect;
                bool backHover = backBtnRect.Contains(_hoverMousePosition);
                spriteBatch.Draw(_pixel, backBtnRect, backHover ? BUTTON_HOVER : BUTTON_COLOR);
                DrawBorder(spriteBatch, backBtnRect, SEARCH_BORDER);
                spriteBatch.DrawString(uiFont, backLabel, new Vector2(backBtnRect.X + (backBtnW - backLabelSize.X) / 2, backBtnRect.Y + (backBtnH - backLabelSize.Y) / 2), TEXT_COLOR);

                int leftMargin = 10 + backBtnW + 8;
                int idX = nameBounds.X + leftMargin;
                string idHeader = "ID:";
                Vector2 idHeaderSize = uiFont.MeasureString(idHeader);
                int idHeaderW = (int)(idHeaderSize.X * headerBoldScale) + 5;

                int nameBlockX;
                int nameBlockW;
                string nameHeader = "Name:";
                Vector2 nameHeaderSize = uiFont.MeasureString(nameHeader);
                int nameHeaderW = (int)(nameHeaderSize.X * headerBoldScale) + 5;
                int nameBlockRightMax = nameBounds.X + nameBounds.Width - 10;

                Vector2 idHeaderPos = new Vector2(idX + 10, rowY + (rowH - idHeaderSize.Y * headerBoldScale) / 2);
                spriteBatch.DrawString(uiFont, idHeader, idHeaderPos, TEXT_SECONDARY, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);

                int idContentRight;
                if (!_isEditingId)
                {
                    int idValX = idX + 10 + idHeaderW + 8;
                    Vector2 idValSize = uiFont.MeasureString(_characterId);
                    spriteBatch.DrawString(uiFont, _characterId, new Vector2(idValX, rowY + (rowH - idValSize.Y) / 2), TEXT_COLOR);
                    string editLabel = "Edit";
                    Vector2 editLabelSize = uiFont.MeasureString(editLabel);
                    int editBtnPadH = 16;
                    int editBtnPadV = 6;
                    int editBtnW = (int)editLabelSize.X + editBtnPadH;
                    int editBtnH = (int)editLabelSize.Y + editBtnPadV;
                    int editBtnX = idValX + (int)idValSize.X + 10;
                    int editBtnY = rowY + (rowH - editBtnH) / 2;
                    Rectangle editBtnRect = new Rectangle(editBtnX, editBtnY, editBtnW, editBtnH);
                    _idEditButtonBounds = editBtnRect;
                    bool idEditDisabled = _isEditingName;
                    bool editHover = !idEditDisabled && editBtnRect.Contains(_hoverMousePosition);
                    Color idEditBg = idEditDisabled ? new Color(BUTTON_COLOR.R / 2, BUTTON_COLOR.G / 2, BUTTON_COLOR.B / 2) : (editHover ? BUTTON_HOVER : BUTTON_COLOR);
                    Color idEditBorder = idEditDisabled ? new Color(SEARCH_BORDER.R / 2, SEARCH_BORDER.G / 2, SEARCH_BORDER.B / 2) : SEARCH_BORDER;
                    Color idEditText = idEditDisabled ? TEXT_SECONDARY : TEXT_COLOR;
                    spriteBatch.Draw(_pixel, editBtnRect, idEditBg);
                    DrawBorder(spriteBatch, editBtnRect, idEditBorder);
                    spriteBatch.DrawString(uiFont, editLabel, new Vector2(editBtnRect.X + (editBtnW - editLabelSize.X) / 2, editBtnRect.Y + (editBtnH - editLabelSize.Y) / 2), idEditText);
                    idContentRight = editBtnRect.Right;
                }
                else
                {
                    string confirmLabel = "Confirm";
                    Vector2 confirmSize = uiFont.MeasureString(confirmLabel);
                    int confirmPadH = 16;
                    int confirmPadV = 6;
                    int confirmBtnW = (int)confirmSize.X + confirmPadH;
                    int confirmBtnH = (int)confirmSize.Y + confirmPadV;
                    int inputX = idX + 10 + idHeaderW + 8;
                    int idBlockRight = nameBlockRightMax - idToNameGap;
                    int maxIdInputW = Math.Max(0, idBlockRight - inputX - 8 - confirmBtnW);
                    int minIdInputWidthFromText = (int)uiFont.MeasureString(_characterId).X + 24;
                    int idBlockAvailableW = idBlockRight - idX - idHeaderW - confirmBtnW - 30;
                    int preferredIdInputW = Math.Max(MIN_INPUT_WIDTH_ID, Math.Max(minIdInputWidthFromText, idBlockAvailableW / 4));
                    int inputW = Math.Min(preferredIdInputW, maxIdInputW);
                    int confirmBtnX = inputX + inputW + 8;
                    int confirmBtnY = rowY + (rowH - confirmBtnH) / 2;
                    int inputH = rowH - 4;
                    int inputY = rowY + 2;
                    Rectangle inputRect = new Rectangle(inputX, inputY, inputW, inputH);
                    _idTextBounds = inputRect;
                    spriteBatch.Draw(_pixel, inputRect, new Color(25, 25, 25));
                    DrawBorder(spriteBatch, inputRect, new Color(147, 112, 219));
                    Rectangle idClip = Rectangle.Intersect(scissorRect, inputRect);
                    if (idClip.Width > 0 && idClip.Height > 0)
                    {
                        Rectangle prevScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
                        spriteBatch.GraphicsDevice.ScissorRectangle = idClip;
                        int idSelMin = Math.Min(_idCursorPosition, _idAnchorPosition);
                        int idSelMax = Math.Max(_idCursorPosition, _idAnchorPosition);
                        if (idSelMin < idSelMax)
                        {
                            float selStartX = inputRect.X + 6 + uiFont.MeasureString(_idEditBuffer.Substring(0, idSelMin)).X;
                            float selEndX = inputRect.X + 6 + uiFont.MeasureString(_idEditBuffer.Substring(0, idSelMax)).X;
                            Rectangle selRect = new Rectangle((int)selStartX, inputRect.Y + 2, (int)(selEndX - selStartX), inputH - 4);
                            if (selRect.Width > 0)
                                spriteBatch.Draw(_pixel, selRect, new Color(60, 100, 180, 140));
                        }
                        if (_idEditBuffer.Length > 0)
                            spriteBatch.DrawString(uiFont, _idEditBuffer, new Vector2(inputRect.X + 6, inputRect.Y + (inputH - uiFont.MeasureString(_idEditBuffer).Y) / 2), TEXT_COLOR);
                        if (_idShowCursor)
                        {
                            string beforeCursor = _idEditBuffer.Substring(0, _idCursorPosition);
                            float cursorX = inputRect.X + 6 + uiFont.MeasureString(beforeCursor).X;
                            spriteBatch.Draw(_pixel, new Rectangle((int)cursorX, inputRect.Y + 4, 1, inputH - 8), TEXT_COLOR);
                        }
                        spriteBatch.GraphicsDevice.ScissorRectangle = prevScissor;
                    }
                    Rectangle confirmBtnRect = new Rectangle(confirmBtnX, confirmBtnY, confirmBtnW, confirmBtnH);
                    _idConfirmButtonBounds = confirmBtnRect;
                    bool idConfirmDisabled = IsIdEditConfirmDisabled();
                    bool confirmHover = !idConfirmDisabled && confirmBtnRect.Contains(_hoverMousePosition);
                    Color confirmBg = idConfirmDisabled ? new Color(50, 50, 50) : (confirmHover ? new Color(60, 160, 60) : new Color(45, 130, 45));
                    Color confirmBorder = idConfirmDisabled ? SEARCH_BORDER : new Color(70, 180, 70);
                    Color confirmTextColor = idConfirmDisabled ? TEXT_SECONDARY : TEXT_COLOR;
                    spriteBatch.Draw(_pixel, confirmBtnRect, confirmBg);
                    DrawBorder(spriteBatch, confirmBtnRect, confirmBorder);
                    spriteBatch.DrawString(uiFont, confirmLabel, new Vector2(confirmBtnRect.X + (confirmBtnW - confirmSize.X) / 2, confirmBtnRect.Y + (confirmBtnH - confirmSize.Y) / 2), confirmTextColor);
                    idContentRight = confirmBtnRect.Right;
                }

                nameBlockX = idContentRight + idToNameGap;
                nameBlockW = Math.Max(0, nameBlockRightMax - nameBlockX);

                Vector2 nameHeaderPos = new Vector2(nameBlockX + 10, rowY + (rowH - nameHeaderSize.Y * headerBoldScale) / 2);
                spriteBatch.DrawString(uiFont, nameHeader, nameHeaderPos, TEXT_SECONDARY, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);

                if (!_isEditingName)
                {
                    int nameValX = nameBlockX + 10 + nameHeaderW + 8;
                    Vector2 nameValSize = uiFont.MeasureString(_characterName);
                    spriteBatch.DrawString(uiFont, _characterName, new Vector2(nameValX, rowY + (rowH - nameValSize.Y) / 2), TEXT_COLOR);
                    string editLabel = "Edit";
                    Vector2 editLabelSize = uiFont.MeasureString(editLabel);
                    int editBtnPadH = 16;
                    int editBtnPadV = 6;
                    int editBtnW = (int)editLabelSize.X + editBtnPadH;
                    int editBtnH = (int)editLabelSize.Y + editBtnPadV;
                    int editBtnX = nameValX + (int)nameValSize.X + 10;
                    int editBtnY = rowY + (rowH - editBtnH) / 2;
                    Rectangle editBtnRect = new Rectangle(editBtnX, editBtnY, editBtnW, editBtnH);
                    _nameEditButtonBounds = editBtnRect;
                    bool nameEditDisabled = _isEditingId;
                    bool nameEditHover = !nameEditDisabled && editBtnRect.Contains(_hoverMousePosition);
                    Color nameEditBg = nameEditDisabled ? new Color(BUTTON_COLOR.R / 2, BUTTON_COLOR.G / 2, BUTTON_COLOR.B / 2) : (nameEditHover ? BUTTON_HOVER : BUTTON_COLOR);
                    Color nameEditBorder = nameEditDisabled ? new Color(SEARCH_BORDER.R / 2, SEARCH_BORDER.G / 2, SEARCH_BORDER.B / 2) : SEARCH_BORDER;
                    Color nameEditText = nameEditDisabled ? TEXT_SECONDARY : TEXT_COLOR;
                    spriteBatch.Draw(_pixel, editBtnRect, nameEditBg);
                    DrawBorder(spriteBatch, editBtnRect, nameEditBorder);
                    spriteBatch.DrawString(uiFont, editLabel, new Vector2(editBtnRect.X + (editBtnW - editLabelSize.X) / 2, editBtnRect.Y + (editBtnH - editLabelSize.Y) / 2), nameEditText);
                }
                else
                {
                    string confirmLabel = "Confirm";
                    Vector2 confirmSize = uiFont.MeasureString(confirmLabel);
                    int confirmPadH = 16;
                    int confirmPadV = 6;
                    int confirmBtnW = (int)confirmSize.X + confirmPadH;
                    int confirmBtnH = (int)confirmSize.Y + confirmPadV;
                    int inputX = nameBlockX + 10 + nameHeaderW + 8;
                    int nameBlockRight = nameBlockX + nameBlockW - 4;
                    int maxNameInputW = Math.Max(0, nameBlockRight - inputX - 8 - confirmBtnW);
                    int preferredNameInputW = Math.Max(MIN_INPUT_WIDTH_NAME, nameBlockW - nameHeaderW - confirmBtnW - 30);
                    int inputW = Math.Min(preferredNameInputW, maxNameInputW);
                    int confirmBtnX = inputX + inputW + 8;
                    int confirmBtnY = rowY + (rowH - confirmBtnH) / 2;
                    int inputH = rowH - 4;
                    int inputY = rowY + 2;
                    Rectangle inputRect = new Rectangle(inputX, inputY, inputW, inputH);
                    _nameTextBounds = inputRect;
                    spriteBatch.Draw(_pixel, inputRect, new Color(25, 25, 25));
                    DrawBorder(spriteBatch, inputRect, new Color(147, 112, 219));
                    int nameSelMin = Math.Min(_nameCursorPosition, _nameAnchorPosition);
                    int nameSelMax = Math.Max(_nameCursorPosition, _nameAnchorPosition);
                    if (nameSelMin < nameSelMax)
                    {
                        float selStartX = inputRect.X + 6 + uiFont.MeasureString(_nameEditBuffer.Substring(0, nameSelMin)).X;
                        float selEndX = inputRect.X + 6 + uiFont.MeasureString(_nameEditBuffer.Substring(0, nameSelMax)).X;
                        Rectangle selRect = new Rectangle((int)selStartX, inputRect.Y + 2, (int)(selEndX - selStartX), inputH - 4);
                        if (selRect.Width > 0)
                            spriteBatch.Draw(_pixel, selRect, new Color(60, 100, 180, 140));
                    }
                    if (_nameEditBuffer.Length > 0)
                        spriteBatch.DrawString(uiFont, _nameEditBuffer, new Vector2(inputRect.X + 6, inputRect.Y + (inputH - uiFont.MeasureString(_nameEditBuffer).Y) / 2), TEXT_COLOR);
                    if (_nameShowCursor)
                    {
                        string beforeCursor = _nameEditBuffer.Substring(0, _nameCursorPosition);
                        float cursorX = inputRect.X + 6 + uiFont.MeasureString(beforeCursor).X;
                        spriteBatch.Draw(_pixel, new Rectangle((int)cursorX, inputRect.Y + 4, 1, inputH - 8), TEXT_COLOR);
                    }
                    Rectangle confirmBtnRect = new Rectangle(confirmBtnX, confirmBtnY, confirmBtnW, confirmBtnH);
                    _nameConfirmButtonBounds = confirmBtnRect;
                    bool confirmHover = confirmBtnRect.Contains(_hoverMousePosition);
                    spriteBatch.Draw(_pixel, confirmBtnRect, confirmHover ? new Color(60, 160, 60) : new Color(45, 130, 45));
                    DrawBorder(spriteBatch, confirmBtnRect, new Color(70, 180, 70));
                    spriteBatch.DrawString(uiFont, confirmLabel, new Vector2(confirmBtnRect.X + (confirmBtnW - confirmSize.X) / 2, confirmBtnRect.Y + (confirmBtnH - confirmSize.Y) / 2), TEXT_COLOR);
                }
            }

            Rectangle imageBounds = new Rectangle(_characterImageBounds.X, _characterImageBounds.Y + scrollY, _characterImageBounds.Width, _characterImageBounds.Height);
            if (IsVisible(imageBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, imageBounds, new Color(30, 30, 30));
                DrawBorder(spriteBatch, imageBounds, PANEL_BORDER);
                Vector2 imageTextPos = new Vector2(imageBounds.X + imageBounds.Width / 2 - 50, imageBounds.Y + imageBounds.Height / 2);
                spriteBatch.DrawString(uiFont, "Knight_Warrior", imageTextPos, TEXT_SECONDARY);
            }

            int statsY = imageBounds.Bottom + PANEL_PADDING;
            Rectangle statsHeaderBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, statsY, 200, 30);
            if (IsVisible(statsHeaderBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, statsHeaderBounds, SECTION_HEADER);
                Vector2 statsHeaderPos = new Vector2(statsHeaderBounds.X + 10, statsHeaderBounds.Y + 5);
                spriteBatch.DrawString(uiFont, "BASE STATS", statsHeaderPos, TEXT_COLOR);
            }

            int statY = statsHeaderBounds.Bottom + 10;
            foreach (var stat in _baseStats)
            {
                Rectangle statBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, statY, 200, 25);
                if (IsVisible(statBounds, scissorRect))
                {
                    Vector2 statNamePos = new Vector2(statBounds.X + 10, statY);
                    spriteBatch.DrawString(uiFont, $"{stat.Name}: {stat.Value}", statNamePos, TEXT_COLOR);
                    Rectangle barBounds = new Rectangle(statBounds.X + 10, statY + 20, 150, 8);
                    spriteBatch.Draw(_pixel, barBounds, STAT_BAR_BACKGROUND);
                    int fillWidth = (int)(barBounds.Width * ((float)stat.Value / stat.MaxValue));
                    Rectangle fillBounds = new Rectangle(barBounds.X, barBounds.Y, fillWidth, barBounds.Height);
                    spriteBatch.Draw(_pixel, fillBounds, STAT_BAR_FILL);
                }
                statY += 35;
            }

            int rightStatsY = statsHeaderBounds.Y;
            int rightStatsX = _centerPanelBounds.Right - 250;
            Rectangle healthBounds = new Rectangle(rightStatsX, rightStatsY, 250, 25);
            if (IsVisible(healthBounds, scissorRect))
                DrawStatDisplay(spriteBatch, "Health: 120 / 120", rightStatsX, rightStatsY, Color.Red);
            Rectangle actionPointsBounds = new Rectangle(rightStatsX, rightStatsY + 30, 250, 25);
            if (IsVisible(actionPointsBounds, scissorRect))
                DrawStatDisplay(spriteBatch, "Action Points: 8", rightStatsX, rightStatsY + 30, Color.Gray);
            Rectangle initiativeBounds = new Rectangle(rightStatsX, rightStatsY + 60, 250, 25);
            if (IsVisible(initiativeBounds, scissorRect))
                DrawStatDisplay(spriteBatch, "Initiative: 12", rightStatsX, rightStatsY + 60, Color.Yellow);

            int traitsSectionY = statY + PANEL_PADDING;
            Rectangle traitsSectionHeader = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, traitsSectionY, _centerPanelBounds.Width - PANEL_PADDING * 2, 30);
            if (IsVisible(traitsSectionHeader, scissorRect))
            {
                spriteBatch.Draw(_pixel, traitsSectionHeader, SECTION_HEADER);
                Vector2 traitsSectionPos = new Vector2(traitsSectionHeader.X + 10, traitsSectionHeader.Y + 5);
                spriteBatch.DrawString(uiFont, "TRAITS & PERKS", traitsSectionPos, TEXT_COLOR);
            }

            int charTraitY = traitsSectionHeader.Bottom + 10;
            foreach (var trait in _characterTraits)
            {
                Rectangle traitBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, charTraitY, 300, 30);
                if (IsVisible(traitBounds, scissorRect))
                {
                    Vector2 traitNamePos = new Vector2(traitBounds.X + 10, traitBounds.Y + 5);
                    spriteBatch.DrawString(uiFont, trait.Name, traitNamePos, TEXT_COLOR);
                    Rectangle editButtonBounds = new Rectangle(traitBounds.Right - 100, traitBounds.Y, 50, 25);
                    spriteBatch.Draw(_pixel, editButtonBounds, BUTTON_COLOR);
                    DrawBorder(spriteBatch, editButtonBounds, PANEL_BORDER);
                    Vector2 editPos = new Vector2(editButtonBounds.X + 10, editButtonBounds.Y + 3);
                    spriteBatch.DrawString(uiFont, "Edit", editPos, TEXT_COLOR);
                }
                charTraitY += 35;
            }

            int abilitiesSectionY = charTraitY + PANEL_PADDING;
            Rectangle abilitiesSectionHeader = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, abilitiesSectionY, _centerPanelBounds.Width - PANEL_PADDING * 2, 30);
            if (IsVisible(abilitiesSectionHeader, scissorRect))
            {
                spriteBatch.Draw(_pixel, abilitiesSectionHeader, SECTION_HEADER);
                Vector2 abilitiesSectionPos = new Vector2(abilitiesSectionHeader.X + 10, abilitiesSectionHeader.Y + 5);
                spriteBatch.DrawString(uiFont, "SKILLS", abilitiesSectionPos, TEXT_COLOR);
            }

            int abilityY = abilitiesSectionHeader.Bottom + 10;
            foreach (var ability in _characterAbilities)
            {
                Rectangle abilityBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, abilityY, 300, 30);
                if (IsVisible(abilityBounds, scissorRect))
                {
                    Vector2 abilityNamePos = new Vector2(abilityBounds.X + 10, abilityBounds.Y + 5);
                    spriteBatch.DrawString(uiFont, ability.Name, abilityNamePos, TEXT_COLOR);
                    Rectangle pickButtonBounds = new Rectangle(abilityBounds.Right - 100, abilityBounds.Y, 80, 25);
                    spriteBatch.Draw(_pixel, pickButtonBounds, BUTTON_COLOR);
                    DrawBorder(spriteBatch, pickButtonBounds, PANEL_BORDER);
                    Vector2 pickPos = new Vector2(pickButtonBounds.X + 5, pickButtonBounds.Y + 3);
                    spriteBatch.DrawString(uiFont, "Pick Skill", pickPos, TEXT_COLOR);
                }
                abilityY += 35;
            }
        }
    }
}
