using System;
using System.Collections.Generic;
using DreamWardUtils.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

/// <summary>
/// A tree tile entity. Cannot be attacked, stepped on, or built over.
/// Purely decorative obstacle that blocks movement and building.
/// </summary>
public class TreeTile : Structure
{
    public override bool IsFlat => false;
    public override bool CanMove => false;

    private List<float> shakeRotations = new List<float>();
    private float shakeSpeed = 5f; //in deg per second


    protected override Vector2 _localOffset => _sprite.SpriteEffects == SpriteEffects.FlipHorizontally
            ? new Vector2(31, 16)
            : new Vector2(32, 16);

    //  new Vector2(32, 16);
    // new Vector2(-32, -96);

    public TreeTile(string animName, (int,int) pos)
        : base(animName, pos, int.MaxValue)
    {
        _sprite.SpriteEffects = Random.Shared.NextDouble() < 0.5 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _sprite.Origin =  new Vector2(64, 128-16);
    }

    public void Shake()
    {
        shakeRotations.Clear();
        float currentRot = 16;
        while(currentRot > 2)
        {
            float add = (currentRot/360.0f) * MathHelper.TwoPi;
            shakeRotations.Add(shakeRotations.Count % 2 == 0 ? add : -add);
            currentRot = Random.Shared.Next((int)currentRot/2,(int)currentRot);
        }
        shakeRotations.Add(0);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        float addAngle = shakeSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        while(shakeRotations.Count > 0)
        {
            float currentTarget = shakeRotations[0];
            if(currentTarget > 0)
            {
                if(_sprite.Rotation + addAngle > currentTarget)
                {
                    //rotated too far
                    addAngle -= currentTarget - _sprite.Rotation;
                    _sprite.Rotation = currentTarget;
                    shakeRotations.RemoveAt(0);
                    continue;
                }
                _sprite.Rotation += addAngle;
                break;
            }
            else if(currentTarget < 0)
            {
                if(_sprite.Rotation - addAngle < currentTarget)
                {
                    //rotated too far
                    addAngle -= currentTarget - _sprite.Rotation;
                    _sprite.Rotation = currentTarget;
                    shakeRotations.RemoveAt(0);
                    continue;
                }
                _sprite.Rotation -= addAngle;
                break;
            }
            else
            {
                if(_sprite.Rotation > 0)
                {
                    _sprite.Rotation -= addAngle;
                    if(_sprite.Rotation <= 0)
                    {
                        _sprite.Rotation = 0;
                        shakeRotations.Clear();
                    }
                    break;
                }
                else
                {
                    _sprite.Rotation += addAngle;
                    if(_sprite.Rotation >= 0)
                    {
                        _sprite.Rotation = 0;
                        shakeRotations.Clear();
                    }
                    break;
                }
            }
        }
        // _sprite.Rotation += 0.1f;
    }
}
