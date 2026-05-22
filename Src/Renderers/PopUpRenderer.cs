using System;
using System.Collections.Generic;
using System.Linq;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public static class PopUpRenderer
{

#region Managers
    private static GamePhaseManager gamePhaseManager => GamelabGame.GamePhaseManager;
    private static MapManager mapManager => GamelabGame.MapManager;
#endregion

#region drawparams

    private static readonly Color COLOR_D = gamePhaseManager.IsDay ? new(0x29, 0x24, 0x10) : new Color(214, 214, 214);    
    private static readonly Color COLOR_G = gamePhaseManager.IsDay ? new Color(0x7d, 0x64, 0x48)  : new Color(33, 28, 11);
    private static readonly Color COLOR_L =  gamePhaseManager.IsDay ? new Color(0xc7, 0xb8, 0x9c) : new Color(33, 28, 11);
    
    public static Matrix ScreenTransform {get; set;}

    
    private static Texture2D _dummy = GamelabGame.DummyText;
    private static SpriteFont _font = GamelabGame.Font;
    private static SpriteFont _hintFont = GamelabGame.HintFont;

    private static string continueHint = "(Press Space to continue)";
    
    private static Sprite blankPortrait;
    private static Sprite sheepPortrait;

#endregion

    public static void Reset()
    {
        
        blankPortrait = GamelabGame.TextureAtlas["day_player"].CreateSprite("player0");
        blankPortrait.Scale = Vector2.One * 2;
        blankPortrait.Origin = new Vector2(2,18);
        
        sheepPortrait = GamelabGame.TextureAtlas["day_top"].CreateSprite("sheepBig0");
        sheepPortrait.Scale = Vector2.One * 2;
        sheepPortrait.Origin = new Vector2(49,112);
    }

    public static void Draw(SpriteBatch spriteBatch, Rectangle screen)
    {
        if(TutorialManager.CurrentWarningStructure != null)
        {
            //we are displaying a warning everything else shouldnt be displayed
            DrawDialog(spriteBatch, TutorialManager.CurrentWarningStructure, TutorialManager.WarningText, TutorialManager.WarningText, screen);

            return;
        }
        else if(TutorialManager.CurrentInfoStructure != null)
        {
            //figure out the priority of tutoial vs popup
            DrawInfo(spriteBatch, TutorialManager.CurrentInfoStructure, screen);
        }
        else if(TutorialManager.CurrentTutorialStructure != null && gamePhaseManager.IsDay)
        {
            DrawDialog(spriteBatch, TutorialManager.CurrentTutorialStructure, TutorialManager.TutorialText, TutorialManager.TutorialFullText, screen);
        }
    }

    private static void DrawDialog(SpriteBatch spriteBatch, Structure structure, string text, string fullText, Rectangle screen)
    {        
        Color rectColor = COLOR_L;
        Color textColor = COLOR_D;

        // Floor to integer scale so every font stroke occupies whole screen pixels without growing larger than intended.
        float sx = MathF.Max(1f, MathF.Floor(ScreenTransform.M11));
        float sy = MathF.Max(1f, MathF.Floor(ScreenTransform.M22));
        Vector2 worldPos = mapManager.GetWorldPositionOfTile(structure.TilePosition);
        int pad = 10;
        int maxTutorialWidth = 300;

        int boxHeight;
        int boxWidth;
        int portWid = structure is Tent ? 11 : 23;

        Sprite portrait = structure is Tent ? blankPortrait : sheepPortrait;

        Vector2 boxDimensions = PopUpRenderer.ProcessTextWithIcons(null, _font, text, Vector2.Zero, Color.White, maxTutorialWidth * sx, new Vector2(sx, sy), true);
        Vector2 hintSize = TutorialManager.BadString.Contains(fullText) ? Vector2.Zero: _hintFont.MeasureString(continueHint) * new Vector2(sx, sy);
        boxWidth = (int)MathF.Max(boxDimensions.X, hintSize.X)  + (pad * 4) + (int)(portrait.Scale.X * portWid);
        boxHeight = (int)(boxDimensions.Y + hintSize.Y) + (pad * 3);

        

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

        


        Vector2 textStartPos = new Vector2(left + MathF.Round(((3 * pad) + (int)(portrait.Scale.X * portWid)) * sx), top + MathF.Round(pad * sy));
        portrait.Draw(spriteBatch, new Vector2(left + MathF.Round(pad * sx), top + MathF.Round(boxHeight/2 * sy)));    
        // Pass false for isMeasuring, pass the actual SpriteBatch
        var dialogBox = PopUpRenderer.ProcessTextWithIcons(spriteBatch, _font, text, textStartPos, textColor, maxTutorialWidth * sx, new Vector2(sx, sy), false);
        
        if(!TutorialManager.BadString.Contains(fullText))
        {
            Vector2 hintPos = new Vector2(
                left + (portrait.Scale.X * portWid) + (2 * pad) + ((boxWidth -(portrait.Scale.X * portWid) - (2*pad))  / 2) - ((_hintFont.MeasureString(continueHint).X * sx) / 2), // Centered
                top + dialogBox.Y  + (2 * pad)
            );

            spriteBatch.DrawString(_hintFont, continueHint, hintPos, textColor * 0.7f, 0, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0);
            //spriteBatch.DrawString(_font, displayText, new Vector2(left + MathF.Round(pad * sx), top + MathF.Round(pad * sy)) , textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
        }
    }
    private static void DrawInfo(SpriteBatch spriteBatch, Structure structure, Rectangle screen)
    {        
        Color rectColor = COLOR_L;
        Color loadColor = COLOR_G;
        Color textColor = COLOR_D;
        Color goldColor = new Color(255, 210, 80);
        Color dimColor  = textColor * 0.55f;

        float sx = MathF.Max(1f, MathF.Floor(ScreenTransform.M11));
        float sy = MathF.Max(1f, MathF.Floor(ScreenTransform.M22));

        Vector2 worldPos  = mapManager.GetWorldPositionOfTile(structure.TilePosition);
        int pad = 10, midPad = 50, heightPad = 50;
        int sepH = (int)MathF.Round(2 * sy);

        bool hasBonusSection = gamePhaseManager.IsDay && structure is Meadow meadow && meadow.MeadowGrant != null;
        bool showBonusLabel  = hasBonusSection && structure is Meadow med && !string.IsNullOrEmpty(med.BonusLabel);
        string sectionHeader = "Survive the night here to unlock:";

        // ── Build card blocks once ─────────────────────────────────────────────
        var cardBlocks = new List<(string title, string[] body)>();
        if (hasBonusSection &&  structure is Meadow m && m.UnlockCardSprites?.Count > 0)
        {
            foreach (var name in m.UnlockCardSprites)
            {
                var parts = CardRenderer.CardDescriptions.TryGetValue(name, out var d)
                    ? d.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    : new[] { name };
                cardBlocks.Add((parts[0], parts.Length > 1 ? parts[1..] : Array.Empty<string>()));
            }
        }

        // ── Local helpers ──────────────────────────────────────────────────────
        float MeasW(string s) => _font.MeasureString(s).X;
        float MeasH(string s) => _font.MeasureString(s).Y;
        int   ScW(string s)   => (int)MathF.Round((MeasW(s) + 2 * pad) * sx);
        int   ScH(string s)   => (int)MathF.Round((MeasH(s) + pad) * sy);

        Vector2 ToScreen(float x, float y) => new(
            MathF.Round(x * ScreenTransform.M11 + ScreenTransform.M41),
            MathF.Round(y * ScreenTransform.M22 + ScreenTransform.M42));

        // ── Box sizing ─────────────────────────────────────────────────────────
        
        int boxHeight;
        int boxWidth;

        boxWidth = (int)MathF.Round(
            (structure.Infos.Cast<(string,string)>().Max(i => MeasW(i.Item1 + i.Item2)) + 2 * pad + midPad) * sx);
        boxHeight = (int)MathF.Round(
            (structure.Infos.Cast<(string,string)>().Sum(i => MeasH(i.Item1)) + pad * (structure.Infos.Length + 1)) * sy);

        if (structure.BottomText != null)
        {
            boxWidth   = Math.Max(boxWidth, ScW(structure.BottomText));
            boxHeight += (int)MathF.Round((MeasH(structure.BottomText) + heightPad) * sy);
        }

        if (hasBonusSection)
        {
            boxHeight += (int)MathF.Round(pad * sy) + sepH + (int)MathF.Round(pad * sy);
            boxWidth   = Math.Max(boxWidth, ScW(sectionHeader));
            boxHeight += ScH(sectionHeader);

            foreach (var (title, body) in cardBlocks)
            {
                boxWidth   = Math.Max(boxWidth, ScW(title));
                boxHeight += ScH(title);
                foreach (var line in body) { boxWidth = Math.Max(boxWidth, ScW(line)); boxHeight += ScH(line); }
                boxHeight += (int)MathF.Round(pad * sy);
            }

            if (showBonusLabel && structure is Meadow me) { boxWidth = Math.Max(boxWidth, ScW(me.BonusLabel)); boxHeight += ScH(me.BonusLabel); }
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
        for (int i = 0; i < structure.Infos.Length; i++)
        {
            spriteBatch.DrawString(_font, structure.Infos[i].label,
                new Vector2(left + MathF.Round(pad * sx), top + yOff),
                textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            float infoW = MeasW(structure.Infos[i].info) * sx;
            spriteBatch.DrawString(_font, structure.Infos[i].info,
                new Vector2((int)(left + boxWidth - MathF.Round(pad * sx) - infoW), top + yOff),
                textColor, 0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(structure.Infos[i].info) + pad) * sy);
        }

        // ── Hold-to-move bar ───────────────────────────────────────────────────
        if (structure.BottomText != null )
        {
            float textW = MeasW(structure.BottomText) * sx;
            int   barH  = (int)MathF.Round(MeasH(structure.BottomText) * sy);
            var   pos   = new Vector2((int)(anchor.X - textW / 2f),
                                    (int)(top + yOff - MathF.Round(pad * sy) + MathF.Round(heightPad * sy)));
            Color barColor = structure.CantAffordFlash > TimeSpan.Zero
                ? Color.Red * ((float)(structure.CantAffordFlash / Meadow.FlashDuration) * 0.6f)
                : loadColor * 0.5f;
            int fillW = structure.CantAffordFlash > TimeSpan.Zero
                ? (int)textW
                : (int)MathF.Round(textW * (float)(structure.BarProgress / structure.BarComplete));
            spriteBatch.Draw(_dummy, new Rectangle((int)pos.X, (int)pos.Y, fillW, barH), barColor);
            spriteBatch.DrawString(_font, structure.BottomText, pos, textColor,
                0f, Vector2.Zero, new Vector2(sx, sy), SpriteEffects.None, 0f);
            yOff += MathF.Round((MeasH(structure.BottomText) + heightPad) * sy);
        }

        // ── Bonus section ──────────────────────────────────────────────────────
        if (hasBonusSection && structure is Meadow medo)
        {
            yOff += MathF.Round(pad * sy);
            DrawSep();
            yOff += MathF.Round(pad * sy);
            DrawCentred(sectionHeader, dimColor);
            foreach (var (title, body) in cardBlocks)
            {
                DrawCentred(title, goldColor);
                foreach (var line in body) DrawCentred(line, textColor);
                yOff += MathF.Round(pad * sy);
            }
            if (showBonusLabel) DrawCentred(medo.BonusLabel, goldColor);
        }
        
    }

    public static Vector2 ProcessTextWithIcons(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 startPosition, Color color, float maxLineWidth, Vector2 scale, bool isMeasuring) 
    {
        if(text == null) return Vector2.Zero;
        // Clean up the text and isolate our tag so it always splits neatly
        text = text.Replace("\n", " ").Replace("[SANITY]", " [SANITY] ").Replace("[DAYICON]", " [DAYICON] ");
        while (text.Contains("  ")) text = text.Replace("  ", " ");

        string[] words = text.Split(' ');
        
        float currentX = 0f;
        float currentY = 0f;
        float maxX = 0f;
        float spaceWidth = font.MeasureString(" ").X * scale.X;
        float lineHeight = font.LineSpacing * scale.Y;
        TextureRegion sanityIcon = GamelabGame.TextureAtlas["day_interface"].GetRegion("sanity_small0");
        TextureRegion dayIcon = GamelabGame.TextureAtlas["day_interface"].GetRegion("time_small0");

        foreach (string word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;

            float itemWidth;
            float itemHeight;

            bool isSanityIcon = word == "[SANITY]";
            bool IsDayIcon = word == "[DAYICON]";

            if (isSanityIcon)
            {
                // Adjust icon scale here if your texture is too big/small
                itemWidth = sanityIcon.Width * scale.X * 1.3f; 
                itemHeight = sanityIcon.Height * scale.Y * 1.3f;
            }
            else if(IsDayIcon)
            {
                itemWidth = dayIcon.Width * scale.X * 1.3f; 
                itemHeight = dayIcon.Height * scale.Y * 1.3f;
            }
            else
            {
                Vector2 wordSize = font.MeasureString(word) * scale;
                itemWidth = wordSize.X;
                itemHeight = wordSize.Y;
            }

            // Wrap to next line if it exceeds max width
            if (currentX + itemWidth > maxLineWidth && currentX > 0)
            {
                currentX = 0f;
                currentY += lineHeight;
            }

            // Draw logic (skipped if we are just measuring)
            if (!isMeasuring && spriteBatch != null)
            {
                if (isSanityIcon)
                {
                    // Center the icon vertically relative to the text line
                    float yOffset = (lineHeight - itemHeight) / 2f; 
                    sanityIcon.Draw(spriteBatch, startPosition + new Vector2(currentX-7.5f, currentY + yOffset - 7.5f), Color.White, 0f, Vector2.Zero, Vector2.One * 3f, SpriteEffects.None, 0f);
                    // spriteBatch.Draw(sanityIcon, startPosition + new Vector2(currentX, currentY + yOffset), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else if(IsDayIcon)
                {
                    // Center the icon vertically relative to the text line
                    float yOffset = (lineHeight - itemHeight) / 2f; 
                    dayIcon.Draw(spriteBatch, startPosition + new Vector2(currentX-7.5f, currentY + yOffset - 7.5f), Color.White, 0f, Vector2.Zero, Vector2.One * 3f, SpriteEffects.None, 0f);
                    // spriteBatch.Draw(sanityIcon, startPosition + new Vector2(currentX, currentY + yOffset), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.DrawString(font, word, startPosition + new Vector2(currentX, currentY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            currentX += itemWidth + spaceWidth;
            if (currentX > maxX) maxX = currentX;
        }

        // Return total dimensions used
        return new Vector2(maxX - spaceWidth, currentY + lineHeight);
    }

    
    // Converts a virtual-space point to integer screen pixels.
    private static Vector2 ToScreen(float x, float y)
    {
        float sx = ScreenTransform.M11, sy = ScreenTransform.M22;
        float tx = ScreenTransform.M41, ty = ScreenTransform.M42;
        return new Vector2(MathF.Round(x * sx + tx), MathF.Round(y * sy + ty));
    }

}