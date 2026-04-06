using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gamelab;

public class EntityRenderer
{
    private MapManager mapManager => GamelabGame.MapManager;
    private Texture2D _pixel;

    public void SetHealthBarTexture(Texture2D pixel)
    {
        _pixel = pixel;
    }

    public void Draw(SpriteBatch spriteBatch, (int col, int row)? skipTile = null)
    {
        for (int i = 0; i < mapManager.Rows*6; i++)
        {
            for (int j = i % 2; j < mapManager.Columns*3; j += 2)
            {                
                (int col, int row) tilePos = (j - mapManager.Columns, (i / 2)-mapManager.Rows);
                if (skipTile.HasValue && skipTile.Value == tilePos)
                continue;
                mapManager.GetStructure((j,i/2), true)?.Draw(spriteBatch);
                mapManager.GetMonsterGroup((tilePos.col,tilePos.row))?.Draw(spriteBatch);
            }
        }
        for(int i = 0; i < mapManager.Rows * 2; i++)
        {
            for(int j = i % 2; j < mapManager.Columns; j += 2)
            {
            }
        }

        if (_pixel == null) return;

        Vector2 scale = mapManager.Scale;

        // Draw HP bars for towers only
        for(int i = 0; i < mapManager.Columns; i++)
        {
            for(int j = 0; j < mapManager.Rows; j++)
            {
                var structure = mapManager.GetStructure((i, j));
                if (structure is Tower)
                {
                    Vector2 worldPos = mapManager.GetWorldPositionOfTile((i, j));
                    DrawHpBar(spriteBatch, worldPos, scale,
                              structure.CurrentHealth, structure.MaxHealth, -80f);
                }
                else if (structure is Fence)
                {
                    Vector2 worldPos = mapManager.GetWorldPositionOfTile((i, j));
                    DrawHpBar(spriteBatch, worldPos, scale,
                              structure.CurrentHealth, structure.MaxHealth, -10f);
                }
                else if (structure is Tent)
                {
                    Vector2 worldPos = mapManager.GetWorldPositionOfTile((i, j));
                    DrawHpBar(spriteBatch, worldPos, scale,
                              structure.CurrentHealth, structure.MaxHealth, -10f);
                }

                var group = mapManager.GetMonsterGroup((i, j));
                if (group != null && group.Count > 0)
                {
                    Vector2 worldPos = group.GetInterpolatedWorldPosition();
                    DrawHpBar(spriteBatch, worldPos, scale,
                              group.TotalCurrentHealth, group.TotalMaxHealth, -10f);
                }
            }
        }
    }

    // Sprite localOffset is (-32, -90). The sprite is 64px wide unscaled.
    // HP bar sits just above the sprite, centered on it.
    private void DrawHpBar(SpriteBatch spriteBatch, Vector2 worldPos, Vector2 scale,
                           float currentHealth, float maxHealth, float spriteOffsetY)
    {
        float barWidth = 37.5f * scale.X;
        float barHeight = 3f * scale.Y;

        // sprite top-left is at worldPos + (-32, -90)*scale
        // center bar on the 64px-wide sprite, place it just above the sprite top
        float spriteLeftX = worldPos.X + (-32f) * scale.X;
        float spriteCenterX = spriteLeftX + 32f * scale.X;
        float barX = spriteCenterX - barWidth / 2f + 30f * scale.X;
        float barY = worldPos.Y + spriteOffsetY * scale.Y - barHeight - 2f * scale.Y;

        // Background (dark gray)
        spriteBatch.Draw(_pixel, new Rectangle(
            (int)barX, (int)barY, (int)barWidth, (int)barHeight),
            new Color(40, 40, 40));

        // Fill (green → red based on %)
        float pct = Math.Clamp(currentHealth / maxHealth, 0f, 1f);
        Color barColor = pct > 0.5f ? Color.LimeGreen : pct > 0.25f ? Color.Orange : Color.Red;
        spriteBatch.Draw(_pixel, new Rectangle(
            (int)barX, (int)barY, (int)(barWidth * pct), (int)barHeight),
            barColor);
    }
}