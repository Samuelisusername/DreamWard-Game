using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace DreamWardUtils.Graphics;

public class TileSet
{
    private readonly TextureRegion[] _tiles;

    public int TileWidth { get; }

    public int TileHeight { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int Count { get; }

    public TimeSpan FrameDuration { get; }

    public TileSet(TextureRegion region, int tileWidth, int tileHeight, TimeSpan frameDuration = default)
    {
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Columns = region.Width/tileWidth;
        Rows = region.Height/tileHeight;
        Count = Rows * Columns;
        FrameDuration = frameDuration;

        _tiles = new TextureRegion[Count];

        for(int i = 0; i < Count; i++)
        {
            _tiles[i] = new TextureRegion(region.Texture, region.SourceRectangle.X +  (i%Columns) * tileWidth,region.SourceRectangle.Y +  (i/Columns) * tileHeight , tileWidth, tileHeight); 
        }

    }

    public TileSet(TextureAtlas atlas, string slicePrefix, string animTag)
    {
        var variantAnims = new List<Animation>();
        int v = 0;
        while (true)
        {
            try { variantAnims.Add(atlas.GetAnimation($"{slicePrefix}{v}_{animTag}")); v++; }
            catch (KeyNotFoundException) { break; }
        }

        var firstFrame = variantAnims[0].Frames[0];
        TileWidth = firstFrame.Width;
        TileHeight = firstFrame.Height;
        Columns = variantAnims[0].Frames.Count;
        Rows = variantAnims.Count;
        Count = Rows * Columns;
        FrameDuration = variantAnims[0].Delay;

        _tiles = new TextureRegion[Count];
        for (int row = 0; row < Rows; row++)
            for (int col = 0; col < Columns; col++)
                _tiles[row * Columns + col] = variantAnims[row].Frames[col];
    }

    public TextureRegion GetTile(int index) => _tiles[index];

    public TextureRegion GetTile(int column, int row)
    {
        int index = row * Columns + column;
        return GetTile(index);
    }
}
