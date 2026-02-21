using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MarySGameEngine.Modules.CharacterCreation
{
    /// <summary>Partial: right panel (inspector) – entity type descriptions for All tab and per-tab (Character, Trait, Skill, Effect, Stat, Tag).</summary>
    public partial class CharacterCreation
    {
        private void DrawRightPanel(SpriteBatch spriteBatch)
        {
            if (_pixel == null || _menuFont == null) return;
            if (_rightPanelBounds.Width <= 0 || _rightPanelBounds.Height <= 0) return;

            SpriteFont uiFont = _uiFont ?? _menuFont;

            spriteBatch.Draw(_pixel, _rightPanelBounds, PANEL_BACKGROUND);
            DrawBorder(spriteBatch, _rightPanelBounds, PANEL_BORDER);

            Rectangle originalScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
            int scissorWidth = _rightPanelBounds.Width - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH + SCROLLBAR_PADDING : 0);

            int scissorLeft = Math.Max(_rightPanelBounds.X, originalScissor.X);
            int scissorTop = Math.Max(_rightPanelBounds.Y, Math.Max(originalScissor.Y, _rightPanelBounds.Y));
            int scissorRight = Math.Min(_rightPanelBounds.X + scissorWidth, originalScissor.Right);
            int scissorBottom = Math.Min(_rightPanelBounds.Bottom, originalScissor.Bottom);

            int finalScissorWidth = Math.Max(0, scissorRight - scissorLeft);
            int finalScissorHeight = Math.Max(0, scissorBottom - scissorTop);

            Rectangle rightPanelScissor = new Rectangle(scissorLeft, scissorTop, finalScissorWidth, finalScissorHeight);

            spriteBatch.GraphicsDevice.ScissorRectangle = rightPanelScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

            int scrollY = -_rightPanelScrollY;
            int currentY = _rightPanelBounds.Y + PANEL_PADDING + scrollY;
            float headerBoldScale = 1.3f;
            float subtitleScale = 1.1f;
            float lineSpacing = uiFont.LineSpacing * 1.2f;
            float descriptionLineSpacing = uiFont.LineSpacing * 1.1f;

            if (_currentView == "All")
            {
                string[] entityTypes = { "Character", "Trait", "Skill", "Effect", "Stat", "Tag" };

                foreach (string entityType in entityTypes)
                {
                    if (_entityDescriptionsShort != null && _entityDescriptionsShort.ContainsKey(entityType))
                    {
                        Vector2 headerSize = uiFont.MeasureString(entityType) * headerBoldScale;
                        Vector2 headerPos = new Vector2(_rightPanelBounds.X + PANEL_PADDING, currentY);

                        if (currentY >= _rightPanelBounds.Y && currentY + headerSize.Y <= _rightPanelBounds.Bottom &&
                            currentY + headerSize.Y >= rightPanelScissor.Y && currentY <= rightPanelScissor.Bottom)
                        {
                            spriteBatch.DrawString(uiFont, entityType, headerPos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                        }
                        currentY += (int)(headerSize.Y + 10);

                        string description = _entityDescriptionsShort[entityType];
                        float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH : 0);
                        currentY = DrawWrappedText(spriteBatch, uiFont, description,
                            _rightPanelBounds.X + PANEL_PADDING, currentY, maxWidth, TEXT_COLOR, descriptionLineSpacing, rightPanelScissor);

                        currentY += 30;
                    }
                }
            }
            else
            {
                string entityType = _currentView;
                if (_tabToEntityType.ContainsKey(_currentView))
                    entityType = _tabToEntityType[_currentView];

                if (_entityDescriptionsLong.ContainsKey(entityType))
                {
                    Vector2 headerSize = uiFont.MeasureString(entityType) * headerBoldScale;
                    Vector2 headerPos = new Vector2(_rightPanelBounds.X + PANEL_PADDING, currentY);

                    if (currentY >= _rightPanelBounds.Y && currentY + headerSize.Y <= _rightPanelBounds.Bottom &&
                        currentY + headerSize.Y >= rightPanelScissor.Y && currentY <= rightPanelScissor.Bottom)
                    {
                        spriteBatch.DrawString(uiFont, entityType, headerPos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                    }
                    currentY += (int)(headerSize.Y + 15);

                    string description = _entityDescriptionsLong[entityType];
                    float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH : 0);
                    DrawLongDescriptionWithSubtitles(spriteBatch, uiFont, description,
                        _rightPanelBounds.X + PANEL_PADDING, currentY, maxWidth, TEXT_COLOR, descriptionLineSpacing, rightPanelScissor, subtitleScale);
                }
                else if (_entityDescriptionsShort.ContainsKey(entityType))
                {
                    Vector2 headerSize = uiFont.MeasureString(entityType) * headerBoldScale;
                    Vector2 headerPos = new Vector2(_rightPanelBounds.X + PANEL_PADDING, currentY);

                    if (currentY >= _rightPanelBounds.Y && currentY + headerSize.Y <= _rightPanelBounds.Bottom &&
                        currentY + headerSize.Y >= rightPanelScissor.Y && currentY <= rightPanelScissor.Bottom)
                    {
                        spriteBatch.DrawString(uiFont, entityType, headerPos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, headerBoldScale, SpriteEffects.None, 0f);
                    }
                    currentY += (int)(headerSize.Y + 10);

                    string description = _entityDescriptionsShort[entityType];
                    float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - (_rightPanelNeedsScrollbar ? SCROLLBAR_WIDTH : 0);
                    DrawWrappedText(spriteBatch, uiFont, description,
                        _rightPanelBounds.X + PANEL_PADDING, currentY, maxWidth, TEXT_COLOR, descriptionLineSpacing, rightPanelScissor);
                }
            }

            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissor;
            spriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = false };

            if (_rightPanelNeedsScrollbar)
                DrawRightPanelScrollbar(spriteBatch);
        }

        private int DrawWrappedText(SpriteBatch spriteBatch, SpriteFont font, string text, int x, int y, float maxWidth, Color color, float lineSpacing, Rectangle scissorRect)
        {
            string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int currentY = y;

            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    currentY += (int)lineSpacing;
                    continue;
                }

                string[] words = paragraph.Split(' ');
                string currentLine = "";

                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    Vector2 size = font.MeasureString(testLine);

                    if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                    {
                        if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                            spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                        currentY += (int)lineSpacing;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                        spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                    currentY += (int)lineSpacing;
                }

                currentY += (int)(lineSpacing * 0.5f);
            }

            return currentY;
        }

        private int DrawLongDescriptionWithSubtitles(SpriteBatch spriteBatch, SpriteFont font, string text, int x, int y, float maxWidth, Color color, float lineSpacing, Rectangle scissorRect, float subtitleScale)
        {
            string[] paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int currentY = y;
            string[] subtitles = { "Defines", "Exists", "Links" };
            int subtitleIndex = 0;

            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    currentY += (int)lineSpacing;
                    continue;
                }

                if (subtitleIndex < subtitles.Length)
                {
                    string subtitle = subtitles[subtitleIndex];
                    Vector2 subtitleSize = font.MeasureString(subtitle) * subtitleScale;
                    Vector2 subtitlePos = new Vector2(x, currentY);

                    if (currentY >= scissorRect.Y && currentY + subtitleSize.Y <= scissorRect.Bottom)
                        spriteBatch.DrawString(font, subtitle, subtitlePos, PROJECT_NAME_COLOR, 0f, Vector2.Zero, subtitleScale, SpriteEffects.None, 0f);
                    currentY += (int)(subtitleSize.Y + 5);
                    subtitleIndex++;
                }

                string[] words = paragraph.Split(' ');
                string currentLine = "";

                foreach (string word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    Vector2 size = font.MeasureString(testLine);

                    if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                    {
                        if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                            spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                        currentY += (int)lineSpacing;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    if (currentY >= scissorRect.Y && currentY <= scissorRect.Bottom)
                        spriteBatch.DrawString(font, currentLine, new Vector2(x, currentY), color);
                    currentY += (int)lineSpacing;
                }

                currentY += (int)(lineSpacing * 0.8f);
            }

            return currentY;
        }

        private void UpdateRightPanelScrolling()
        {
            if (_windowManagement == null || !_windowManagement.IsVisible())
                return;

            _rightPanelContentHeight = CalculateRightPanelContentHeight();

            _rightPanelNeedsScrollbar = _rightPanelContentHeight > _rightPanelBounds.Height;

            if (_rightPanelNeedsScrollbar)
            {
                _rightPanelScrollbarBounds = new Rectangle(
                    _rightPanelBounds.Right - SCROLLBAR_WIDTH - 2,
                    _rightPanelBounds.Y,
                    SCROLLBAR_WIDTH,
                    _rightPanelBounds.Height
                );

                int maxScroll = Math.Max(0, _rightPanelContentHeight - _rightPanelBounds.Height);
                _rightPanelScrollY = MathHelper.Clamp(_rightPanelScrollY, 0, maxScroll);
            }
            else
            {
                _rightPanelScrollY = 0;
                _rightPanelScrollbarBounds = Rectangle.Empty;
            }
        }

        private int CalculateRightPanelContentHeight()
        {
            if (_uiFont == null) return 0;

            float headerBoldScale = 1.3f;
            float subtitleScale = 1.1f;
            float lineSpacing = _uiFont.LineSpacing * 1.2f;
            float descriptionLineSpacing = _uiFont.LineSpacing * 1.1f;
            float maxWidth = _rightPanelBounds.Width - PANEL_PADDING * 2 - SCROLLBAR_WIDTH;
            int height = PANEL_PADDING;

            if (_currentView == "All")
            {
                string[] entityTypes = { "Character", "Trait", "Skill", "Effect", "Stat", "Tag" };

                foreach (string entityType in entityTypes)
                {
                    if (_entityDescriptionsShort.ContainsKey(entityType))
                    {
                        Vector2 headerSize = _uiFont.MeasureString(entityType) * headerBoldScale;
                        height += (int)headerSize.Y + 10;

                        string description = _entityDescriptionsShort[entityType];
                        string[] words = description.Split(' ');
                        string currentLine = "";
                        int lines = 0;

                        foreach (string word in words)
                        {
                            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                            Vector2 size = _uiFont.MeasureString(testLine);

                            if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                            {
                                lines++;
                                currentLine = word;
                            }
                            else
                            {
                                currentLine = testLine;
                            }
                        }
                        if (!string.IsNullOrEmpty(currentLine)) lines++;

                        height += (int)(lines * descriptionLineSpacing);
                        height += 30;
                    }
                }
            }
            else
            {
                string entityType = _currentView;
                if (_tabToEntityType.ContainsKey(_currentView))
                    entityType = _tabToEntityType[_currentView];

                if (_entityDescriptionsLong != null && _entityDescriptionsLong.ContainsKey(entityType))
                {
                    Vector2 headerSize = _uiFont.MeasureString(entityType) * headerBoldScale;
                    height += (int)headerSize.Y + 15;

                    string description = _entityDescriptionsLong[entityType];
                    string[] paragraphs = description.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    int lines = 0;
                    string[] subtitles = { "Defines", "Exists", "Links" };
                    int subtitleIndex = 0;

                    foreach (string paragraph in paragraphs)
                    {
                        if (string.IsNullOrWhiteSpace(paragraph))
                        {
                            lines++;
                            continue;
                        }

                        if (subtitleIndex < subtitles.Length)
                        {
                            Vector2 subtitleSize = _uiFont.MeasureString(subtitles[subtitleIndex]) * subtitleScale;
                            height += (int)(subtitleSize.Y + 5);
                            subtitleIndex++;
                        }

                        string[] words = paragraph.Split(' ');
                        string currentLine = "";

                        foreach (string word in words)
                        {
                            string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                            Vector2 size = _uiFont.MeasureString(testLine);

                            if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                            {
                                lines++;
                                currentLine = word;
                            }
                            else
                            {
                                currentLine = testLine;
                            }
                        }
                        if (!string.IsNullOrEmpty(currentLine)) lines++;
                        lines++;
                    }

                    height += (int)(lines * descriptionLineSpacing);
                }
                else if (_entityDescriptionsShort != null && _entityDescriptionsShort.ContainsKey(entityType))
                {
                    Vector2 headerSize = _uiFont.MeasureString(entityType) * headerBoldScale;
                    height += (int)headerSize.Y + 10;

                    string description = _entityDescriptionsShort[entityType];
                    string[] words = description.Split(' ');
                    string currentLine = "";
                    int lines = 0;

                    foreach (string word in words)
                    {
                        string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                        Vector2 size = _uiFont.MeasureString(testLine);

                        if (size.X > maxWidth && !string.IsNullOrEmpty(currentLine))
                        {
                            lines++;
                            currentLine = word;
                        }
                        else
                        {
                            currentLine = testLine;
                        }
                    }
                    if (!string.IsNullOrEmpty(currentLine)) lines++;

                    height += (int)(lines * descriptionLineSpacing);
                }
            }

            height += PANEL_PADDING;
            return height;
        }

        private void HandleRightPanelScrollbarInteraction()
        {
            if (!_rightPanelNeedsScrollbar) return;

            var mousePosition = _currentMouseState.Position;

            if (_currentMouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (_rightPanelScrollbarBounds.Contains(mousePosition))
                {
                    _isDraggingRightPanelScrollbar = true;
                    _rightPanelScrollbarDragStart = new Vector2(mousePosition.X, mousePosition.Y);
                }
            }

            if (_isDraggingRightPanelScrollbar)
            {
                if (_currentMouseState.LeftButton == ButtonState.Pressed)
                {
                    float deltaY = mousePosition.Y - _rightPanelScrollbarDragStart.Y;
                    float scrollbarHeight = _rightPanelScrollbarBounds.Height;

                    float scrollRatio = deltaY / scrollbarHeight;
                    _rightPanelScrollY = (int)(scrollRatio * (_rightPanelContentHeight - _rightPanelBounds.Height));

                    int maxScroll = Math.Max(0, _rightPanelContentHeight - _rightPanelBounds.Height);
                    _rightPanelScrollY = MathHelper.Clamp(_rightPanelScrollY, 0, maxScroll);
                }
                else
                {
                    _isDraggingRightPanelScrollbar = false;
                }
            }

            if (_rightPanelBounds.Contains(mousePosition) &&
                _currentMouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
            {
                int delta = _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                _rightPanelScrollY -= delta / 3;

                int maxScroll = Math.Max(0, _rightPanelContentHeight - _rightPanelBounds.Height);
                _rightPanelScrollY = MathHelper.Clamp(_rightPanelScrollY, 0, maxScroll);
            }
        }

        private void DrawRightPanelScrollbar(SpriteBatch spriteBatch)
        {
            if (!_rightPanelNeedsScrollbar || _rightPanelScrollbarBounds.IsEmpty) return;
            if (_pixel == null) return;

            spriteBatch.Draw(_pixel, _rightPanelScrollbarBounds, new Color(55, 65, 81));
            DrawBorder(spriteBatch, _rightPanelScrollbarBounds, PANEL_BORDER);

            float contentRatio = (float)_rightPanelBounds.Height / Math.Max(1, _rightPanelContentHeight);
            int thumbHeight = Math.Max(20, (int)(_rightPanelScrollbarBounds.Height * contentRatio));

            int maxScroll = Math.Max(1, _rightPanelContentHeight - _rightPanelBounds.Height);
            int thumbY = _rightPanelScrollbarBounds.Y + (int)((_rightPanelScrollbarBounds.Height - thumbHeight) * (_rightPanelScrollY / (float)maxScroll));

            var thumbBounds = new Rectangle(_rightPanelScrollbarBounds.X + 2, thumbY + 2, _rightPanelScrollbarBounds.Width - 4, thumbHeight - 4);
            bool isThumbHovered = thumbBounds.Contains(_hoverMousePosition) || _isDraggingRightPanelScrollbar;
            Color thumbColor = isThumbHovered ? BUTTON_HOVER : BUTTON_COLOR;

            spriteBatch.Draw(_pixel, thumbBounds, thumbColor);
            DrawBorder(spriteBatch, thumbBounds, PANEL_BORDER);
        }
    }
}
