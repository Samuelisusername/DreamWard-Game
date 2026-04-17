using System;
using System.Linq; // Added this so we can calculate max health!
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
        // we only draw monsters here since they dont fall        
        // 1. grounded monsters
        // 2. flying monsers


        if (_pixel == null) return;

        Vector2 scale = mapManager.Scale;

        // Draw HP bars
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
                else if (structure is Fence || structure is Tent)
                {
                    Vector2 worldPos = mapManager.GetWorldPositionOfTile((i, j));
                    DrawHpBar(spriteBatch, worldPos, scale,
                              structure.CurrentHealth, structure.MaxHealth, -10f);
                }

                // Look inside the Container to draw HP bars for the groups!
                var container = mapManager.GetMonsterContainer((i, j));
                if (container != null && !container.IsEmpty)
                {
                    foreach (var group in container.MonsterGroups)
                    {
                        if (!group.IsEmpty)
                        {
                            Vector2 worldPos = group.GetInterpolatedWorldPosition();
                            
                            // Calculate total current and max HP for this specific group
                            float currentHp = group.Health;
                            float maxHp = group.Monsters.Sum(m => m.MaxHealth);
                            
                            DrawHpBar(spriteBatch, worldPos, scale,
                                      currentHp, maxHp, -10f);
                        }
                    }
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