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

            // Description area: header (Description + Edit/Save) and content bounds (for hit test)
            int descScreenY = _inspectionDescriptionBounds.Y + scrollY;
            int descW = _inspectionDescriptionBounds.Width;
            int descH = _inspectionDescriptionBounds.Height;
            const float descHeaderBoldScale = 1.2f;
            int descHeaderHeight = (int)(uiFont.MeasureString("Description").Y * descHeaderBoldScale) + 8;
            float descTitleWidth = uiFont.MeasureString("Description").X * descHeaderBoldScale;
            string descBtnLabel = _isEditingDescription ? "Save" : "Edit";
            Vector2 descBtnSize = uiFont.MeasureString(descBtnLabel);
            int descBtnW = (int)descBtnSize.X + 16;
            int descBtnH = (int)descBtnSize.Y + 6;
            int descBtnX = _inspectionDescriptionBounds.X + 10 + CATEGORY_BAR_ACCENT_WIDTH + (int)descTitleWidth + 12;
            int descBtnY = descScreenY + (descHeaderHeight - descBtnH) / 2;
            _descriptionEditButtonBounds = new Rectangle(descBtnX, descBtnY, descBtnW, descBtnH);
            _descriptionContentBounds = new Rectangle(
                _inspectionDescriptionBounds.X + DESCRIPTION_TEXT_PADDING,
                descScreenY + descHeaderHeight + DESCRIPTION_TEXT_PADDING,
                Math.Max(0, descW - DESCRIPTION_TEXT_PADDING * 2),
                Math.Max(0, descH - descHeaderHeight - DESCRIPTION_TEXT_PADDING * 2));
        }

        private void DrawCharacterInspectionView(SpriteBatch spriteBatch, int scrollOffset, Rectangle scissorRect, SpriteFont uiFont)
        {
            int scrollY = -scrollOffset;
            const float headerBoldScale = 1.2f;

            int rowVisibleWidth = Math.Min(_nameInputBounds.Width, Math.Max(0, scissorRect.Right - _nameInputBounds.X));
            Rectangle nameBounds = new Rectangle(_nameInputBounds.X, _nameInputBounds.Y + scrollY, rowVisibleWidth, _nameInputBounds.Height);
            if (IsVisible(nameBounds, scissorRect))
            {
                // Top bar background strip (modern header look)
                Rectangle topBarBg = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, nameBounds.Y, _centerPanelBounds.Width - PANEL_PADDING * 2, nameBounds.Height);
                if (IsVisible(topBarBg, scissorRect))
                    spriteBatch.Draw(_pixel, topBarBg, INSPECTION_ROW_BG);
                int rowY = nameBounds.Y;
                int rowH = nameBounds.Height;
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
                float backPulse = GetListViewHeaderPulse();
                Color backBase = backHover ? new Color(120, 90, 200) : BUTTON_ACTIVE;
                Color backColor = Color.Lerp(backBase, new Color(165, 130, 230), backPulse * 0.12f);
                spriteBatch.Draw(_pixel, backBtnRect, backColor);
                DrawBorder(spriteBatch, backBtnRect, Color.Lerp(new Color(backColor.R + 30, backColor.G + 30, backColor.B + 30), new Color(200, 180, 255), backPulse * 0.2f));
                spriteBatch.DrawString(uiFont, backLabel, new Vector2(backBtnRect.X + (backBtnW - backLabelSize.X) / 2, backBtnRect.Y + (backBtnH - backLabelSize.Y) / 2), Color.White);

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
                    float idEditPulse = GetListViewHeaderPulse();
                    Color idEditBase = idEditDisabled ? new Color(BUTTON_COLOR.R / 2, BUTTON_COLOR.G / 2, BUTTON_COLOR.B / 2) : (editHover ? new Color(120, 90, 200) : BUTTON_ACTIVE);
                    Color idEditBg = idEditDisabled ? idEditBase : Color.Lerp(idEditBase, new Color(165, 130, 230), idEditPulse * 0.12f);
                    Color idEditBorder = idEditDisabled ? new Color(SEARCH_BORDER.R / 2, SEARCH_BORDER.G / 2, SEARCH_BORDER.B / 2) : Color.Lerp(new Color(idEditBg.R + 30, idEditBg.G + 30, idEditBg.B + 30), new Color(200, 180, 255), idEditPulse * 0.2f);
                    Color idEditText = idEditDisabled ? TEXT_SECONDARY : Color.White;
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
                    spriteBatch.Draw(_pixel, inputRect, INSPECTION_INPUT_BG);
                    DrawBorder(spriteBatch, inputRect, INSPECTION_ACCENT_BORDER);
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
                    float nameEditPulse = GetListViewHeaderPulse();
                    Color nameEditBase = nameEditDisabled ? new Color(BUTTON_COLOR.R / 2, BUTTON_COLOR.G / 2, BUTTON_COLOR.B / 2) : (nameEditHover ? new Color(120, 90, 200) : BUTTON_ACTIVE);
                    Color nameEditBg = nameEditDisabled ? nameEditBase : Color.Lerp(nameEditBase, new Color(165, 130, 230), nameEditPulse * 0.12f);
                    Color nameEditBorder = nameEditDisabled ? new Color(SEARCH_BORDER.R / 2, SEARCH_BORDER.G / 2, SEARCH_BORDER.B / 2) : Color.Lerp(new Color(nameEditBg.R + 30, nameEditBg.G + 30, nameEditBg.B + 30), new Color(200, 180, 255), nameEditPulse * 0.2f);
                    Color nameEditText = nameEditDisabled ? TEXT_SECONDARY : Color.White;
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
                    spriteBatch.Draw(_pixel, inputRect, INSPECTION_INPUT_BG);
                    DrawBorder(spriteBatch, inputRect, INSPECTION_ACCENT_BORDER);
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

            // Horizontal separator between top bar (Back, ID, Name) and content below
            int topBarBottom = nameBounds.Bottom;
            Rectangle hrRect = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, topBarBottom, _centerPanelBounds.Width - PANEL_PADDING * 2, 1);
            if (IsVisible(hrRect, scissorRect))
                spriteBatch.Draw(_pixel, hrRect, INSPECTION_SEPARATOR);

            Rectangle imageBounds = new Rectangle(_characterImageBounds.X, _characterImageBounds.Y + scrollY, _characterImageBounds.Width, _characterImageBounds.Height);
            if (IsVisible(imageBounds, scissorRect))
            {
                spriteBatch.Draw(_pixel, imageBounds, INSPECTION_IMAGE_BG);
                DrawBorder(spriteBatch, imageBounds, INSPECTION_ACCENT_BORDER);
                Vector2 imageTextPos = new Vector2(imageBounds.X + imageBounds.Width / 2 - 50, imageBounds.Y + imageBounds.Height / 2);
                spriteBatch.DrawString(uiFont, "Knight_Warrior", imageTextPos, TEXT_SECONDARY);
            }

            Rectangle descBounds = new Rectangle(_inspectionDescriptionBounds.X, _inspectionDescriptionBounds.Y + scrollY, _inspectionDescriptionBounds.Width, _inspectionDescriptionBounds.Height);
            if (descBounds.Width > 0 && descBounds.Height > 0 && IsVisible(descBounds, scissorRect))
            {
                // Header: same style as BASE STATS / TRAITS (DrawCategoryTitleBar) with Edit/Save right after "Description"
                int descHeaderHeight = (int)(uiFont.MeasureString("Description").Y * headerBoldScale) + 8;
                Rectangle descHeaderRect = new Rectangle(descBounds.X, descBounds.Y, descBounds.Width, descHeaderHeight);
                DrawCategoryTitleBar(spriteBatch, descHeaderRect, "Description", uiFont, headerBoldScale);
                string descBtnLabel = _isEditingDescription ? "Save" : "Edit";
                Vector2 descBtnLabelSize = uiFont.MeasureString(descBtnLabel);
                int descBtnW = (int)descBtnLabelSize.X + 16;
                int descBtnH = (int)descBtnLabelSize.Y + 6;
                int descBtnX = descBounds.X + 10 + CATEGORY_BAR_ACCENT_WIDTH + (int)(uiFont.MeasureString("Description").X * headerBoldScale) + 12;
                int descBtnY = descBounds.Y + (descHeaderHeight - descBtnH) / 2;
                Rectangle descEditBtnRect = new Rectangle(descBtnX, descBtnY, descBtnW, descBtnH);
                bool descBtnHover = descEditBtnRect.Contains(_hoverMousePosition);
                float descBtnPulse = GetListViewHeaderPulse();
                Color descBtnBase = descBtnHover ? new Color(120, 90, 200) : BUTTON_ACTIVE;
                Color descBtnBg = Color.Lerp(descBtnBase, new Color(165, 130, 230), descBtnPulse * 0.12f);
                spriteBatch.Draw(_pixel, descEditBtnRect, descBtnBg);
                DrawBorder(spriteBatch, descEditBtnRect, Color.Lerp(new Color(descBtnBg.R + 30, descBtnBg.G + 30, descBtnBg.B + 30), new Color(200, 180, 255), descBtnPulse * 0.2f));
                spriteBatch.DrawString(uiFont, descBtnLabel, new Vector2(descEditBtnRect.X + (descBtnW - descBtnLabelSize.X) / 2, descEditBtnRect.Y + (descBtnH - descBtnLabelSize.Y) / 2), Color.White);
                // Content area below header: subtle card background when not editing
                Rectangle descContentRect = new Rectangle(descBounds.X + DESCRIPTION_TEXT_PADDING, descBounds.Y + descHeaderHeight + DESCRIPTION_TEXT_PADDING,
                    Math.Max(0, descBounds.Width - DESCRIPTION_TEXT_PADDING * 2), Math.Max(0, descBounds.Height - descHeaderHeight - DESCRIPTION_TEXT_PADDING * 2));
                if (_isEditingDescription)
                    DrawDescriptionEditArea(spriteBatch, uiFont, descContentRect, scissorRect, scrollY);
                else
                {
                    if (IsVisible(descContentRect, scissorRect))
                    {
                        spriteBatch.Draw(_pixel, descContentRect, INSPECTION_CARD_BG);
                        DrawBorder(spriteBatch, descContentRect, INSPECTION_ACCENT_BORDER);
                    }
                    DrawDescriptionTextView(spriteBatch, uiFont, descContentRect, scissorRect);
                }
            }

            int statsY = Math.Max(imageBounds.Bottom, descBounds.Bottom) + PANEL_PADDING;
            int statsHeaderHeight = (int)(uiFont.MeasureString("BASE STATS").Y * headerBoldScale) + 8;
            Rectangle statsHeaderBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, statsY, _centerPanelBounds.Width - PANEL_PADDING * 2, statsHeaderHeight);
            if (IsVisible(statsHeaderBounds, scissorRect))
                DrawCategoryTitleBar(spriteBatch, statsHeaderBounds, "BASE STATS", uiFont, headerBoldScale);

            int statY = statsHeaderBounds.Bottom + 10;
            if (_baseStats.Count == 0)
            {
                Rectangle emptyCard = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, statY, _centerPanelBounds.Width - PANEL_PADDING * 2, 70);
                if (IsVisible(emptyCard, scissorRect))
                {
                    spriteBatch.Draw(_pixel, emptyCard, INSPECTION_CARD_BG);
                    DrawBorder(spriteBatch, emptyCard, INSPECTION_ACCENT_BORDER);
                }
                DrawEmptyInspectionSection(spriteBatch, uiFont, _centerPanelBounds.X + PANEL_PADDING, statY, _centerPanelBounds.Width - PANEL_PADDING * 2, 70, scissorRect, "There are no stats yet, create or link one.");
                statY += 70;
            }
            else
            {
                int cardContentH = _baseStats.Count * 35 + 12;
                Rectangle statsCard = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, statY, _centerPanelBounds.Width - PANEL_PADDING * 2, cardContentH);
                if (IsVisible(statsCard, scissorRect))
                {
                    spriteBatch.Draw(_pixel, statsCard, INSPECTION_CARD_BG);
                    DrawBorder(spriteBatch, statsCard, INSPECTION_ACCENT_BORDER);
                }
                foreach (var stat in _baseStats)
                {
                    Rectangle statBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, statY, 200, 25);
                    if (IsVisible(statBounds, scissorRect))
                    {
                        Vector2 statNamePos = new Vector2(statBounds.X + 10, statY);
                        spriteBatch.DrawString(uiFont, $"{stat.Name}: {stat.Value}", statNamePos, TEXT_COLOR);
                        Rectangle barBounds = new Rectangle(statBounds.X + 10, statY + 20, 150, 8);
                        spriteBatch.Draw(_pixel, barBounds, STAT_BAR_TRACK_INSPECTION);
                        int maxVal = Math.Max(1, stat.MaxValue);
                        int fillWidth = (int)(barBounds.Width * ((float)stat.Value / maxVal));
                        Rectangle fillBounds = new Rectangle(barBounds.X, barBounds.Y, fillWidth, barBounds.Height);
                        spriteBatch.Draw(_pixel, fillBounds, STAT_BAR_FILL);
                    }
                    statY += 35;
                }
                statY += 12;
            }

            int traitsSectionY = statY + PANEL_PADDING;
            int traitsHeaderHeight = (int)(uiFont.MeasureString("TRAITS & PERKS").Y * headerBoldScale) + 8;
            Rectangle traitsSectionHeader = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, traitsSectionY, _centerPanelBounds.Width - PANEL_PADDING * 2, traitsHeaderHeight);
            if (IsVisible(traitsSectionHeader, scissorRect))
                DrawCategoryTitleBar(spriteBatch, traitsSectionHeader, "TRAITS & PERKS", uiFont, headerBoldScale);

            int charTraitY = traitsSectionHeader.Bottom + 10;
            if (_characterTraits.Count == 0)
            {
                Rectangle emptyCard = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, charTraitY, _centerPanelBounds.Width - PANEL_PADDING * 2, 70);
                if (IsVisible(emptyCard, scissorRect))
                {
                    spriteBatch.Draw(_pixel, emptyCard, INSPECTION_CARD_BG);
                    DrawBorder(spriteBatch, emptyCard, INSPECTION_ACCENT_BORDER);
                }
                DrawEmptyInspectionSection(spriteBatch, uiFont, _centerPanelBounds.X + PANEL_PADDING, charTraitY, _centerPanelBounds.Width - PANEL_PADDING * 2, 70, scissorRect, "There are no perks yet, create or link one.");
                charTraitY += 70;
            }
            else
            {
                int traitCardH = _characterTraits.Count * 35 + 12;
                Rectangle traitsCard = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, charTraitY, _centerPanelBounds.Width - PANEL_PADDING * 2, traitCardH);
                if (IsVisible(traitsCard, scissorRect))
                {
                    spriteBatch.Draw(_pixel, traitsCard, INSPECTION_CARD_BG);
                    DrawBorder(spriteBatch, traitsCard, INSPECTION_ACCENT_BORDER);
                }
                foreach (var trait in _characterTraits)
                {
                    Rectangle traitBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, charTraitY, 300, 30);
                    if (IsVisible(traitBounds, scissorRect))
                    {
                        Rectangle rowBg = new Rectangle(_centerPanelBounds.X + PANEL_PADDING + 4, charTraitY + 2, _centerPanelBounds.Width - PANEL_PADDING * 2 - 8, 28);
                        if (IsVisible(rowBg, scissorRect))
                            spriteBatch.Draw(_pixel, rowBg, INSPECTION_ROW_BG);
                        Vector2 traitNamePos = new Vector2(traitBounds.X + 10, traitBounds.Y + 5);
                        spriteBatch.DrawString(uiFont, trait.Name, traitNamePos, TEXT_COLOR);
                        Rectangle editButtonBounds = new Rectangle(traitBounds.Right - 100, traitBounds.Y, 50, 25);
                        bool traitEditHover = editButtonBounds.Contains(_hoverMousePosition);
                        float traitEditPulse = GetListViewHeaderPulse();
                        Color traitEditBase = traitEditHover ? new Color(120, 90, 200) : BUTTON_ACTIVE;
                        Color traitEditBg = Color.Lerp(traitEditBase, new Color(165, 130, 230), traitEditPulse * 0.12f);
                        spriteBatch.Draw(_pixel, editButtonBounds, traitEditBg);
                        DrawBorder(spriteBatch, editButtonBounds, Color.Lerp(new Color(traitEditBg.R + 30, traitEditBg.G + 30, traitEditBg.B + 30), new Color(200, 180, 255), traitEditPulse * 0.2f));
                        Vector2 editPos = new Vector2(editButtonBounds.X + 10, editButtonBounds.Y + 3);
                        spriteBatch.DrawString(uiFont, "Edit", editPos, Color.White);
                    }
                    charTraitY += 35;
                }
                charTraitY += 12;
            }

            int abilitiesSectionY = charTraitY + PANEL_PADDING;
            int abilitiesHeaderHeight = (int)(uiFont.MeasureString("SKILLS").Y * headerBoldScale) + 8;
            Rectangle abilitiesSectionHeader = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, abilitiesSectionY, _centerPanelBounds.Width - PANEL_PADDING * 2, abilitiesHeaderHeight);
            if (IsVisible(abilitiesSectionHeader, scissorRect))
                DrawCategoryTitleBar(spriteBatch, abilitiesSectionHeader, "SKILLS", uiFont, headerBoldScale);

            int abilityY = abilitiesSectionHeader.Bottom + 10;
            if (_characterAbilities.Count == 0)
            {
                Rectangle emptyCard = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, abilityY, _centerPanelBounds.Width - PANEL_PADDING * 2, 70);
                if (IsVisible(emptyCard, scissorRect))
                {
                    spriteBatch.Draw(_pixel, emptyCard, INSPECTION_CARD_BG);
                    DrawBorder(spriteBatch, emptyCard, INSPECTION_ACCENT_BORDER);
                }
                DrawEmptyInspectionSection(spriteBatch, uiFont, _centerPanelBounds.X + PANEL_PADDING, abilityY, _centerPanelBounds.Width - PANEL_PADDING * 2, 70, scissorRect, "There are no skills yet, create or link one.");
            }
            else
            {
                int abilityCardH = _characterAbilities.Count * 35 + 12;
                Rectangle abilitiesCard = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, abilityY, _centerPanelBounds.Width - PANEL_PADDING * 2, abilityCardH);
                if (IsVisible(abilitiesCard, scissorRect))
                {
                    spriteBatch.Draw(_pixel, abilitiesCard, INSPECTION_CARD_BG);
                    DrawBorder(spriteBatch, abilitiesCard, INSPECTION_ACCENT_BORDER);
                }
                foreach (var ability in _characterAbilities)
                {
                    Rectangle abilityBounds = new Rectangle(_centerPanelBounds.X + PANEL_PADDING, abilityY, 300, 30);
                    if (IsVisible(abilityBounds, scissorRect))
                    {
                        Rectangle rowBg = new Rectangle(_centerPanelBounds.X + PANEL_PADDING + 4, abilityY + 2, _centerPanelBounds.Width - PANEL_PADDING * 2 - 8, 28);
                        if (IsVisible(rowBg, scissorRect))
                            spriteBatch.Draw(_pixel, rowBg, INSPECTION_ROW_BG);
                        Vector2 abilityNamePos = new Vector2(abilityBounds.X + 10, abilityBounds.Y + 5);
                        spriteBatch.DrawString(uiFont, ability.Name, abilityNamePos, TEXT_COLOR);
                        Rectangle pickButtonBounds = new Rectangle(abilityBounds.Right - 100, abilityBounds.Y, 80, 25);
                        bool pickHover = pickButtonBounds.Contains(_hoverMousePosition);
                        float pickPulse = GetListViewHeaderPulse();
                        Color pickBase = pickHover ? new Color(120, 90, 200) : BUTTON_ACTIVE;
                        Color pickBg = Color.Lerp(pickBase, new Color(165, 130, 230), pickPulse * 0.12f);
                        spriteBatch.Draw(_pixel, pickButtonBounds, pickBg);
                        DrawBorder(spriteBatch, pickButtonBounds, Color.Lerp(new Color(pickBg.R + 30, pickBg.G + 30, pickBg.B + 30), new Color(200, 180, 255), pickPulse * 0.2f));
                        Vector2 pickPos = new Vector2(pickButtonBounds.X + 5, pickButtonBounds.Y + 3);
                        spriteBatch.DrawString(uiFont, "Pick Skill", pickPos, Color.White);
                    }
                    abilityY += 35;
                }
            }
        }

        private const int DESCRIPTION_TEXT_PADDING = 8;

        /// <summary>Draws read-only description text with scroll (display mode).</summary>
        private void DrawDescriptionTextView(SpriteBatch spriteBatch, SpriteFont uiFont, Rectangle contentRect, Rectangle scissorRect)
        {
            int innerX = contentRect.X;
            int innerY = contentRect.Y;
            int innerW = contentRect.Width;
            int innerH = contentRect.Height;
            Rectangle clip = Rectangle.Intersect(scissorRect, contentRect);
            if (clip.Width <= 0 || clip.Height <= 0) return;

            float lineHeight = uiFont.MeasureString("Ay").Y;
            string text = _characterDescriptionText ?? "";
            if (string.IsNullOrEmpty(text))
            {
                spriteBatch.DrawString(uiFont, "Description (multiline)...", new Vector2(innerX, innerY), TEXT_SECONDARY);
                return;
            }
            string[] lines = text.Split('\n');
            Rectangle savedScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.GraphicsDevice.ScissorRectangle = clip;
            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = innerY + i * lineHeight - _descriptionScrollY;
                if (lineY + lineHeight < innerY || lineY > innerY + innerH) continue;
                spriteBatch.DrawString(uiFont, lines[i], new Vector2(innerX, (int)lineY), TEXT_COLOR);
            }
            spriteBatch.GraphicsDevice.ScissorRectangle = savedScissor;
        }

        /// <summary>Draws editable description textarea with cursor and selection (edit mode).</summary>
        private void DrawDescriptionEditArea(SpriteBatch spriteBatch, SpriteFont uiFont, Rectangle contentRect, Rectangle scissorRect, int scrollOffset)
        {
            int innerX = contentRect.X + 6;
            int innerY = contentRect.Y;
            int innerW = Math.Max(0, contentRect.Width - 12);
            int innerH = contentRect.Height;
            Rectangle clip = Rectangle.Intersect(scissorRect, contentRect);
            if (clip.Width <= 0 || clip.Height <= 0) return;

            spriteBatch.Draw(_pixel, contentRect, INSPECTION_INPUT_BG);
            DrawBorder(spriteBatch, contentRect, INSPECTION_ACCENT_BORDER);

            float lineHeight = uiFont.MeasureString("Ay").Y;
            string text = _descriptionEditBuffer ?? "";
            string[] lines = text.Split('\n');
            int selMin = Math.Min(_descriptionCursorPosition, _descriptionAnchorPosition);
            int selMax = Math.Max(_descriptionCursorPosition, _descriptionAnchorPosition);

            Rectangle savedScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.GraphicsDevice.ScissorRectangle = clip;

            int idx = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                float lineY = innerY + i * lineHeight - _descriptionScrollY;
                if (lineY + lineHeight < contentRect.Y || lineY > contentRect.Bottom) { idx += line.Length + 1; continue; }
                int lineStart = idx;
                int lineEnd = idx + line.Length;
                if (selMin < lineEnd && selMax > lineStart)
                {
                    int selStartInLine = Math.Max(0, selMin - lineStart);
                    int selEndInLine = Math.Min(line.Length, selMax - lineStart);
                    if (selStartInLine < selEndInLine)
                    {
                        string beforeSel = line.Substring(0, selStartInLine);
                        string selText = line.Substring(selStartInLine, selEndInLine - selStartInLine);
                        float selX = innerX + uiFont.MeasureString(beforeSel).X;
                        float selW = uiFont.MeasureString(selText).X;
                        Rectangle selRect = new Rectangle((int)selX, (int)lineY, (int)selW, (int)lineHeight);
                        if (selRect.Width > 0)
                            spriteBatch.Draw(_pixel, selRect, new Color(60, 100, 180, 140));
                    }
                }
                if (line.Length > 0)
                    spriteBatch.DrawString(uiFont, line, new Vector2(innerX, (int)lineY), TEXT_COLOR);
                idx += line.Length + 1;
            }
            if (_descriptionShowCursor)
            {
                int curLine = 0, curCol = 0;
                int k = 0;
                for (int i = 0; i < lines.Length && k <= _descriptionCursorPosition; i++)
                {
                    curLine = i;
                    if (k + lines[i].Length >= _descriptionCursorPosition)
                        curCol = _descriptionCursorPosition - k;
                    else
                        curCol = lines[i].Length;
                    k += lines[i].Length + 1;
                }
                float cursorY = innerY + curLine * lineHeight - _descriptionScrollY;
                if (cursorY >= contentRect.Y - lineHeight && cursorY <= contentRect.Bottom)
                {
                    string cursorLine = curLine < lines.Length ? lines[curLine] : "";
                    string beforeCursor = cursorLine.Substring(0, Math.Min(curCol, cursorLine.Length));
                    float cursorX = innerX + uiFont.MeasureString(beforeCursor).X;
                    spriteBatch.Draw(_pixel, new Rectangle((int)cursorX, (int)cursorY, 1, (int)lineHeight), TEXT_COLOR);
                }
            }
            spriteBatch.GraphicsDevice.ScissorRectangle = savedScissor;
        }

        private const int EMPTY_INSPECTION_SECTION_HEIGHT = 70;
        private const int EMPTY_SECTION_PLUS_BUTTON_SIZE = 28;

        /// <summary>Draws message text and one plus button (disabled) below for an empty stats/perks/skills section.</summary>
        private void DrawEmptyInspectionSection(SpriteBatch spriteBatch, SpriteFont uiFont, int x, int y, int width, int height, Rectangle scissorRect, string message)
        {
            Rectangle sectionRect = new Rectangle(x, y, width, height);
            if (!IsVisible(sectionRect, scissorRect)) return;
            int centerX = x + width / 2;
            Vector2 msgSize = uiFont.MeasureString(message);
            Vector2 plusSize = uiFont.MeasureString("+");
            int buttonSize = EMPTY_SECTION_PLUS_BUTTON_SIZE;
            float msgY = y + (height - msgSize.Y - buttonSize - 8) / 2f;
            float plusButtonY = msgY + msgSize.Y + 8;

            // Message text
            spriteBatch.DrawString(uiFont, message, new Vector2(centerX - msgSize.X / 2, msgY), TEXT_SECONDARY);
            // Single plus button below text (All-tab style: pulse, purple, border, White)
            int plusX = centerX - buttonSize / 2;
            Rectangle plusRect = new Rectangle(plusX, (int)plusButtonY, buttonSize, buttonSize);
            bool plusHover = plusRect.Contains(_hoverMousePosition);
            float plusPulse = GetListViewHeaderPulse();
            Color plusBase = plusHover ? new Color(120, 90, 200) : BUTTON_ACTIVE;
            Color plusBg = Color.Lerp(plusBase, new Color(165, 130, 230), plusPulse * 0.12f);
            if (IsVisible(plusRect, scissorRect))
            {
                spriteBatch.Draw(_pixel, plusRect, plusBg);
                DrawBorder(spriteBatch, plusRect, Color.Lerp(new Color(plusBg.R + 30, plusBg.G + 30, plusBg.B + 30), new Color(200, 180, 255), plusPulse * 0.2f));
                spriteBatch.DrawString(uiFont, "+", new Vector2(plusRect.X + (buttonSize - plusSize.X) / 2, plusRect.Y + (buttonSize - plusSize.Y) / 2), Color.White);
            }
        }
    }
}
