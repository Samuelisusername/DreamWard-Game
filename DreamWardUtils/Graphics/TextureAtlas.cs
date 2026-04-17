using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace DreamWardUtils.Graphics;

public class TextureAtlas 
{
    private Dictionary<string, TextureRegion> _regions;
    private Dictionary<string, Animation> _animations;
    public Texture2D Texture {get; set;}

    public TextureAtlas()
    {
        _regions = new Dictionary<string, TextureRegion>();
        _animations = new Dictionary<string, Animation>();
    }

    public TextureAtlas(Texture2D text)
    {
        _regions = new Dictionary<string, TextureRegion>();
        _animations = new Dictionary<string, Animation>();
        Texture = text;
    }

    public void AddRegion(string name, int x, int y, int width, int height)
    {
        TextureRegion region = new TextureRegion(Texture, x, y, width, height);
        _regions.Add(name, region);
    }

    public TextureRegion GetRegion(string name)
    {
        return _regions[name];
    }

    public bool RemoveRegion(string name)
    {
        return _regions.Remove(name);
    }    

    public void AddAnimation(string name, Animation animation)
    {
        _animations.Add(name, animation);
    }

    public Animation GetAnimation(string name)
    {
        return _animations[name];
    }

    public bool RemoveAnimation(string name)
    {
        return _animations.Remove(name);
    }

    public void Clear()
    {
        _regions.Clear();
    }

    private readonly List<string> _animationVariantsMonster = new List<string>{"SW", "NW", "N", "S"};

    public static TextureAtlas FromFile(ContentManager content, string fileName)
    {
        TextureAtlas atlas = new TextureAtlas();

        string filePath = Path.Combine(content.RootDirectory, fileName);

        using (Stream stream = TitleContainer.OpenStream(filePath))
        {
            using (XmlReader reader = XmlReader.Create(stream))
            {
                XDocument doc = XDocument.Load(reader);
                XElement root = doc.Root;

                // The <Texture> element contains the content path for the Texture2D to load.
                // So we will retrieve that value then use the content manager to load the texture.
                string texturePath = root.Element("Texture").Value;
                atlas.Texture = content.Load<Texture2D>(texturePath);

                // The <Regions> element contains individual <Region> elements, each one describing
                // a different texture region within the atlas.  
                //
                // Example:
                // <Regions>
                //      <Region name="spriteOne" x="0" y="0" width="32" height="32" />
                //      <Region name="spriteTwo" x="32" y="0" width="32" height="32" />
                // </Regions>
                //
                // So we retrieve all of the <Region> elements then loop through each one
                // and generate a new TextureRegion instance from it and add it to this atlas.
                var regions = root.Element("Regions")?.Elements("Region");

                if (regions != null)
                {
                    foreach (var region in regions)
                    {
                        string name = region.Attribute("name")?.Value;
                        int x = int.Parse(region.Attribute("x")?.Value ?? "0");
                        int y = int.Parse(region.Attribute("y")?.Value ?? "0");
                        int width = int.Parse(region.Attribute("width")?.Value ?? "0");
                        int height = int.Parse(region.Attribute("height")?.Value ?? "0");

                        if (!string.IsNullOrEmpty(name))
                        {
                            atlas.AddRegion(name, x, y, width, height);
                        }
                    }
                }

                var animationElements = root.Element("Animations").Elements("Animation");

                if (animationElements != null)
                {
                    foreach (var animationElement in animationElements)
                    {
                        string name = animationElement.Attribute("name")?.Value;
                        float delayInMilliseconds = float.Parse(animationElement.Attribute("delay")?.Value ?? "0");
                        TimeSpan delay = TimeSpan.FromMilliseconds(delayInMilliseconds);

                        List<TextureRegion> frames = new List<TextureRegion>();

                        var frameElements = animationElement.Elements("Frame");

                        if (frameElements != null)
                        {
                            foreach (var frameElement in frameElements)
                            {
                                string regionName = frameElement.Attribute("region").Value;
                                TextureRegion region = atlas.GetRegion(regionName);
                                frames.Add(region);
                            }
                        }

                        Animation animation = new Animation(frames, delay);
                        atlas.AddAnimation(name, animation);
                    }
                }

                return atlas;
            }
        }
    }

