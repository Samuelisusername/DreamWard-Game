using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class CardRenderer
{
    private readonly Color SLOT_COLOR = new Color(0xbc, 0xb6, 0x9d);
    private readonly Color BTN_TEXT_COLOR = new Color(0x74, 0x70, 0x5f);
    private readonly Color SLOT_BORDER_COLOR = new Color(0xd8, 0xd4, 0xc1);

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

        int cardW = 128*2;
        int cardH = 128*2;
        int spacing = 5;

        int totalWidth = slots.Count * cardW + (slots.Count - 1) * spacing;
        int startX = screenW - totalWidth - 10;

        Rectangle borderRect = new(
            startX - 4,
            startY - 4,
            totalWidth + 8,
            cardH + 8
        );

        spriteBatch.Draw(dummy, borderRect, SLOT_BORDER_COLOR);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            Rectangle rect = new(
                startX + i * (cardW + spacing),
                startY,
                cardW,
                cardH
            );

            Color bgColor = (slot.Card != null && slot.Card == cardManager._activeMapCard)
                ? BTN_TEXT_COLOR
                : SLOT_COLOR;

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
    public void UpdateUIButtonLayout(int screenW, int screenH, bool showInventory, SpriteFont font, ref int cardStartY, ref Rectangle inventoryBtn, ref Rectangle towerRangeBtn)
    {
        int margin = 20, spacing = 10;

        int currentY = screenH - margin;

        int cardH = 128*2;
        cardStartY = showInventory ? currentY - cardH : currentY;
        currentY = cardStartY - (showInventory ? spacing : 0);

        string trText = "Tower Range";
        Vector2 trSize = font.MeasureString(trText);

        int invW = (int)trSize.X + 20;
        int invH = (int)trSize.Y + 10;

        currentY -= invH;
        inventoryBtn = new Rectangle(screenW - invW - margin, currentY, invW, invH);
        currentY -= spacing;

        int trW = (int)trSize.X + 20;
        int trH = (int)trSize.Y + 10;

        currentY -= trH;
        towerRangeBtn = new Rectangle(screenW - trW - margin, currentY, trW, trH);
    }
}