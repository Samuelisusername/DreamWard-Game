using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class CardRenderer
{
    private readonly Color COLOR_M      = new Color(0xb9, 0xa5, 0x83);   // card bg
    private readonly Color COLOR_D      = new Color(0x29, 0x24, 0x10);   // dark text / border
    private readonly Color COLOR_L      = new Color(0xc7, 0xb8, 0x9c);   // light panel

    public static readonly Dictionary<string, string> CardDescriptions = new()
    {
        { "sniper",       "Sniper Tower\nTargets 1 enemy at a time\nShort range but high \ndamage" },
        { "aoe",          "Area of Effect Tower\nTargets all the enemies in \nits range. Long range \nbut less damage" },
        { "dreamcatcher", "Dreamcatcher\nActs as a wall to blocks \nthe enemies, including \nflying ones" },
        { "fence",        "Fence\nBlock all ground enemies" },
        { "meadow",       "Meadow\nCan move Sheeps here\nHelps increase sanity" },
        { "void",         "Void\nBlock all ground enemies.\nOnce placed cannot\nbe removed." },
        { "spikes",       "Spikes\nInflicts damage on ground \nenemies." },
        { "decoy",        "Decoy\nActs like a tile with sheep.\nLures enemies towards it." },
        { "buff",         "Buff\nRepels enemies" },
        { "shuffle",      "Shuffle\nShuffles the build cards." },
        { "discount",     "Discount\nNext card purchased gets\na discount of 1 sanity" },
        { "top3",         "Save Card\nSave a card for next \nround" },
        { "discard",      "Discard\nDiscard the card.\nGet a new card." },
        { "polymorph",    "Polymorph\nTransforms the sheep \ninto rats. Monsters don't \nattack the sheep" },
        { "move",         "Move\nRelocate a Build Structure" },
        { "destroy",      "Destroy\nDestroy a Build Structure" },
    };

    private const int CARD_W       = 128*3/2;
    private const int CARD_H       = 128*3/2;
    private const int CARD_SPACING = 10;
    private const int MARGIN_BOT   = 10;
    private const int MARGIN_LEFT  = 10;
    public Rectangle CardStripBounds { get; private set; }
    public Rectangle ExpandedPanelBounds { get; private set; }

    public  Card      ExpandedCard      { get; private set; }
    private Vector2   _lastMousePos;

    public void SetMousePos(Vector2 pos) => _lastMousePos = pos;
    public void CollapseExpanded(){ ExpandedCard   = null;}

    public void HandleHover(Vector2 mousePos, CardManager cardManager)
    {
        ExpandedCard = null;
        foreach (var (rect, card) in cardManager.CardRects)
        {
            if (card != null && rect.Contains(mousePos))
            {
                ExpandedCard = card;
                return;
            }
        }
    }

    public void DrawCardStrip(
        SpriteBatch spriteBatch,
        Texture2D dummy,
        Texture2D cardTexture,
        Dictionary<string, Rectangle> cardRegions,
        SpriteFont font,
        CardManager cardManager,
        int screenW,
        int screenH, bool isTutorial = false)
    {
        var orderedSlots = new List<(CardSlot slot, bool isSaved)>();
        orderedSlots.Add((cardManager.MapSlots[0],   false));
        orderedSlots.Add((cardManager.StackSlots[0], false));
        for (int i = 0; i < cardManager.BuildSlots.Count; i++)
        {
            bool isSaved = cardManager.BuildSlots[i].Purchased
                        && cardManager.SavedSlotIndex == -1   
                        && IsSavedSlot(cardManager, i);
            orderedSlots.Add((cardManager.BuildSlots[i], isSaved));
        }

        int totalW = orderedSlots.Count * CARD_W + (orderedSlots.Count - 1) * CARD_SPACING;
        int startX = MARGIN_LEFT;
        int startY = screenH - CARD_H - MARGIN_BOT;

        if (isTutorial)
        {
            // move everything up by strip height 
            startY -= CARD_H;
        }

        CardStripBounds =  new Rectangle(startX - 6, startY - 6, totalW + 12, CARD_H + 12);

        spriteBatch.Draw(dummy,CardStripBounds, COLOR_L);
        cardManager.CardRects.Clear();

        for (int i = 0; i < orderedSlots.Count; i++)
        {
            var (slot, isSaved) = orderedSlots[i];
            Rectangle rect = new(startX + i * (CARD_W + CARD_SPACING), startY, CARD_W, CARD_H);
            cardManager.CardRects.Add((rect, slot.Card));

            bool isActive = slot.Card != null && ReferenceEquals(slot.Card, cardManager._activeMapCard);
            bool isLocked = slot.Card != null && slot.Card.Type == CardType.Build && !slot.Purchased;
            
            spriteBatch.Draw(dummy, rect, COLOR_M);

            if (slot.Card != null && cardRegions.TryGetValue(slot.Card.SpriteName, out var region))
            {
                Color tint = isLocked ? new Color(120, 120, 120) : Color.White;
                spriteBatch.Draw(cardTexture, rect, region, tint);
            }
            if (isLocked)
            {
                spriteBatch.Draw(dummy, rect, COLOR_D * 0.15f);
            }
            if (cardManager.PendingStackCard != null &&
                ReferenceEquals(slot.Card, cardManager.PendingStackCard))
            {
                spriteBatch.Draw(dummy, rect, COLOR_D * 0.45f);
            }
            if (isActive)
            {
                spriteBatch.Draw(dummy, rect, COLOR_D * 0.45f);
            }
            if (slot.IsSaved)
            {
                spriteBatch.Draw(dummy, rect, Color.Cyan * 0.2f);
            }
        }
    }

    public void DrawExpandedPanel(
        SpriteBatch spriteBatch,
        Texture2D dummy,
        Texture2D cardTexture,
        Dictionary<string, Rectangle> cardRegions,
        SpriteFont font,
        CardManager cardManager,
        int screenW,
        int screenH)
    {
        if (ExpandedCard == null)
        {
            ExpandedPanelBounds = Rectangle.Empty;
            return;
        }

        // Find the card's strip rect
        Rectangle cardRect = Rectangle.Empty;
        foreach (var (r, c) in cardManager.CardRects)
            if (c != null && ReferenceEquals(c, ExpandedCard)) { cardRect = r; break; }
        if (cardRect == Rectangle.Empty) { ExpandedCard = null; return; }

        CardSlot slot = cardManager.GetSlotForCard(ExpandedCard);
        bool isLocked = slot != null && ExpandedCard.Type == CardType.Build && !slot.Purchased;

        if (slot.Card != null && ReferenceEquals(slot.Card,ExpandedCard))
        {
            DrawBorder(spriteBatch, dummy, cardRect, COLOR_D, 2);
        }

        int bigCard    = 128*2;
        int panelW = bigCard + 80;
        int lineH      = 22;
        float textSc   = 0.55f;

        int descLines = CardDescriptions.TryGetValue(ExpandedCard.SpriteName, out var desc)
            ? desc.Split('\n').Length : 0;
        int panelH = bigCard + 14 + descLines * lineH + (isLocked || ExpandedCard.Type == CardType.Build ? lineH + 4 : 0) + 16 + 44;

        int panelX = Math.Clamp(cardRect.X + cardRect.Width / 2 - panelW / 2, 4, screenW - panelW - 4);
        int panelY = cardRect.Y - panelH - 10;
        if (panelY < 4) panelY = 4;

        Rectangle panelRect = new(panelX, panelY, panelW, panelH);
        ExpandedPanelBounds = panelRect;

        spriteBatch.Draw(dummy, panelRect, COLOR_L);
        spriteBatch.Draw(dummy, new Rectangle(panelRect.X + 2, panelRect.Y + 2, panelRect.Width - 4, panelRect.Height - 4), COLOR_D);
        spriteBatch.Draw(dummy, new Rectangle(panelRect.X + 4, panelRect.Y + 4, panelRect.Width - 8, panelRect.Height - 8), COLOR_L);

        Rectangle bigCardRect = new(panelX + (panelW - bigCard) / 2, panelY + 8, bigCard, bigCard);
        if (cardRegions.TryGetValue(ExpandedCard.SpriteName, out var bigRegion))
        {
            spriteBatch.Draw(cardTexture, bigCardRect, bigRegion, Color.White);
        }

        float ty = panelY + bigCard + 14;
        float lx = panelX + 8;
        float lineSpacing = font.LineSpacing * textSc;

        foreach (var line in desc.Split('\n'))
        {
            spriteBatch.DrawString(font, line, new Vector2(lx, ty), COLOR_D, 0, Vector2.Zero, textSc, SpriteEffects.None, 0);
            ty += lineSpacing;
        }

        Color infoColor = new Color(0x40, 0x80, 0xc0); // blue 

        if (ExpandedCard.Type == CardType.StackAction)
        {
            string info = "";

            if (ExpandedCard.Effect == CardEffect.Shuffle ||
                ExpandedCard.Effect == CardEffect.Discount)
            {
                info = "Double Click to apply";
            }
            else if (ExpandedCard.Effect == CardEffect.SaveCard ||
                    ExpandedCard.Effect == CardEffect.Discard)
            {
                info = "Double Click -> Select card";
            }

            spriteBatch.DrawString(font, info, new Vector2(lx, ty), infoColor, 0, Vector2.Zero, textSc, SpriteEffects.None, 0);
            ty += lineH * 2;
        }
        if (ExpandedCard.Type == CardType.MapAction)
        {
            string info = "Click card -> click on map";

            spriteBatch.DrawString(font, info, new Vector2(lx, ty), infoColor, 0, Vector2.Zero, textSc, SpriteEffects.None, 0);
            ty += lineH;
        }

        // Cost / status line
        if (ExpandedCard.Type == CardType.Build)
        {
            int displayCost = cardManager.DiscountActive
                ? Math.Max(ExpandedCard.Cost - 1, 0) : ExpandedCard.Cost;

            string costStr = isLocked
                ? (cardManager.DiscountActive ? $"Cost: {displayCost} sanity (discounted)" : $"Cost: {displayCost} sanity")
                : "Purchased";
            Color costColor = isLocked ? new Color(0xc0, 0x50, 0x30) : new Color(0x20, 0x80, 0x30);
            spriteBatch.DrawString(font, costStr, new Vector2(lx, ty), costColor, 0, Vector2.Zero, textSc, SpriteEffects.None, 0);
            ty += lineH + 4;
            if (isLocked)
                spriteBatch.DrawString(font, "Double click to buy", new Vector2(lx, ty), new Color(0x40, 0x90, 0xff), 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
            ty += lineH + 6;
        }

        ty += 6;
    }

    public void UpdateUIButtonLayout(int screenW, int screenH, bool showInventory, SpriteFont font, ref int cardStartY, ref Rectangle showCardsBtn, ref Rectangle towerRangeBtn, ref Rectangle nightBtn)
    {
        int marginRight = 20, spacing = 12;

        // measure all texts first
        Vector2 t1 = font.MeasureString("Show Cards");
        Vector2 t2 = font.MeasureString("Tower Range");
        Vector2 t3 = font.MeasureString("Start Night (Space)");

        // dynamic widths (shared column = max width)
        int btnW = (int)Math.Max(t1.X, Math.Max(t2.X, t3.X)) + 5;

        // dynamic heights (optional, usually same font so can unify)
        int btnH1 = (int)t1.Y + 10;
        int btnH2 = (int)t2.Y + 10;
        int btnH3 = (int)t3.Y + 10;

        // base Y from bottom
        int y3 = screenH - btnH3 - marginRight;
        int y2 = y3 - btnH2 - spacing;
        int y1 = y2 - btnH1 - spacing;

        // right alignment X (shared for all)
        int x = screenW - btnW - marginRight;

        // assign buttons
        showCardsBtn   = new Rectangle(x, y1, btnW, btnH1);
        towerRangeBtn  = new Rectangle(x, y2, btnW, btnH2);
        nightBtn       = new Rectangle(x, y3, btnW, btnH3);

    }

    public void DrawPreview(
        SpriteBatch spriteBatch, CardManager cardManager,
        MapManager mapManager, (int col, int row) hoverTile)
    {
        if (cardManager._activeMapCard == null || hoverTile.col < 0) return;

        if (cardManager._activeMapCard.Type == CardType.Build)
        {
            if (cardManager._previewStructure == null ||
                cardManager._previewStructure.TilePosition != hoverTile)
                cardManager._previewStructure =
                    cardManager.GetStructureFromCard(cardManager._activeMapCard, hoverTile);

            if (cardManager._previewStructure != null)
            {
                DrawTransparentStructure(spriteBatch, cardManager._previewStructure);
                if (cardManager._previewStructure is Tower t)
                    DrawTowerRangePreview(spriteBatch, mapManager, t);
            }
            return;
        }

        Structure target = mapManager.GetStructure(hoverTile) as Structure;
        switch (cardManager._activeMapCard.Effect)
        {
            case CardEffect.Destroy:
            case CardEffect.Discard:
                if (target != null) DrawTransparentStructure(spriteBatch, target);
                break;

            case CardEffect.Move:
                if (target != null) DrawTransparentStructure(spriteBatch, target);
                if (cardManager._selectedTile != (-1, -1))
                {
                    var moving = cardManager._previewStructure
                        ?? mapManager.GetStructure(cardManager._selectedTile) as Structure;
                    if (moving != null && target == null)
                    {
                        var old = moving.TilePosition;
                        moving.TilePosition = hoverTile;
                        DrawTransparentStructure(spriteBatch, moving);
                        if (moving is Tower tower) DrawTowerRangePreview(spriteBatch, mapManager, tower);
                        moving.TilePosition = old;
                    }
                }
                break;

            case CardEffect.Polymorph:
                if (target != null) DrawTransparentStructure(spriteBatch, target);
                break;
        }
    }

    private Texture2D _hexMask;
    private const int HEX_MASK_W = 64;
    private const int HEX_MASK_H = 32;
    private const float HEATMAP_ALPHA = 0.55f;

    private Texture2D GetHexMask(GraphicsDevice gd)
    {
        if (_hexMask != null) return _hexMask;
        var data = new Color[HEX_MASK_W * HEX_MASK_H];
        // Pointy-side (flat top/bottom) hexagon spanning the full 64x32.
        // Top/bottom edges occupy the middle 32px width; sides angle out to full 64px width at vertical center.
        for (int y = 0; y < HEX_MASK_H; y++)
        {
            float t = y < HEX_MASK_H / 2 ? y / (float)(HEX_MASK_H / 2)
                                         : (HEX_MASK_H - 1 - y) / (float)(HEX_MASK_H / 2);
            int halfWidth = (int)(16 + 16 * t);
            int xStart = HEX_MASK_W / 2 - halfWidth;
            int xEnd = HEX_MASK_W / 2 + halfWidth;
            for (int x = 0; x < HEX_MASK_W; x++)
                data[y * HEX_MASK_W + x] = (x >= xStart && x < xEnd) ? Color.White : Color.Transparent;
        }
        _hexMask = new Texture2D(gd, HEX_MASK_W, HEX_MASK_H);
        _hexMask.SetData(data);
        return _hexMask;
    }

    private static Color HeatmapColor(int level)
    {
        // 0: yellow (low), 1: orange (some), 2: muted red (more), 3: deep red (most)
        return level switch
        {
            0 => new Color(247, 211, 79),
            1 => new Color(232, 142, 60),
            2 => new Color(184, 64, 50),
            _ => new Color(120, 28, 28),
        };
    }

    public void DrawAllTowerRanges(SpriteBatch spriteBatch, MapManager mapManager)
    {
        var damagePerTile = new Dictionary<(int, int), float>();
        var coveredTiles = new HashSet<(int, int)>();

        for (int i = 0; i < mapManager.Columns; i++)
            for (int j = 0; j < mapManager.Rows; j++)
                if (mapManager.GetStructure((i, j)) is Tower t)
                {
                    foreach (var tile in t.GetTilesInRange())
                    {
                        if (tile.Item1 < 0 || tile.Item1 >= mapManager.Columns ||
                            tile.Item2 < 0 || tile.Item2 >= mapManager.Rows) continue;
                        coveredTiles.Add(tile);
                    }
                    foreach (var kv in t.DamageByTile)
                    {
                        damagePerTile.TryGetValue(kv.Key, out float prev);
                        damagePerTile[kv.Key] = prev + kv.Value;
                    }
                }

        float maxDamage = 0f;
        foreach (var v in damagePerTile.Values)
            if (v > maxDamage) maxDamage = v;

        var mask = GetHexMask(spriteBatch.GraphicsDevice);
        Vector2 maskScale = new Vector2(
            mapManager.TileWidth / HEX_MASK_W,
            (HEX_MASK_H * mapManager.Scale.Y) / HEX_MASK_H);

        foreach (var tile in coveredTiles)
        {
            damagePerTile.TryGetValue(tile, out float dmg);
            int level;
            if (maxDamage <= 0f || dmg <= 0f) level = 0;
            else
            {
                float ratio = dmg / maxDamage;
                if (ratio < 0.34f) level = 1;
                else if (ratio < 0.67f) level = 2;
                else level = 3;
            }

            Color color = HeatmapColor(level) * HEATMAP_ALPHA;
            Vector2 pos = mapManager.GetWorldPositionOfTile(tile);
            spriteBatch.Draw(mask, pos, null, color, 0f, Vector2.Zero,
                maskScale, SpriteEffects.None, 0.8f);
        }
    }

    public void DrawTowerRangePreview(SpriteBatch spriteBatch, MapManager mapManager, Tower tower)
    {
        if (tower == null) return;
        Color rangeColor = new Color(247, 211, 79) * 0.45f;

        var mask = GetHexMask(spriteBatch.GraphicsDevice);
        Vector2 maskScale = new Vector2(
            mapManager.TileWidth / HEX_MASK_W,
            (HEX_MASK_H * mapManager.Scale.Y) / HEX_MASK_H);

        foreach (var tile in tower.GetTilesInRange())
        {
            if (tile.Item1 < 0 || tile.Item1 >= mapManager.Columns ||
                tile.Item2 < 0 || tile.Item2 >= mapManager.Rows) continue;
            Vector2 pos = mapManager.GetWorldPositionOfTile(tile);
            spriteBatch.Draw(mask, pos, null, rangeColor, 0f, Vector2.Zero,
                maskScale, SpriteEffects.None, 0.8f);
        }
    }

    private void DrawTransparentStructure(SpriteBatch spriteBatch, Structure structure)
    {
        if (structure == null) return;
        structure.DrawBase(spriteBatch, alpha: 0.5f);
        structure.Draw(spriteBatch, alpha: 0.5f);
    }

    private void DrawBorder(SpriteBatch spriteBatch, Texture2D dummy, Rectangle rect, Color color, int t)
    {
        spriteBatch.Draw(dummy, new Rectangle(rect.X, rect.Y, rect.Width, t), color);
        spriteBatch.Draw(dummy, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), color);
        spriteBatch.Draw(dummy, new Rectangle(rect.X, rect.Y, t, rect.Height), color);
        spriteBatch.Draw(dummy, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), color);
    }

    private bool IsSavedSlot(CardManager cardManager, int slotIndex)
    {
        return _restoredSavedSlotIndex == slotIndex;
    }
    private int _restoredSavedSlotIndex = -1;
    public void SetRestoredSavedSlot(int index) => _restoredSavedSlotIndex = index;
}