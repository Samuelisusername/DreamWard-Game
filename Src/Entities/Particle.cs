using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

public class Particle
{
    public Vector2 WorldPosition {get; set;}

    public Vector2 Velocity {get; set;}

    public Color Color {get; set;}

    public TimeSpan LifeSpan {get; set;}

    public TimeSpan LivedFor {get; set;}

    public float Scale {get; set;} = 1f;

    public string Text {get; set;}

    public SpriteFont Font {get; set;}

    public Action<Particle, GameTime> Updater {get; set;}
}
