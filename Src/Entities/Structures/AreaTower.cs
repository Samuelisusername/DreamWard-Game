

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Gamelab;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Tower with depth-2 hex range (own tile + immediate neighbors + their neighbors).
/// </summary>
public class AreaTower : Tower
{
    private static readonly Color beamColor = new Color(252, 187, 45);
    private const int beamSegments = 24;
    private const float beamWaveAmplitude = 8f;
    private const float beamWaveFrequency = 0.9f;
    private const float beamWaveSpeed = 9f;
    private const float beamThickness = 2f;

    private const int beamParticlesPerFrame = 1;
    private const float beamParticleSpread = 18f;
    private const float beamParticleEmitChance = 0.5f;
    private static readonly TimeSpan beamParticleLifespan = TimeSpan.FromMilliseconds(180);

    private float beamPhase;
    private readonly List<MonsterGroup> activeBeamTargets = new List<MonsterGroup>();

    public AreaTower((int,int) pos) : base("towerAOE_idle", pos)
    {
    }

    protected override (string,string)[] infos { get => [("Health:", $"{CurrentHealth}"), ("DPS:", $"{damagePerSecond}"), ("Total Damage Dealt:", $"{(int)totalDamage}")];}

    public override void Attack(GameTime gameTime)
    {
        base.Attack(gameTime);

        activeBeamTargets.Clear();
        var tilesInRange = MapUtils.GetSurroundingCircle(TilePosition, 0, rangeDepth + 1);
        foreach (var tile in tilesInRange)
        {
            var container = mapManager.GetMonsterContainer(tile);
            if (container == null || container.IsEmpty) continue;
            foreach (var group in container.MonsterGroups)
            {
                if (!group.IsEmpty)
                {
                    activeBeamTargets.Add(group);
                }
            }
        }
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        beamPhase += beamWaveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
    }

    public override void Draw(SpriteBatch spriteBatch, Vector2 offset = default, float alpha = 1f)
    {
        base.Draw(spriteBatch, offset, alpha);

        if (_spriteTop == null || gamePhaseManager.IsDay) return;
        if (activeBeamTargets.Count == 0) return;

        Vector2 worldPos = mapManager.GetWorldPositionOfTile(TilePosition);
        Vector2 startPos = worldPos + (_localOffset * _spriteTop.Scale + offset) + (new Vector2(64, 55) * _spriteTop.Scale);

        foreach (var group in activeBeamTargets)
        {
            if (group.IsEmpty) continue;
            Vector2 endPos = group.GetInterpolatedWorldPosition() + (new Vector2(45, 5) * mapManager.Scale);
            DrawWavyBeam(spriteBatch, startPos, endPos);
        }
    }

    private void DrawWavyBeam(SpriteBatch spriteBatch, Vector2 start, Vector2 end)
    {
        Vector2 axis = end - start;
        float length = axis.Length();
        if (length < 0.001f) return;

        Vector2 dir = axis / length;
        Vector2 perp = new Vector2(-dir.Y, dir.X);

        Vector2 prev = start;
        for (int i = 1; i <= beamSegments; i++)
        {
            float t = (float)i / beamSegments;
            float taper = MathF.Sin(t * MathF.PI);
            float wave = MathF.Sin(t * MathF.PI * 2f * beamWaveFrequency * 1.5f - beamPhase) * beamWaveAmplitude * taper;
            Vector2 point = start + dir * (length * t) + perp * wave;
            spriteBatch.DrawLine(prev, point, beamColor, beamThickness);
            prev = point;
        }

        for (int i = 0; i < beamParticlesPerFrame; i++)
        {
            if (Random.Shared.NextDouble() > beamParticleEmitChance) continue;
            float t = (float)Random.Shared.NextDouble();
            float taper = MathF.Sin(t * MathF.PI);
            float wave = MathF.Sin(t * MathF.PI * 2f * beamWaveFrequency * 1.5f - beamPhase) * beamWaveAmplitude * taper;
            Vector2 point = start + dir * (length * t) + perp * wave;
            ParticleManager.AddParticle(new Particle
            {
                WorldPosition = (point - mapManager.Offset) / mapManager.Scale,
                Velocity = new Vector2(
                    (float)(Random.Shared.NextDouble() - 0.5f) * beamParticleSpread,
                    (float)(Random.Shared.NextDouble() - 0.5f) * beamParticleSpread),
                LifeSpan = beamParticleLifespan,
                Color = beamColor
            });
        }
    }
}
