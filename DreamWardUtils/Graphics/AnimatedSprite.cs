using System;
using Microsoft.Xna.Framework;

namespace DreamWardUtils.Graphics;

public class AnimatedSprite : Sprite
{
    private int _currentFrame;
    private TimeSpan _elapsed;
    private Animation _animation;

    /// <summary>
    /// Gets or Sets the animation for this animated sprite.
    /// </summary>
    public Animation Animation
    {
        get => _animation;
        set
        {
            _animation = value;
            _elapsed = TimeSpan.Zero;
            Region = _animation.Frames[0];
        }
    }

    public AnimatedSprite(){}

    public AnimatedSprite(Animation anim)
    {
        Animation = anim;
    }

    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime;

        if(_elapsed >= _animation.Delay)
        {
            _elapsed -= _animation.Delay;
            _currentFrame = (_currentFrame +1) % _animation.Frames.Count;
            if(_currentFrame == 0 && _animation.IsOneShot)
            {
                Animation = Animation.NextAnimation;
            }
            else
            {
                Region = _animation.Frames[_currentFrame];
            }
        }
    }
}
