using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class CardRenderer
{
    private readonly Color COLOR_M = new Color(0xb9, 0xa5, 0x83);  
    private readonly Color COLOR_D = new Color(0xa1, 0x88, 0x5b);    
    private readonly Color COLOR_L = new Color(0xc7, 0xb8, 0x9c);

    // ------------------------
    // CARD SLOTS
    // ------------------------
    public void DrawCardSlots(
        SpriteBatch spriteBatch,
        Texture2D dummy,
        Texture2D cardTexture,
        Dictionary<string, Rectangle> cardRegions,
        SpriteFont font,
        CardManager cardManager,
        int startY,
        int screenW,
        int screenH)
    {
        var slots = cardManager.MapSlots.Concat(cardManager.BuildSlots).ToList();

        int cardW = 128*3/2;
        int cardH = 128*3/2;
        int spacing = 5;
        startY = screenH - cardH - 10;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;
        int startX = screenW - totalWidth - 10;

        Rectangle borderRect = new(
            startX - 4,
            startY - 4,
            totalWidth + 8,
            cardH + 8
        );

        spriteBatch.Draw(dummy, borderRect, COLOR_L);
        cardManager.CardRects.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            Rectangle rect = new(
                startX + i * (cardW + spacing),
                startY,
                cardW,
                cardH
            );

            cardManager.CardRects.Add((rect, slot.Card));

            Color bgColor = (slot.Card != null && ReferenceEquals(slot.Card, cardManager._activeMapCard))
                ? COLOR_D
                : COLOR_M;

            spriteBatch.Draw(dummy, rect, bgColor);

            if (slot.Card != null)
            {
                spriteBatch.Draw(cardTexture, rect,
                    cardRegions[slot.Card.SpriteName], Color.White);
            }
        }
    }

    public void DrawPreview(
        SpriteBatch spriteBatch,
        CardManager cardManager,
        MapManager mapManager,
        (int col, int row) hoverTile)
    {
        if (cardManager._activeMapCard == null || hoverTile.col < 0) return;

        // BUILD PREVIEW
        if (cardManager._activeMapCard.Type == CardType.Build)
        {
            if (cardManager._previewStructure == null ||
                cardManager._previewStructure.TilePosition != hoverTile)
            {
                cardManager._previewStructure =
                    cardManager.GetStructureFromCard(cardManager._activeMapCard, hoverTile);
            }

            if (cardManager._previewStructure != null)
            {
                DrawTransparentStructure(spriteBatch, cardManager._previewStructure);

                if (cardManager._previewStructure is Tower tower)
                    DrawTowerRangePreview(spriteBatch, mapManager, tower);
            }

            return;
        }

        Structure target = mapManager.GetStructure(hoverTile) as Structure;

        switch (cardManager._activeMapCard.Effect)
        {
            case CardEffect.Destroy:
                if (target != null)
                    DrawTransparentStructure(spriteBatch, target);
                break;

            case CardEffect.Move:
                if (target != null)
                    DrawTransparentStructure(spriteBatch, target);

                if (cardManager._selectedTile != (-1, -1))
                {
                    Structure movingEntity =
                        cardManager._previewStructure ??
                        mapManager.GetStructure(cardManager._selectedTile) as Structure;

                    if (movingEntity != null && target == null)
                    {
                        var oldPos = movingEntity.TilePosition;
                        movingEntity.TilePosition = hoverTile;

                        DrawTransparentStructure(spriteBatch, movingEntity);

                        if (movingEntity is Tower tower)
                            DrawTowerRangePreview(spriteBatch, mapManager, tower);

                        movingEntity.TilePosition = oldPos;
                    }
                }
                break;

            case CardEffect.Polymorph:
                if (target != null)
                    DrawTransparentStructure(spriteBatch, target);
                break;
        }
    }

    private void DrawTransparentStructure(SpriteBatch spriteBatch, Structure structure)
    {
        if (structure == null) return;
        structure.DrawTransparent(spriteBatch, 0.5f);
    }

    public void DrawTowerRangePreview(SpriteBatch spriteBatch, MapManager mapManager, Tower tower)
    {
        if (tower == null) return;

        float alpha = tower is AreaTower ? 0.25f : 0.4f;
        Color rangeColor = (tower is AreaTower ? Color.Blue : Color.Red) * alpha;

        foreach (var tile in tower.GetTilesInRange())
        {
            if (tile.Item1 < 0 || tile.Item1 >= mapManager.Columns ||
                tile.Item2 < 0 || tile.Item2 >= mapManager.Rows)
                continue;

            var tileTexture = mapManager.GetTile(tile);
            Vector2 pos = mapManager.GetWorldPositionOfTile(tile);

            tileTexture.Draw(spriteBatch, pos, rangeColor, 0f,
                Vector2.Zero, mapManager.Scale, SpriteEffects.None, 0.8f);
        }
    }

    public void DrawAllTowerRanges(SpriteBatch spriteBatch, MapManager mapManager)
    {
        for (int i = 0; i < mapManager.Columns; i++)
        {
            for (int j = 0; j < mapManager.Rows; j++)
            {
                var structure = mapManager.GetStructure((i, j));

                if (structure is Tower tower)
                {
                    DrawTowerRangePreview(spriteBatch, mapManager, tower);
                }
            }
        }
    }
    public void UpdateUIButtonLayout(
        int screenW,
        int screenH,
        bool showInventory,
        SpriteFont font,
        ref int cardStartY,
        ref Rectangle inventoryBtn,
        ref Rectangle towerRangeBtn)
    {
        int spacing = 10;
        int marginRight = 20;

        int cardH = 128 * 3 / 2;

        cardStartY = showInventory ? (screenH - cardH - 10) : screenH;

        int currentY = cardStartY - spacing;

        string text = "Tower Range";
        Vector2 size = font.MeasureString(text);

        int btnW = (int)size.X + 20;
        int btnH = (int)size.Y + 10;

        // Inventory button (just above cards)
        currentY -= btnH;
        inventoryBtn = new Rectangle(screenW - btnW - marginRight, currentY, btnW, btnH);

        // spacing
        currentY -= spacing;

        // Tower range button (above inventory)
        currentY -= btnH;
        towerRangeBtn = new Rectangle(screenW - btnW - marginRight, currentY, btnW, btnH);
    }
}