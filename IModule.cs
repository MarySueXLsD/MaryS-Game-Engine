using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using System;

namespace MarySGameEngine;

public interface IModule : IDisposable
{
    void Update();
    void Draw(SpriteBatch spriteBatch);
    void UpdateWindowWidth(int width);
    void LoadContent(ContentManager content);
} 