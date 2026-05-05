using System;
using System.Collections.Generic;
using Gamelab;
using Gamelab.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// A tree tile entity. Cannot be attacked, stepped on, or built over.
/// Purely decorative obstacle that blocks movement and building.
/// </summary>
public class TreeTile : Structure
{
    public override bool CanMove => false;

    private List<float> shakeRotations = new List<float>();
    private float shakeSpeed = 5f; //in deg per second


    protected override Vector2 _localOffset => _spriteTop.SpriteEffects == SpriteEffects.FlipHorizontally
            ? new Vector2(31, 16)
            : new Vector2(32, 16);

    protected override (string, string)[] infos => [("I am tree:", "If you see this something went wrong")];

    //  new Vector2(32, 16);
    // new Vector2(-32, -96);

    public TreeTile(string animName, (int,int) pos)
        : base(animName, pos)
    {
        var flip = Random.Shared.NextDouble() < 0.5 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        _spriteTop.SpriteEffects = flip;
        _spriteBase.SpriteEffects = flip;
        _spriteTop.Origin =  new Vector2(64, 128-16);
        _spriteBase.Origin =  new Vector2(64, 128-16);
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
        int nr = Random.Shared.Next(1,21);
        AudioManager.Play($"rustle{nr:D2}");
        var worldPos = mapManager.GetWorldPositionOfTile(TilePosition) + (new Vector2(32, -10) * mapManager.Scale);
        // for(int i = 0; i < 20; i++)
        // {
        //     ParticleManager.AddParticle(new Particle
        //     {
        //         WorldPosition = (worldPos - (Vector2.UnitX * ((float)Random.Shared.NextDouble() -0.5f) * 20 * mapManager.Scale) - mapManager.Offset)/mapManager.Scale,
        //         Velocity = new Vector2((float)(Random.Shared.NextDouble() -0.5f) * 10, (float)(Random.Shared.NextDouble()) * 150),
        //         LifeSpan = TimeSpan.FromSeconds(0.5),
        //         Color = Color.ForestGreen
        //     });
        // }
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
                if(_spriteTop.Rotation + addAngle > currentTarget)
                {
                    //rotated too far
                    addAngle -= currentTarget - _spriteTop.Rotation;
                    _spriteTop.Rotation = currentTarget;
                    shakeRotations.RemoveAt(0);
                    continue;
                }
                _spriteTop.Rotation += addAngle;
                break;
            }
            else if(currentTarget < 0)
            {
                if(_spriteTop.Rotation - addAngle < currentTarget)
                {
                    //rotated too far
                    addAngle -= currentTarget - _spriteTop.Rotation;
                    _spriteTop.Rotation = currentTarget;
                    shakeRotations.RemoveAt(0);
                    continue;
                }
                _spriteTop.Rotation -= addAngle;
                break;
            }
            else
            {
                if(_spriteTop.Rotation > 0)
                {
                    _spriteTop.Rotation -= addAngle;
                    if(_spriteTop.Rotation <= 0)
                    {
                        _spriteTop.Rotation = 0;
                        shakeRotations.Clear();
                    }
                    break;
                }
                else
                {
                    _spriteTop.Rotation += addAngle;
                    if(_spriteTop.Rotation >= 0)
                    {
                        _spriteTop.Rotation = 0;
                        shakeRotations.Clear();
                    }
                    break;
                }
            }
        }
        // _sprite.Rotation += 0.1f;
    }

    public override void DrawCard(SpriteBatch spriteBatch, Rectangle screen)
    {
        return;
    }
}
