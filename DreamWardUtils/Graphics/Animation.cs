using System;
using System.Collections.Generic;

namespace DreamWardUtils.Graphics;

public class Animation 
{
    public List<TextureRegion> Frames { get; set; }

    public TimeSpan Delay { get; set; }

    public bool IsOneShot {get; set;}

    public Animation NextAnimation {get;set;}

    public Animation()
    {
        Frames = new List<TextureRegion>();
        Delay = TimeSpan.FromMilliseconds(100);
    }

    public Animation(List<TextureRegion> frames, TimeSpan delay, bool isOneShot = false, Animation nextAnimation = null)
    {
        Frames = frames;
        Delay = delay;
        IsOneShot = isOneShot;
        NextAnimation = nextAnimation;
    }

    public Animation GetOneShotAnimation(Animation nextAnimation)
    {
        return new Animation(Frames, Delay, true, nextAnimation);
    }

}
