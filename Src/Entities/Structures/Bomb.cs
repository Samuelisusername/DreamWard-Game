using System;
using System.Linq;
using Gamelab;
using Gamelab.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

public class Bomb : Tower
{    
    protected TimeSpan timeToBlow = TimeSpan.FromSeconds(1f);
    protected TimeSpan blowProgress = TimeSpan.Zero;
    public bool ShouldBlowUp {get; set;}

    private bool resetBlowProgress = true;
    public Bomb((int, int) pos) : base("towerAOE_idle", pos)
    {
    }

    protected override (string label, string info)[] infos => [("Health:", $"{CurrentHealth}"), ("DPS:", $"{damagePerSecond}")];

    protected override string bottomText => !gamePhaseManager.IsDay? "Click and hold blow up" : "Cant blow up during the day";

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (resetBlowProgress)
        { 
            blowProgress = TimeSpan.Zero;
        }
        resetBlowProgress = true;
    }

    public bool AddProgress(TimeSpan delta)
    {
        if (gamePhaseManager.IsDay) return false;
        resetBlowProgress = false;
        blowProgress += delta;
        if (blowProgress > timeToBlow)
        {
            return true;
        }
        return false;
    }

    public override void Attack(GameTime gameTime)
    {
        if(ShouldBlowUp)
        {
            var groupsInRage = mapManager.GetMonsterGroupsInRange(TilePosition, rangeDepth);

            foreach (MonsterGroup group in groupsInRage)
            {
                group.TakeDamageAOE(damagePerSecond);
                string soundToPlay = "AOE-tower_stomp";

                AudioManager.Play(soundToPlay);
            }
            CurrentHealth = 0;
            Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
            for(int i = 0; i < 40; i++)
            {
                ParticleManager.AddParticle(new Particle
                {
                    WorldPosition = (worldPos + (_localOffset * _spriteTop.Scale) + (new Vector2(64, 100) * _spriteTop.Scale) - mapManager.Offset)/mapManager.Scale,
                    Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 150, (float)(Random.Shared.NextDouble() -0.5f) * 150),
                    LifeSpan = TimeSpan.FromSeconds(0.5),
                    Color = Color.DarkRed,
                    Updater = (particle, gameTime) =>
                    {            
                        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                        particle.Color  = Color.Lerp(particle.Color, Color.Black , 3f * dt);
                        particle.Scale += 0.5f * dt;
                    }
                });
            }
            mapManager.InitScreenShake(TimeSpan.FromSeconds(0.3f), 7);

        }
    }
    
    public override void DrawCard(SpriteBatch spriteBatch, Rectangle screen)
    {
        if (!ShouldDrawCard) return;

        Color rectColor = gamePhaseManager.IsDay ? COLOR_L : new Color(33, 28, 11);
        Color loadColor = gamePhaseManager.IsDay ? COLOR_G : new Color(33, 28, 11);
        Color textColor = gamePhaseManager.IsDay ? COLOR_D : new Color(214, 214, 214);
        Color goldColor = new Color(255, 210, 80);
        Color dimColor  = textColor * 0.55f;

        float sx = MathF.Max(1f, MathF.Floor(_screenTransform.M11));
        float sy = MathF.Max(1f, MathF.Floor(_screenTransform.M22));

        Vector2 worldPos  = mapManager.GetWorldPositionOfTile(TilePosition);
        int pad = 10, midPad = 50, heightPad = 50;
        int sepH = (int)MathF.Round(2 * sy);

        // ── Local helpers ──────────────────────────────────────────────────────
        float MeasW(string s) => _font.MeasureString(s).X;
        float MeasH(string s) => _font.MeasureString(s).Y;
        int   ScW(string s)   => (int)MathF.Round((MeasW(s) + 2 * pad) * sx);
        int   ScH(string s)   => (int)MathF.Round((MeasH(s) + pad) * sy);

        Vector2 ToScreen(float x, float y) => new(
            MathF.Round(x * _screenTransform.M11 + _screenTransform.M41),
            MathF.Round(y * _screenTransform.M22 + _screenTransform.M42));

        // ── Box sizing ─────────────────────────────────────────────────────────
        int boxWidth = (int)MathF.Round(
            (infos.Cast<(string,string)>().Max(i => MeasW(i.Item1 + i.Item2)) + 2 * pad + midPad) * sx);
        int boxHeight = (int)MathF.Round(
            (infos.Cast<(string,string)>().Sum(i => MeasH(i.Item1)) + pad * (infos.Length + 1)) * sy);

        if (bottomText != null)
        {
            boxWidth   = Math.Max(boxWidth, ScW(bottomText));
            boxHeight += (int)MathF.Round((MeasH(bottomText) + heightPad) * sy);
        }

        // ── Layout anchors ─────────────────────────────────────────────────────
        Vector2 anchor = ToScreen(worldPos.X + 31.5f * mapManager.Scale.X, worldPos.Y);
        if((int)(anchor.X - boxWidth / 2f) < screen.Left + (2*pad))
        {
            anchor.X = screen.Left + (2*pad) + (boxWidth / 2f);
        }        
        else if((int)(anchor.X - boxWidth / 2f) + boxWidth > screen.Right - (2*pad))
        {
            anchor.X  = screen.Right - boxWidth - (2*pad)  + (boxWidth / 2f);
        }
        if((int)(anchor.Y - MathF.Round(16 * mapManager.Scale.X * sy) - boxHeight) < screen.Top + (2*pad))
        {
            anchor.Y = screen.Top + (2*pad) + MathF.Round(16 * mapManager.Scale.X * sy) + boxHeight;
        }
        int left = (int)(anchor.X - boxWidth / 2f);
        int top  = (int)(anchor.Y - MathF.Round(16 * mapManager.Scale.X * sy) - boxHeight);

        spriteBatch.Draw(_dummy, new Rectangle(left, top, boxWidth, boxHeight), rectColor * 0.5f);

        // ── Draw helpers ───────────────────────────────────────────────────────
        float yOff = MathF.Round(pad * sy);
        void DrawCentred(string s, Color c) {
            spriteBatch.DrawString(_font, s,
                new Vector2((int)(anchor.X - MeasW(s) * sx / 2f), (int)(top + yOff)),
                c, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(s) + pad) * sy);
        }
        void DrawSep() {
            spriteBatch.Draw(_dummy,
                new Rectangle(left + (int)MathF.Round(pad * sx), (int)(top + yOff),
                            boxWidth - (int)MathF.Round(2 * pad * sx), sepH), dimColor);
            yOff += sepH;
        }

        // ── Stat rows ──────────────────────────────────────────────────────────
        for (int i = 0; i < infos.Length; i++)
        {
            spriteBatch.DrawString(_font, infos[i].label,
                new Vector2(left + MathF.Round(pad * sx), top + yOff),
                textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            float infoW = MeasW(infos[i].info) * sx;
            spriteBatch.DrawString(_font, infos[i].info,
                new Vector2((int)(left + boxWidth - MathF.Round(pad * sx) - infoW), top + yOff),
                textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(infos[i].info) + pad) * sy);
        }

        // ── Hold-to-move bar ───────────────────────────────────────────────────
        if (bottomText != null)
        {
            float textW = MeasW(bottomText) * sx;
            int   barH  = (int)MathF.Round(MeasH(bottomText) * sy);
            var   pos   = new Vector2((int)(anchor.X - textW / 2f),
                                    (int)(top + yOff - MathF.Round(pad * sy) + MathF.Round(heightPad * sy)));
            Color barColor = loadColor * 0.5f;
            int fillW = (int)MathF.Round(textW * (float)(blowProgress / timeToBlow));
            spriteBatch.Draw(_dummy, new Rectangle((int)pos.X, (int)pos.Y, fillW, barH), barColor);
            spriteBatch.DrawString(_font, bottomText, pos, textColor,
                0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(bottomText) + heightPad) * sy);
        }
    }
}