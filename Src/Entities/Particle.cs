using System;
using Microsoft.Xna.Framework;

public class Particle
{
    public Vector2 WorldPosition {get; set;}

    public Vector2 Velocity {get; set;}

    public  Color Color {get; set;}

    public TimeSpan LifeSpan {get; set;}

    public TimeSpan LivedFor {get; set;}
}