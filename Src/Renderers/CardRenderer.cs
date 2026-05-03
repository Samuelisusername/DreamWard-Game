using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class CardRenderer
{
    private readonly Color COLOR_M = new Color(0xb9, 0xa5, 0x83);   // card bg
    private readonly Color COLOR_D = new Color(0x29, 0x24, 0x10);   // dark text / border
    private readonly Color COLOR_L = new Color(0xc7, 0xb8, 0x9c);   // light panel
    private readonly Color COLOR_G = new Color(0x7d, 0x64, 0x48);   // mid tone

    public static readonly Dictionary<string, string> CardDescriptions = new()
    {
        { "sniper",       "Piercer of Veils\nTargets one nightmare. Long range burst damage.\n" },
        { "aoe",          "Banisher of Horrors\nTargets all nightmares. Short range burn damage.\n" },
        { "dreamcatcher", "Catcher of Dreams\nBlock all nightmares. Including flying ones.\n" },
        { "fence",        "Warden of Thresholds\nBlock walking nightmares.\n" },
        { "meadow",       "Meadow of Slumber\nGain more sanity. Stronger next to sheep.\n" },
        { "void",         "Absence of Field\nBlock walking nightmares. Cannot be removed.\n" },
        { "spikes",       "Bed of Thorns\nHurts walking nightmares.\n" },
        { "decoy",        "Lure of Phantoms\nLures nightmares.\n" },
        { "buff",         "Kindle of Resolve\nRepels nightmares.\n" },
        { "shuffle",      "Shuffler of Fates\nShuffles build cards.\n" },
        { "discount",     "Discounter of Burdens\nNext card purchased gets a discount.\n" },
        { "top3",         "Dreamer's Bounty\nFills all empty build slots with new cards.\n" },
        { "polymorph",    "Vermin of Shadows\nTurn sheep to rats. Ignored by nightmares.\n" },
    };

    // ── Tunable hand constants ────────────────────────────────────────────────
    // Change CARD_W / CARD_H freely. Everything else recomputes automatically.
    private const int   CARD_W       = 92 *2;   // card width  in virtual pixels
    private const int   CARD_H       = 128 * 2;   // card height in virtual pixels

    // How much of each card peeks above the bottom edge of the screen
    private const float HAND_PEEK_FRAC = 0.55f;   // 0 = fully hidden, 1 = fully visible

    // Arc curvature: larger → more pronounced curve; 0 → flat row
    // Expressed as a fraction of screen width so it scales with resolution.
    private const float ARC_RADIUS_FRAC = 2.5f;   // arc radius = screenW * this

    // Max total arc angle (degrees) across all cards.  Clamped so no card overlaps.
    private const float MAX_ARC_DEG  = 40f;

    // Lift (in virtual px) applied to the card being hovered
    private const float CARD_HOVER_LIFT = 30f;

    // ── Per-card data stored during Draw, reused by hover/click ──────────────
    // All three share the same coordinate system (virtual pixels after viewport scale).
    // 'center'  = the point passed as the position argument to SpriteBatch.Draw
    //             (= the world position of the card's texture origin, which is its centre)
    // 'angleDeg'= rotation in degrees (same value passed to SpriteBatch.Draw)
    // 'halfW/H' = half the drawn pixel size of the card  (CARD_W/2, CARD_H/2)
    public struct CardTransform
    {
        public Vector2 Center;    // drawn centre (SpriteBatch position == center because origin=texCenter)
        public float   AngleDeg;
        public float   HalfW;         // CARD_W / 2
        public float   HalfH;         // CARD_H / 2
        public float   BottomExtension; // extra hitbox height added below when card is lifted
    }
    private readonly List<CardTransform> _transforms = new();

    public Rectangle CardStripBounds     { get; private set; }
    public Rectangle ExpandedPanelBounds { get; private set; }
    public int TransformCount => _transforms.Count;
    public CardTransform GetTransform(int i) => _transforms[i];
    public Card     ExpandedCard  { get; private set; }
    private Vector2 _lastMousePos;
    private const int ARC_MARGIN_LEFT  = 400;
    private const int ARC_MARGIN_RIGHT = 400;
    public void SetMousePos(Vector2 pos) => _lastMousePos = pos;
    public void CollapseExpanded() => ExpandedCard = null;

    // ── Rotate a 2-D vector around the origin ────────────────────────────────
    public static Vector2 Rot(Vector2 v, float rad)
    {
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
    }

    public bool HitTest(int index, Vector2 mousePos)
    {
        if (index < 0 || index >= _transforms.Count) return false;
        var t = _transforms[index];
        if (t.HalfW == 0 || t.HalfH == 0) return false;
        float rad = MathHelper.ToRadians(t.AngleDeg);
        Vector2 local = Rot(mousePos - t.Center, -rad);
        return MathF.Abs(local.X) <= t.HalfW
            && local.Y >= -t.HalfH
            && local.Y <= t.HalfH + CARD_HOVER_LIFT;
    }

    public void HandleHover(Vector2 mousePos, CardManager cardManager)
    {
        if (ExpandedCard != null)
        {
            for (int i = 0; i < _transforms.Count; i++)
            {
                if (i >= cardManager.CardRects.Count) continue;
                var (_, c) = cardManager.CardRects[i];
                if (!ReferenceEquals(c, ExpandedCard)) continue;
                if (HitTest(i, mousePos)) return;
                break;                           
            }
        }
 
        ExpandedCard = null;
        for (int i = _transforms.Count - 1; i >= 0; i--)
        {
            if (!HitTest(i, mousePos)) continue;
            if (i >= cardManager.CardRects.Count) continue;
            var (_, c) = cardManager.CardRects[i];
            if (c != null) { ExpandedCard = c; return; }
        }
    }

    public (Card card, CardSlot slot) HandleClick(Vector2 mousePos, CardManager cardManager)
    {
        if (ExpandedCard != null)
        {
            for (int i = 0; i < _transforms.Count; i++)
            {
                if (i >= cardManager.CardRects.Count) continue;
                var (_, c) = cardManager.CardRects[i];
                if (!ReferenceEquals(c, ExpandedCard)) continue;
                if (HitTest(i, mousePos))
                    return (c, cardManager.GetSlotForCard(c));
                break;   
            }
        }
 
        for (int i = _transforms.Count - 1; i >= 0; i--)
        {
            if (!HitTest(i, mousePos)) continue;
            if (i >= cardManager.CardRects.Count) continue;
            var (_, c) = cardManager.CardRects[i];
            if (c == null) continue;
            return (c, cardManager.GetSlotForCard(c));
        }
 
        return (null, null);
    }

    // ── Compute arc transforms for 'count' cards, fitting inside screenW ──────
    // Returns (center, angleDeg) pairs. The arc is clamped so no card goes offscreen.
    private static List<(Vector2 center, float angleDeg)> ComputeArcTransforms(
        int count, int screenW, int screenH)
    {
        var result = new List<(Vector2, float)>(count);
        if (count == 0) return result;

        // ── Arc radius ─────────────────────────────────────────────
        float R = screenW * ARC_RADIUS_FRAC;

        // ── Arc center (below screen) ──────────────────────────────
        float peekY = CARD_H * HAND_PEEK_FRAC;
        float arcCX = screenW / 2f;
        float arcCY = screenH + R - peekY;

        // ── Button-safe horizontal bounds ──────────────────────────
        float usableLeft  = ARC_MARGIN_LEFT + CARD_W / 2f;
        float usableRight = screenW - ARC_MARGIN_RIGHT - CARD_W / 2f;
        float usableW     = MathF.Max(1f, usableRight - usableLeft);

        // ── Constraint 1: prevent card overlap ─────────────────────
        float minGap     = CARD_W * 0.05f; // 5% spacing
        float chordMax   = CARD_W + minGap;
        float maxStepDeg = MathHelper.ToDegrees(
            2f * MathF.Asin(MathF.Min(chordMax / (2f * R), 1f))
        );

        // ── Constraint 2: keep cards inside usable width ───────────
        float maxHalfSpanRad = MathF.Asin(MathF.Min(usableW / (2f * R), 1f));
        float maxSpanDeg     = MathHelper.ToDegrees(maxHalfSpanRad * 2f);

        // ── Desired span (based on count) ──────────────────────────
        float baseStepDeg = 10f; // tweak if needed (8–12 range)
        float spanDeg     = (count - 1) * baseStepDeg;

        // clamp span to fit screen
        spanDeg = MathF.Min(spanDeg, maxSpanDeg);

        // also respect overlap constraint
        float stepDeg = count > 1
            ? MathF.Min(spanDeg / (count - 1), maxStepDeg)
            : 0f;

        spanDeg = stepDeg * (count - 1);

        float startDeg = -spanDeg / 2f;

        // ── Generate positions ─────────────────────────────────────
        for (int i = 0; i < count; i++)
        {
            float deg = startDeg + i * stepDeg;

            // convert to circle angle (0° at top)
            float rad = MathHelper.ToRadians(deg - 90f);

            float cx = arcCX + R * MathF.Cos(rad);
            float cy = arcCY + R * MathF.Sin(rad);

            result.Add((new Vector2(cx, cy), deg));
        }

        return result;
    }

    // ── Main card strip draw ──────────────────────────────────────────────────
    public void DrawCardStrip(
        SpriteBatch spriteBatch,
        Texture2D dummy,
        Texture2D cardTexture,
        Dictionary<string, Rectangle> cardRegions,
        SpriteFont font,
        CardManager cardManager,
        int screenW,
        int screenH,
        bool isTutorial = false)
    {
        // Build slot list: 4 build → 2 stack (only non-null slots count toward arc)
        var orderedSlots = new List<(CardSlot slot, bool isSaved)>();
        for (int i = 0; i < cardManager.BuildSlots.Count; i++)
        {
            var slot = cardManager.BuildSlots[i];
            orderedSlots.Add((slot, false));
        }
        for (int i = 0; i < cardManager.StackSlots.Count; i++)
            orderedSlots.Add((cardManager.StackSlots[i], false));

        // Only cards that actually have content take up arc slots
        // (empty slots still go into CardRects so indices stay aligned, but they
        //  use the same position as a "phantom" — they're invisible and unhittable)
        int filledCount = orderedSlots.Count(s => s.slot.Card != null);

        int effectiveH = isTutorial ? screenH - CARD_H : screenH;

        // Build arc positions only for filled slots; we'll assign them in order
        var arcPositions = ComputeArcTransforms(filledCount, screenW, effectiveH);

        cardManager.CardRects.Clear();
        _transforms.Clear();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        int arcIdx = 0;   // index into arcPositions

        for (int i = 0; i < orderedSlots.Count; i++)
        {
            var (slot, isSaved) = orderedSlots[i];

            if (slot.Card == null)
            {
                // Reserve slot so CardRects index stays aligned
                cardManager.CardRects.Add((Rectangle.Empty, null));
                _transforms.Add(new CardTransform());
                continue;
            }

            var (arcCenter, angleDeg) = arcPositions[arcIdx++];

            bool isHovered  = ExpandedCard != null && ReferenceEquals(slot.Card, ExpandedCard);
            bool isActive   = ReferenceEquals(slot.Card, cardManager._activeMapCard);
            bool isLocked   = (slot.Card.Type == CardType.Build || slot.Card.Type == CardType.MapAction) && !slot.Purchased;

            float lift = isHovered ? CARD_HOVER_LIFT : 0f;

            // drawCenter is where SpriteBatch places the card centre.
            // Because we pass origin = texCenter (below), this IS the screen centre of the card.
            Vector2 drawCenter = arcCenter - new Vector2(0, lift);

            float   rad      = MathHelper.ToRadians(angleDeg);
            float   halfW    = CARD_W / 2f;
            float   halfH    = CARD_H / 2f;

            // ── Store transform (identical coordinate system as what we draw) ──
            _transforms.Add(new CardTransform
            {
                Center          = drawCenter,
                AngleDeg        = angleDeg,
                HalfW           = halfW,
                HalfH           = halfH,
                BottomExtension = isHovered ? CARD_HOVER_LIFT : 0f,
            });

            // ── AABB for CardRects (used for bounds / mapOverUI guard).
            //    This is an outer AABB of the rotated rectangle — always conservative.
            float absCos = MathF.Abs(MathF.Cos(rad));
            float absSin = MathF.Abs(MathF.Sin(rad));
            int   aabbW  = (int)MathF.Ceiling(CARD_W * absCos + CARD_H * absSin);
            int   aabbH  = (int)MathF.Ceiling(CARD_W * absSin + CARD_H * absCos);
            var   aabb   = new Rectangle(
                (int)(drawCenter.X - aabbW / 2f),
                (int)(drawCenter.Y - aabbH / 2f),
                aabbW, aabbH);

            cardManager.CardRects.Add((aabb, slot.Card));

            minX = Math.Min(minX, aabb.X);
            minY = Math.Min(minY, aabb.Y);
            maxX = Math.Max(maxX, aabb.Right);
            maxY = Math.Max(maxY, aabb.Bottom);

            // ── The texture region for this card ──────────────────────────────
            if (!cardRegions.TryGetValue(slot.Card.SpriteName, out var region))
                region = new Rectangle(0, 0, 128, 128);   // fallback

            // origin = centre of the SOURCE region → SpriteBatch rotates around that
            // → the screen centre of the drawn card == drawCenter  ✓
            Vector2 texOrigin = new(region.Width / 2f, region.Height / 2f);
            Vector2 texScale  = new(CARD_W / (float)region.Width, CARD_H / (float)region.Height);

            // ── Draw card art ─────────────────────────────────────────────────
            spriteBatch.Draw(cardTexture, drawCenter, region,
                Color.White, rad, texOrigin, texScale, SpriteEffects.None, 0f);
        }

        // ── Overall strip bounds ──────────────────────────────────────────────
        if (filledCount > 0)
            CardStripBounds = new Rectangle(minX - 8, minY - 8,
                maxX - minX + 16, maxY - minY + 16);
        else
            CardStripBounds = Rectangle.Empty;
    }

    // ── Draw a solid rotated rectangle with the dummy 1×1 texture ────────────
    // center = screen centre of the rectangle (matches drawCenter above)
    private static void DrawRotatedSolid(SpriteBatch sb, Texture2D dummy,
        Vector2 center, float halfW, float halfH, float rad, Color color)
    {
        // origin = (0.5, 0.5) in texture space = centre of the 1×1 pixel
        sb.Draw(dummy, center, null,
            color, rad, new Vector2(0.5f, 0.5f),
            new Vector2(halfW * 2f, halfH * 2f), SpriteEffects.None, 0f);
    }

    // ── Expanded panel ────────────────────────────────────────────────────────
    public void DrawExpandedPanel(
        SpriteBatch spriteBatch,
        Texture2D dummy,
        SpriteFont font,
        CardManager cardManager,
        int screenW,
        int screenH)
    {
        if (ExpandedCard == null) { ExpandedPanelBounds = Rectangle.Empty; return; }

        const int   pad    = 12;
        const float textSc = 0.6f;
        float lineH = font.LineSpacing * textSc + 2f;

        CardSlot slot     = cardManager.GetSlotForCard(ExpandedCard);
        bool     isLocked = slot != null && (ExpandedCard.Type == CardType.Build || ExpandedCard.Type == CardType.MapAction) && !slot.Purchased;
        Color infoColor   = new Color(0x40, 0x80, 0xc0);

        // ── Measure content to size the panel ────────────────────────────────
        float contentW = 0, contentH = 0;

        void Measure(string text, float sc, float extraH = 0)
        {
            contentW = Math.Max(contentW, font.MeasureString(text).X * sc);
            contentH += (extraH > 0 ? extraH : lineH);
        }

        if (CardDescriptions.TryGetValue(ExpandedCard.SpriteName, out var desc))
        {
            var descLines = desc.Split('\n');
            Measure(descLines[0].Trim(), textSc * 1.3f, lineH * 1.6f);
            for (int i = 1; i < descLines.Length; i++)
            {
                string l = descLines[i].Trim();
                if (!string.IsNullOrEmpty(l)) Measure(l, textSc);
            }
        }

        string stats = GetCardStats(ExpandedCard);
        if (!string.IsNullOrEmpty(stats)) Measure(stats, textSc);

        if (ExpandedCard.Type == CardType.StackAction)
            Measure("Double Click to apply", textSc);

        if (ExpandedCard.Type == CardType.Build || ExpandedCard.Type == CardType.MapAction)
        {
            int displayCost = cardManager.DiscountActive ? Math.Max(ExpandedCard.Cost - 2, 0) : ExpandedCard.Cost;
            string costStr  = isLocked
                ? (cardManager.DiscountActive ? $"Cost: {displayCost} sanity (discounted)" : $"Cost: {displayCost} sanity")
                : "Purchased";
            Measure(costStr, textSc);
            if (isLocked) Measure("Drag to buy & place instantly", textSc);

            if (IsBarrierCard(ExpandedCard) && !isLocked)
            {
                int maxStack    = GetMaxStack(ExpandedCard);
                string stackTxt = $"{ExpandedCard.StackCount}/{maxStack} remaining";
                contentH += 14 + 10; // bar height + gap
                Measure(stackTxt, textSc);
            }
        }

        int panelW = (int)(contentW + pad * 2);
        int panelH = (int)(contentH + pad * 2);

        // ── Position above the hovered card ──────────────────────────────────
        int panelX = 5, panelY = CardStripBounds.Y - panelH - 10;

        for (int i = 0; i < cardManager.CardRects.Count && i < _transforms.Count; i++)
        {
            var (_, card) = cardManager.CardRects[i];
            if (card != null && ReferenceEquals(card, ExpandedCard))
            {
                var t = _transforms[i];
                panelX = (int)(t.Center.X - panelW / 2f);
                panelY = (int)(t.Center.Y - t.HalfH - panelH - 10);
                break;
            }
        }

        panelX = Math.Clamp(panelX, 5, screenW - panelW - 5);
        panelY = Math.Max(5, panelY);

        Rectangle panel = new Rectangle(panelX, panelY, panelW, panelH);
        ExpandedPanelBounds = panel;

        // ── Background chrome ─────────────────────────────────────────────────
        spriteBatch.Draw(dummy, panel, COLOR_L * 0.93f);
        spriteBatch.Draw(dummy, new Rectangle(panel.X + 2, panel.Y + 2, panel.Width - 4, panel.Height - 4), COLOR_D);
        spriteBatch.Draw(dummy, new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, panel.Height - 8), COLOR_L);

        float textX = panel.X + pad;
        float textY = panel.Y + pad;

        // ── Draw content ──────────────────────────────────────────────────────
        if (CardDescriptions.TryGetValue(ExpandedCard.SpriteName, out desc))
        {
            var descLines = desc.Split('\n');
            spriteBatch.DrawString(font, descLines[0].Trim(), new Vector2(textX, textY), COLOR_D, 0f, Vector2.Zero, textSc * 1.3f, SpriteEffects.None, 0f);
            textY += lineH * 1.6f;

            spriteBatch.Draw(dummy, new Rectangle((int)textX, (int)textY, panelW - pad * 2, 1), COLOR_G * 0.6f);

            for (int i = 1; i < descLines.Length; i++)
            {
                string line = descLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                spriteBatch.DrawString(font, line, new Vector2(textX, textY), COLOR_D, 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
                textY += lineH;
            }
        }

        if (!string.IsNullOrEmpty(stats))
        {
            spriteBatch.DrawString(font, stats, new Vector2(textX, textY), COLOR_D, 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
            textY += lineH;
        }

        if (ExpandedCard.Type == CardType.StackAction)
        {
            spriteBatch.DrawString(font, "Double Click to apply", new Vector2(textX, textY), infoColor, 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
            textY += lineH;
        }

        if (ExpandedCard.Type == CardType.Build || ExpandedCard.Type == CardType.MapAction)
        {
            int displayCost = cardManager.DiscountActive ? Math.Max(ExpandedCard.Cost - 2, 0) : ExpandedCard.Cost;
            string costStr  = isLocked
                ? (cardManager.DiscountActive ? $"Cost: {displayCost} sanity (discounted)" : $"Cost: {displayCost} sanity")
                : "Purchased";
            Color costColor = isLocked ? new Color(0xc0, 0x50, 0x30) : new Color(0x20, 0x80, 0x30);

            spriteBatch.DrawString(font, costStr, new Vector2(textX, textY), costColor, 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
            textY += lineH;

            if (isLocked)
            {
                spriteBatch.DrawString(font, "Drag to buy & place instantly", new Vector2(textX, textY), infoColor, 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
                textY += lineH;
            }

            int maxStack = GetMaxStack(ExpandedCard);
            if (IsBarrierCard(ExpandedCard) && !isLocked)
            {
                int barWidth = Math.Max(panelW - pad * 2, 60);
                int barHeight = 14;
                DrawStackBar(spriteBatch, dummy, new Rectangle((int)textX, (int)textY + 6, barWidth, barHeight), ExpandedCard.StackCount, maxStack);
                textY += barHeight + 10;
                spriteBatch.DrawString(font, $"{ExpandedCard.StackCount}/{maxStack} remaining", new Vector2(textX, textY), new Color(80, 80, 80), 0f, Vector2.Zero, textSc, SpriteEffects.None, 0f);
            }
        }
    }

    public void UpdateUIButtonLayout(int screenW, int screenH, bool showInventory,
        ref Rectangle showCardsBtn, ref Rectangle towerRangeBtn, ref Rectangle nightBtn, ref Rectangle destroyBtn,
        ref Rectangle moveBtn)
    {
        int margin = 20, spacing = 12;
        int btnW = 240, btnH = 96;

        int x = screenW - btnW - margin;

        int y3 = screenH - btnH - margin;
        int y2 = y3 - btnH - spacing;
        int y1 = y2 - btnH - spacing;

        showCardsBtn  = new Rectangle(x, y1, btnW, btnH);
        towerRangeBtn = new Rectangle(x, y2, btnW, btnH);
        nightBtn   = new Rectangle(x, y3, btnW, btnH);

        // ----------------------------
        // Move / Destroy buttons (FIXED)
        // ----------------------------
        int actionBtnW = btnW;
        int actionBtnH = btnH;

        int leftMargin = margin;

        int destroyY = screenH - margin - actionBtnH;
        int moveY    = destroyY - actionBtnH - spacing;

        moveBtn    = new Rectangle(leftMargin, moveY, actionBtnW, actionBtnH);
        destroyBtn = new Rectangle(leftMargin, destroyY, actionBtnW, actionBtnH);

        // ----------------------------
    }
    private int GetMaxStack(Card card)
    {
        var template = CardManager.BarrierTemplates
            .FirstOrDefault(t => t.SpriteName == card.SpriteName);

        return template?.StackCount ?? 0;
    }
    private bool IsBarrierCard(Card card)
    {
        return CardManager.BarrierTemplates
            .Any(t => t.SpriteName == card.SpriteName);
    }

    private void DrawStackBar(SpriteBatch spriteBatch, Texture2D dummy,
    Rectangle area, int current, int max)
    {
        if (max <= 0) return;

        float ratio = MathHelper.Clamp(current / (float)max, 0f, 1f);

        spriteBatch.Draw(dummy, area, new Color(40, 40, 40));

        var fillRect = new Rectangle(
            area.X,
            area.Y,
            (int)(area.Width * ratio),
            area.Height
        );

        Color fillColor =
            ratio > 0.6f ? new Color(60, 180, 75) :
            ratio > 0.3f ? new Color(230, 180, 50) :
                        new Color(200, 60, 60);

        spriteBatch.Draw(dummy, fillRect, fillColor);

        int t = 2;
        spriteBatch.Draw(dummy, new Rectangle(area.X, area.Y, area.Width, t), Color.Black);
        spriteBatch.Draw(dummy, new Rectangle(area.X, area.Bottom - t, area.Width, t), Color.Black);
        spriteBatch.Draw(dummy, new Rectangle(area.X, area.Y, t, area.Height), Color.Black);
        spriteBatch.Draw(dummy, new Rectangle(area.Right - t, area.Y, t, area.Height), Color.Black);
    }

    private string GetCardStats(Card card)
    {
        if (card == null) return "";

        switch (card.SpriteName)
        {
            case "sniper":
                return "Range: 3 | Damage: 9 | Health: 15";

            case "aoe":
                return "Range: 2 | Damage: 9 | Health: 15";

            case "spikes":
                return "Damage: 7 | Indestructible";

            case "fence":
                return "Health: 24";

            case "dreamcatcher":
                return "Health: 24";

            case "decoy":
                return "Health: 15";

            case "buff":
                return "Health: 15";

            case "void":
                return "Indestructible";

            case "polymorph":
                return "Duration: 1 night";

            default:
                return "";
        }
    }

    private static readonly Color PLACEMENT_OK  = new Color(0x11, 0x7a, 0x15);
    private static readonly Color PLACEMENT_BAD = new Color(220, 60, 60);
    private const float PLACEMENT_HIGHLIGHT_ALPHA = 0.45f;

    /// <summary>
    /// Public hex-tile highlight (single tile). Used by external callers like the hammer/destroy mode.
    /// </summary>
    public void DrawTileHighlightPublic(SpriteBatch spriteBatch, MapManager mapManager,
        (int col, int row) tile, Color color, float alpha = PLACEMENT_HIGHLIGHT_ALPHA)
    {
        if (tile.col < 0 || tile.col >= mapManager.Columns ||
            tile.row < 0 || tile.row >= mapManager.Rows) return;
        var mask = GetHexMask(spriteBatch.GraphicsDevice);
        Vector2 maskScale = new Vector2(
            mapManager.TileWidth / HEX_MASK_W,
            (HEX_MASK_H * mapManager.Scale.Y) / HEX_MASK_H);
        Vector2 pos = mapManager.GetWorldPositionOfTile(tile);
        spriteBatch.Draw(mask, pos, null, color * alpha, 0f,
            Vector2.Zero, maskScale, SpriteEffects.None, 0f);
    }

    private void DrawTileHighlight(SpriteBatch spriteBatch, MapManager mapManager,
        (int col, int row) tile, Color color)
    {
        if (tile.col < 0 || tile.col >= mapManager.Columns ||
            tile.row < 0 || tile.row >= mapManager.Rows) return;
        var mask = GetHexMask(spriteBatch.GraphicsDevice);
        Vector2 maskScale = new Vector2(
            mapManager.TileWidth / HEX_MASK_W,
            (HEX_MASK_H * mapManager.Scale.Y) / HEX_MASK_H);
        Vector2 pos = mapManager.GetWorldPositionOfTile(tile);
        spriteBatch.Draw(mask, pos, null, color * PLACEMENT_HIGHLIGHT_ALPHA, 0f,
            Vector2.Zero, maskScale, SpriteEffects.None, 0f);
    }

    private void DrawPlacementHighlight(SpriteBatch spriteBatch, MapManager mapManager,
        Structure structure, (int col, int row) tile, Color color)
    {
        if (structure is Tower tower)
            DrawTowerRangePreview(spriteBatch, mapManager, tower, color);
        else
            DrawTileHighlight(spriteBatch, mapManager, tile, color);
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

            var preview = cardManager._previewStructure;
            if (preview != null)
            {
                var existing = mapManager.GetStructure(hoverTile);
                bool canHeal = existing != null && existing.GetType() == preview.GetType() && existing.CurrentHealth < existing.MaxHealth;
                Color hl = (existing == null || canHeal) ? PLACEMENT_OK : PLACEMENT_BAD;
                DrawPlacementHighlight(spriteBatch, mapManager, preview, hoverTile, hl);
                DrawTransparentStructure(spriteBatch, preview, hl);
            }
            return;
        }

        Structure target = mapManager.GetStructure(hoverTile) as Structure;
        switch (cardManager._activeMapCard.Effect)
        {
            case CardEffect.Move:
                if (target != null) DrawTransparentStructure(spriteBatch, target);
                if (cardManager._selectedTile != (-1, -1))
                {
                    var moving = cardManager._previewStructure
                        ?? mapManager.GetStructure(cardManager._selectedTile) as Structure;
                    if (moving != null)
                    {
                        bool valid = target == null;
                        Color hl = valid ? PLACEMENT_OK : PLACEMENT_BAD;
                        var old = moving.TilePosition;
                        moving.TilePosition = hoverTile;
                        DrawPlacementHighlight(spriteBatch, mapManager, moving, hoverTile, hl);
                        if (valid)
                            DrawTransparentStructure(spriteBatch, moving, hl);
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
    private const float HEATMAP_DPS_LOW = 10f;   // below this DPS -> yellow
    private const float HEATMAP_DPS_MID = 30f;   // below this -> orange
    private const float HEATMAP_DPS_HIGH = 50f;  // below this -> muted red, above -> deep red

    private Texture2D GetHexMask(GraphicsDevice gd)
    {
        if (_hexMask != null) return _hexMask;
        var data = new Color[HEX_MASK_W * HEX_MASK_H];
        // Pointy-side (flat top/bottom) hexagon spanning the full 64x32.
        // Top/bottom edges occupy the middle 32px width; sides angle out to full 64px width at vertical center.
        for (int y = 0; y < 17; y++)
        {
            int pxlCount = 33 + 2*y;
            int xStart = 15 - y;
            int xEnd = xStart + pxlCount;
            for (int x = 0; x < HEX_MASK_W; x++)
                data[y * HEX_MASK_W + x] = (x >= xStart && x < xEnd) ? Color.White : Color.Transparent;
        }
        for (int y = 17; y < 32; y++)
        {
            int pxlCount = 64-((y-16)*2) - 1;
            int xStart = y-16;
            int xEnd = xStart + pxlCount;
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
        var dpsPerTile = new Dictionary<(int, int), float>();

        for (int i = 0; i < mapManager.Columns; i++)
            for (int j = 0; j < mapManager.Rows; j++)
                if (mapManager.GetStructure((i, j)) is Tower t)
                {
                    foreach (var tile in t.GetTilesInRange())
                    {
                        if (tile.Item1 < 0 || tile.Item1 >= mapManager.Columns ||
                            tile.Item2 < 0 || tile.Item2 >= mapManager.Rows) continue;
                        dpsPerTile.TryGetValue(tile, out float prev);
                        dpsPerTile[tile] = prev + t.DamagePerSecond;
                    }
                }

        if (dpsPerTile.Count == 0) return;

        var mask = GetHexMask(spriteBatch.GraphicsDevice);
        Vector2 maskScale = new Vector2(
            mapManager.TileWidth / HEX_MASK_W,
            (HEX_MASK_H * mapManager.Scale.Y) / HEX_MASK_H);

        foreach (var kv in dpsPerTile)
        {
            float dps = kv.Value;
            int level;
            if (dps < HEATMAP_DPS_LOW) level = 0;
            else if (dps < HEATMAP_DPS_MID) level = 1;
            else if (dps < HEATMAP_DPS_HIGH) level = 2;
            else level = 3;

            Color color = HeatmapColor(level) * HEATMAP_ALPHA;
            Vector2 pos = mapManager.GetWorldPositionOfTile(kv.Key);
            spriteBatch.Draw(mask, pos, null, color, 0f, Vector2.Zero,
                maskScale, SpriteEffects.None, 0f);
        }
    }

    public void DrawTowerRangePreview(SpriteBatch spriteBatch, MapManager mapManager, Tower tower, Color? overrideColor = null)
    {
        if (tower == null) return;
        Color baseColor = overrideColor ?? new Color(247, 211, 79);
        Color rangeColor = baseColor * 0.45f;

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
                maskScale, SpriteEffects.None, 0f);
        }
    }

    private void DrawTransparentStructure(SpriteBatch spriteBatch, Structure structure, Color? tint = null)
    {
        if (structure == null) return;
        var prev = structure.PreviewTint;
        structure.PreviewTint = tint;
        structure.DrawBase(spriteBatch, alpha: 0.5f);
        structure.Draw(spriteBatch, alpha: 0.5f);
        structure.PreviewTint = prev;
    }

    private void DrawBorder(SpriteBatch spriteBatch, Texture2D dummy, Rectangle rect, Color color, int t)
    {
        spriteBatch.Draw(dummy, new Rectangle(rect.X, rect.Y, rect.Width, t), color);
        spriteBatch.Draw(dummy, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), color);
        spriteBatch.Draw(dummy, new Rectangle(rect.X, rect.Y, t, rect.Height), color);
        spriteBatch.Draw(dummy, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), color);
    }

}