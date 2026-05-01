using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Gamelab;

public class EntityRenderer
{
    private MapManager mapManager => GamelabGame.MapManager;
    private GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private Texture2D _pixel;

    private readonly Dictionary<Structure, float> _hpBarAlpha    = new();
    private readonly Dictionary<Structure, float> _lastHealth    = new();
    private readonly Dictionary<Structure, float> _damageTimer   = new();

    public (int col, int row)? HoveredTile   { get; set; }
    public (int col, int row)? HealFlashTile { get; set; }

    // Palette — day / night variants
    // rgb(232, 220,  191), rgb(188, 182,  157), rgb(131,  130,  79)};
    // rgb(188, 182, 157), rgb( 116, 112, 95), rgb(76, 76, 76)
    // rgb( 105, 105, 93);
    // rgb( 59,  58,  58);

    private static readonly Color[] DayFill   = { new(232, 220,  191), new(232, 220,  191), new(232, 220,  191) };
    private static readonly Color[] NightFill = { new(188, 182, 157), new( 188, 182, 157), new(188, 182, 157) };
    private static readonly Color   DayBg     = new( 105, 105, 93);
    private static readonly Color   NightBg   = new( 59,  58,  58);

    public void SetHealthBarTexture(Texture2D pixel) => _pixel = pixel;

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        for (int i = 0; i < mapManager.Columns; i++)
        {
            for (int j = 0; j < mapManager.Rows; j++)
            {
                var s = mapManager.GetStructure((i, j));
                if (s == null) continue;

                bool damaged  = s.CurrentHealth < s.MaxHealth;
                bool hovered  = HoveredTile == (i, j);
                bool flashing = HealFlashTile == (i, j);

                // Snap to 1 and start damage timer on hit
                if (_lastHealth.TryGetValue(s, out float prev) && s.CurrentHealth < prev)
                {
                    _hpBarAlpha[s]  = 1f;
                    _damageTimer[s] = 4f * (1f - s.CurrentHealth / s.MaxHealth);
                }
                _lastHealth[s] = s.CurrentHealth;
                if (_damageTimer.TryGetValue(s, out float t))
                    _damageTimer[s] = MathF.Max(0f, t - dt);

                float target  = (damaged || hovered || flashing) ? 1f : 0f;
                float current = _hpBarAlpha.GetValueOrDefault(s, 0f);
                bool recentlyDamaged = _damageTimer.GetValueOrDefault(s) > 0f;
                float speed   = target > current ? 10f : recentlyDamaged ? 1f : 10f;
                _hpBarAlpha[s] = MathHelper.Lerp(current, target, 1f - MathF.Exp(-speed * dt));
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, (int col, int row)? skipTile = null)
    {
        if (_pixel == null) return;

        Vector2 scale = mapManager.Scale;
        bool isDay    = gamePhaseManager.IsDay;

        for (int i = 0; i < mapManager.Columns; i++)
        {
            for (int j = 0; j < mapManager.Rows; j++)
            {
                var structure = mapManager.GetStructure((i, j));
                if (structure is SniperTower or AreaTower or Wall or WallTile
                              or Fence or PlacableMeadow
                              || (structure is Meadow m && m.HasSheep))
                {
                    float alpha = _hpBarAlpha.GetValueOrDefault(structure, 0f);
                    if (alpha < 0.01f) continue;

                    bool flash = HealFlashTile == (i, j);
                    float pulse = flash
                        ? (float)(Math.Sin(Environment.TickCount64 / 150.0) * 0.35 + 0.65)
                        : 1f;

                    float displayHealth = flash ? structure.MaxHealth : structure.CurrentHealth;
                    Vector2 worldPos    = mapManager.GetWorldPositionOfTile((i, j));
                    DrawStructureHpBar(spriteBatch, worldPos, scale, displayHealth, structure.MaxHealth,
                                       alpha * pulse, isDay);
                }

                var container = mapManager.GetMonsterContainer((i, j));
                if (container != null && !container.IsEmpty)
                {
                    foreach (var group in container.MonsterGroups)
                    {
                        if (group.IsEmpty) continue;
                        float currentHp = group.Health;
                        float maxHp     = group.Monsters.Sum(m => m.MaxHealth);
                        if (currentHp >= maxHp) continue;

                        float? shadowHp    = group.Count > 1 ? group.Monsters[1].CurrentHealth : null;
                        float? shadowMaxHp = group.Count > 1 ? group.Monsters[1].MaxHealth     : null;

                        Vector2 worldPos = group.GetInterpolatedWorldPosition()
                            + ((group.LocalOffset + new Vector2(32, 90)) * scale);
                        DrawMonsterHpBar(spriteBatch, worldPos, scale, currentHp, maxHp, shadowHp, shadowMaxHp, isDay);
                    }
                }
            }
        }
    }

    private void DrawStructureHpBar(SpriteBatch spriteBatch, Vector2 tilePos, Vector2 scale,
                                    float current, float max, float alpha, bool isDay)
    {
        const int segments = 5;
        float segW = 3f * scale.X;
        float gap  = 1f * scale.X;
        float barW = segments * segW + (segments - 1) * gap;
        float barH = segW;

        float barX = tilePos.X + mapManager.TileWidth  / 2f - barW / 2f;
        float barY = tilePos.Y + mapManager.TileHeight - barH - 2f * scale.Y;

        DrawSegmentedBar(spriteBatch, barX, barY, segW, gap, barH, segments, current, max, alpha, isDay);
    }

    private void DrawMonsterHpBar(SpriteBatch spriteBatch, Vector2 anchorPos, Vector2 scale,
                                  float current, float max, float? shadowHp, float? shadowMax, bool isDay)
    {
        const int segments = 5;
        float segW = 3f * scale.X;
        float gap  = 1f * scale.X;
        float barH = segW;

        float barX = anchorPos.X - (segments * segW + (segments - 1) * gap) / 2f + 32f*scale.X;
        float barY = anchorPos.Y - 12f * scale.Y - barH;

        if (shadowHp.HasValue)
            DrawSegmentedBar(spriteBatch, barX + segW, barY + segW, segW, gap, barH,
                             segments, shadowHp.Value, shadowMax!.Value, 0.5f, isDay);

        DrawSegmentedBar(spriteBatch, barX, barY, segW, gap, barH, segments, current, max, 1f, isDay);
    }

    private void DrawSegmentedBar(SpriteBatch spriteBatch, float barX, float barY,
                                  float segW, float gap, float barH, int segments,
                                  float current, float max, float alpha, bool isDay)
    {
        float pct         = Math.Clamp(current / max, 0f, 1f);
        int   filledCount = (int)Math.Ceiling(pct * segments);
        Color[] palette   = isDay ? DayFill : NightFill;
        Color fill        = pct > 0.6f ? palette[0] : pct > 0.25f ? palette[1] : palette[2];
        Color bg          = isDay ? DayBg : NightBg;

        float totalW = segments * segW + (segments - 1) * gap;
        float pad    = gap;
        spriteBatch.Draw(_pixel, new Vector2(barX - pad, barY - pad), null, bg * alpha,
                         0f, Vector2.Zero, new Vector2(totalW + pad * 2, barH + pad * 2),
                         SpriteEffects.None, 0f);

        for (int s = 0; s < segments; s++)
        {
            float sx = barX + s * (segW + gap);
            Color c  = s < filledCount ? fill : bg;
            spriteBatch.Draw(_pixel, new Vector2(sx, barY), null, c * alpha,
                             0f, Vector2.Zero, new Vector2(segW, barH),
                             SpriteEffects.None, 0f);
        }
    }
}