    public static TextureAtlas FromJsonFile(ContentManager content, string fileName)
    {
        TextureAtlas atlas = new TextureAtlas();

        string filePath = Path.Combine(content.RootDirectory, fileName);

        using (Stream stream = TitleContainer.OpenStream(filePath))
        {
            using (JsonDocument reader = JsonDocument.Parse(stream))
            {
                JsonElement root = reader.RootElement;

                // The meta.image element contains the name of the png to load we can change the json file name to the texture name to get its path.
                // So we will retrieve that value then use the content manager to load the texture.
                string textureName = root.GetProperty("meta").GetProperty("image").GetString();
                textureName = Path.GetFileNameWithoutExtension(textureName);
                string[] pathAsArray = fileName.Split("/");
                pathAsArray[pathAsArray.Length -1] = textureName;
                string texturePath = String.Join('/', pathAsArray);
                atlas.Texture = content.Load<Texture2D>(texturePath);

                // The <Regions> element contains individual <Region> elements, each one describing
                // a different texture region within the atlas.  
                //
                // Example:
                // <Regions>
                //      <Region name="spriteOne" x="0" y="0" width="32" height="32" />
                //      <Region name="spriteTwo" x="32" y="0" width="32" height="32" />
                // </Regions>
                //
                // So we retrieve all of the <Region> elements then loop through each one
                // and generate a new TextureRegion instance from it and add it to this atlas.
                // Read per-frame durations in declaration order
                var frameDurations = new List<int>();
                foreach (JsonProperty frameProp in root.GetProperty("frames").EnumerateObject())
                    frameDurations.Add(frameProp.Value.GetProperty("duration").GetInt32());

                foreach(JsonElement entity in root.GetProperty("meta").GetProperty("slices").EnumerateArray())
                {
                    string entityName = entity.GetProperty("name").GetString();
                    foreach(JsonElement frame in entity.GetProperty("keys").EnumerateArray())
                    {
                        JsonElement bounds = frame.GetProperty("bounds");
                        int x = bounds.GetProperty("x").GetInt32();
                        int y = bounds.GetProperty("y").GetInt32();
                        int width = bounds.GetProperty("w").GetInt32();
                        int height = bounds.GetProperty("h").GetInt32();
                        for(int i = 0; i < root.GetProperty("frames").GetPropertyCount(); i++)
                        {
                            atlas.AddRegion(entityName + i.ToString(), x + width * i, y, width, height);
                        }

                    }
                    foreach(JsonElement anim in root.GetProperty("meta").GetProperty("frameTags").EnumerateArray())
                    {
                        string animName = entityName + "_" + anim.GetProperty("name").GetString();
                        int from = anim.GetProperty("from").GetInt32();
                        int to = anim.GetProperty("to").GetInt32();
                        TimeSpan delay = TimeSpan.FromMilliseconds(frameDurations[from]);
                        List<TextureRegion> frames = new List<TextureRegion>();
                        for(int i = from; i <= to; i++)
                        {
                            frames.Add(atlas.GetRegion(entityName + i.ToString()));
                        }
                        Animation newAnimation = new Animation(frames, delay);
                        atlas.AddAnimation(animName, newAnimation);
                        if(animName.Contains("merge"))
                        {
                            newAnimation = new Animation(frames.ToList(), delay);
                            newAnimation.Frames.Reverse();
                            atlas.AddAnimation(animName.Replace("merge", "rebuild"), newAnimation);
                        }
                        if(animName.EndsWith("NW"))
                        {
                            animName = animName[..^2] + "NE";
                            frames.Clear();
                            for(int i = from; i <= to; i++)
                            {
                                frames.Add(atlas.GetRegion(entityName + i.ToString()));
                            }
                            newAnimation = new Animation(frames, delay);
                            atlas.AddAnimation(animName, newAnimation);
                            if(animName.Contains("merge"))
                            {
                                newAnimation = new Animation(frames.ToList(), delay);
                                newAnimation.Frames.Reverse();
                                atlas.AddAnimation(animName.Replace("merge", "rebuild"), newAnimation);
                            }
                        }
                        else if(animName.EndsWith("SW"))
                        {
                            animName = animName[..^2] + "SE";
                            frames.Clear();
                            for(int i = from; i <= to; i++)
                            {
                                frames.Add(atlas.GetRegion(entityName + i.ToString()));
                            }
                            newAnimation = new Animation(frames, delay);
                            atlas.AddAnimation(animName, newAnimation);
                            if(animName.Contains("merge"))
                            {                                
                                newAnimation = new Animation(frames.ToList(), delay);
                                newAnimation.Frames.Reverse();
                                atlas.AddAnimation(animName.Replace("merge", "rebuild"), newAnimation);
                            }
                        }
                    }

                }

                return atlas;
            }
        }
    }

    public Sprite CreateSprite(string regionName)
    {
        TextureRegion region = GetRegion(regionName);
        return new Sprite(region);
    }

    public AnimatedSprite CreateAnimatedSprite(string animationName)
    {
        Animation animation = GetAnimation(animationName);
        return new AnimatedSprite(animation);
    }

    public Dictionary<string, AnimatedSprite> CreateAnimatedSpriteAtlas(string animationName, Vector2 scale)
    {
        Dictionary<string, AnimatedSprite> res = new Dictionary<string, AnimatedSprite>();
        AnimatedSprite sprite;
        foreach (var variant in _animationVariantsMonster)
        {
            sprite =  CreateAnimatedSprite(animationName + variant);
            sprite.Scale = scale;
            res.Add(variant, sprite);
        }

        sprite = CreateAnimatedSprite(animationName + "NW");
        sprite.Scale = scale;
        sprite.SpriteEffects = SpriteEffects.FlipHorizontally;
        res.Add("NE", sprite);

        sprite = CreateAnimatedSprite(animationName + "SW");
        sprite.Scale = scale;
        sprite.SpriteEffects = SpriteEffects.FlipHorizontally;
        res.Add("SE", sprite);
        return res;
    }
}
