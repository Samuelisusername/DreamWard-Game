using System.Linq;
using Gamelab;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class SimpleNightTests : NightScenarioTestBase
{
    [Test]
    public void RegularMonster_DestroysTent()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var tent = s.PlaceTent((7, 7));
        var m = s.SpawnMonster<RegularMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (tent.IsDestroyed) break;
        }

        Assert.That(tent.IsDestroyed, Is.True,
            $"Tent survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void RegularMonster_DestroysSheep()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlaceMeadowWithSheep((7, 7));
        var m = s.SpawnMonster<RegularMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (!meadow.HasSheep) break;
        }

        Assert.That(meadow.HasSheep, Is.False,
            $"Sheep still alive after 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void RegularMonster_DestroysPlaceableMeadow()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlacePlacableMeadow((7, 7));
        var m = s.SpawnMonster<RegularMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (meadow.IsDestroyed) break;
        }

        Assert.That(meadow.IsDestroyed, Is.True,
            $"PlacableMeadow survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void RegularMonster_WithInfiniteHealth_DestroysAllDefences()
    {
        var s = new NightScenario(cols: 14, rows: 14);

        var areaTower   = new AreaTower((6, 6));                    s.MapManager.PlaceEntity(areaTower,   (6, 6));
        var buff        = new Buff((7, 6));                         s.MapManager.PlaceEntity(buff,         (7, 6));
        var decoy       = new Decoy((8, 6));                        s.MapManager.PlaceEntity(decoy,        (8, 6));
        var sniperTower = new SniperTower((6, 8));                  s.MapManager.PlaceEntity(sniperTower, (6, 8));
        var wallTile    = new WallTile("wallTile_idle", (7, 8));    s.MapManager.PlaceEntity(wallTile,    (7, 8));
        var fence       = new Fence((8, 8));                        s.MapManager.PlaceEntity(fence,        (8, 8));
        Structure[] targets = [areaTower, buff, decoy, sniperTower, wallTile, fence];

        var m = s.SpawnMonster<RegularMonster>((7, 7), health: 99999f);
        for (int i = 0; i < 1000; i++)
        {
            s.Tick();
            if (targets.All(t => t.IsDestroyed)) break;
        }

        Assert.Multiple(() =>
        {
            Assert.That(areaTower.IsDestroyed,   Is.True, "AreaTower not destroyed");
            Assert.That(buff.IsDestroyed,         Is.True, "Buff not destroyed");
            Assert.That(decoy.IsDestroyed,        Is.True, "Decoy not destroyed");
            Assert.That(sniperTower.IsDestroyed,  Is.True, "SniperTower not destroyed");
            Assert.That(wallTile.IsDestroyed,     Is.True, "WallTile not destroyed");
            Assert.That(fence.IsDestroyed,         Is.True, "Fence not destroyed");
        });
    }

    [Test]
    public void BigMonster_DestroysTent()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var tent = s.PlaceTent((7, 7));
        var m = s.SpawnMonster<BigMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (tent.IsDestroyed) break;
        }

        Assert.That(tent.IsDestroyed, Is.True,
            $"Tent survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void BigMonster_DestroysSheep()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlaceMeadowWithSheep((7, 7));
        var m = s.SpawnMonster<BigMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (!meadow.HasSheep) break;
        }

        Assert.That(meadow.HasSheep, Is.False,
            $"Sheep still alive after 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void BigMonster_DestroysPlaceableMeadow()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlacePlacableMeadow((7, 7));
        var m = s.SpawnMonster<BigMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (meadow.IsDestroyed) break;
        }

        Assert.That(meadow.IsDestroyed, Is.True,
            $"PlacableMeadow survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void BigMonster_WithInfiniteHealth_DestroysAllDefences()
    {
        var s = new NightScenario(cols: 14, rows: 14);

        var areaTower   = new AreaTower((6, 6));                    s.MapManager.PlaceEntity(areaTower,   (6, 6));
        var buff        = new Buff((7, 6));                         s.MapManager.PlaceEntity(buff,         (7, 6));
        var decoy       = new Decoy((8, 6));                        s.MapManager.PlaceEntity(decoy,        (8, 6));
        var sniperTower = new SniperTower((6, 8));                  s.MapManager.PlaceEntity(sniperTower, (6, 8));
        var wallTile    = new WallTile("wallTile_idle", (7, 8));    s.MapManager.PlaceEntity(wallTile,    (7, 8));
        var fence       = new Fence((8, 8));                        s.MapManager.PlaceEntity(fence,        (8, 8));
        Structure[] targets = [areaTower, buff, decoy, sniperTower, wallTile, fence];

        var m = s.SpawnMonster<BigMonster>((7, 7), health: 99999f);
        for (int i = 0; i < 1000; i++)
        {
            s.Tick();
            if (targets.All(t => t.IsDestroyed)) break;
        }

        Assert.Multiple(() =>
        {
            Assert.That(areaTower.IsDestroyed,   Is.True, "AreaTower not destroyed");
            Assert.That(buff.IsDestroyed,         Is.True, "Buff not destroyed");
            Assert.That(decoy.IsDestroyed,        Is.True, "Decoy not destroyed");
            Assert.That(sniperTower.IsDestroyed,  Is.True, "SniperTower not destroyed");
            Assert.That(wallTile.IsDestroyed,     Is.True, "WallTile not destroyed");
            Assert.That(fence.IsDestroyed,         Is.True, "Fence not destroyed");
        });
    }

    [Test]
    public void SmallMonster_DestroysTent()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var tent = s.PlaceTent((7, 7));
        var m = s.SpawnMonster<SmallMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (tent.IsDestroyed) break;
        }

        Assert.That(tent.IsDestroyed, Is.True,
            $"Tent survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void SmallMonster_DestroysSheep()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlaceMeadowWithSheep((7, 7));
        var m = s.SpawnMonster<SmallMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (!meadow.HasSheep) break;
        }

        Assert.That(meadow.HasSheep, Is.False,
            $"Sheep still alive after 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void SmallMonster_DestroysPlaceableMeadow()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlacePlacableMeadow((7, 7));
        var m = s.SpawnMonster<SmallMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (meadow.IsDestroyed) break;
        }

        Assert.That(meadow.IsDestroyed, Is.True,
            $"PlacableMeadow survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void SmallMonster_WithInfiniteHealth_DestroysAllDefences()
    {
        var s = new NightScenario(cols: 14, rows: 14);

        var areaTower   = new AreaTower((6, 6));                    s.MapManager.PlaceEntity(areaTower,   (6, 6));
        var buff        = new Buff((7, 6));                         s.MapManager.PlaceEntity(buff,         (7, 6));
        var decoy       = new Decoy((8, 6));                        s.MapManager.PlaceEntity(decoy,        (8, 6));
        var sniperTower = new SniperTower((6, 8));                  s.MapManager.PlaceEntity(sniperTower, (6, 8));
        var wallTile    = new WallTile("wallTile_idle", (7, 8));    s.MapManager.PlaceEntity(wallTile,    (7, 8));
        var fence       = new Fence((8, 8));                        s.MapManager.PlaceEntity(fence,        (8, 8));
        Structure[] targets = [areaTower, buff, decoy, sniperTower, wallTile, fence];

        var m = s.SpawnMonster<SmallMonster>((7, 7), health: 99999f);
        for (int i = 0; i < 1000; i++)
        {
            s.Tick();
            if (targets.All(t => t.IsDestroyed)) break;
        }

        Assert.Multiple(() =>
        {
            Assert.That(areaTower.IsDestroyed,   Is.True, "AreaTower not destroyed");
            Assert.That(buff.IsDestroyed,         Is.True, "Buff not destroyed");
            Assert.That(decoy.IsDestroyed,        Is.True, "Decoy not destroyed");
            Assert.That(sniperTower.IsDestroyed,  Is.True, "SniperTower not destroyed");
            Assert.That(wallTile.IsDestroyed,     Is.True, "WallTile not destroyed");
            Assert.That(fence.IsDestroyed,         Is.True, "Fence not destroyed");
        });
    }

    [Test]
    public void FlyingMonster_DestroysTent()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var tent = s.PlaceTent((7, 7));
        var m = s.SpawnMonster<FlyingMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (tent.IsDestroyed) break;
        }

        Assert.That(tent.IsDestroyed, Is.True,
            $"Tent survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void FlyingMonster_DestroysSheep()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlaceMeadowWithSheep((7, 7));
        var m = s.SpawnMonster<FlyingMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (!meadow.HasSheep) break;
        }

        Assert.That(meadow.HasSheep, Is.False,
            $"Sheep still alive after 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void FlyingMonster_DestroysPlaceableMeadow()
    {
        var s = new NightScenario(cols: 14, rows: 14);
        var meadow = s.PlacePlacableMeadow((7, 7));
        var m = s.SpawnMonster<FlyingMonster>((7, 0));

        for (int i = 0; i < 200; i++)
        {
            s.Tick();
            if (meadow.IsDestroyed) break;
        }

        Assert.That(meadow.IsDestroyed, Is.True,
            $"PlacableMeadow survived 200 ticks; monster last at {m.TilePosition}");
    }

    [Test]
    public void FlyingMonster_WithInfiniteHealth_DestroysAllDefences()
    {
        var s = new NightScenario(cols: 14, rows: 14);

        var areaTower   = new AreaTower((6, 6));                    s.MapManager.PlaceEntity(areaTower,   (6, 6));
        var buff        = new Buff((7, 6));                         s.MapManager.PlaceEntity(buff,         (7, 6));
        var decoy       = new Decoy((8, 6));                        s.MapManager.PlaceEntity(decoy,        (8, 6));
        var sniperTower = new SniperTower((6, 8));                  s.MapManager.PlaceEntity(sniperTower, (6, 8));
        var fence       = new Fence((8, 8));                        s.MapManager.PlaceEntity(fence,        (8, 8));
        Structure[] targets = [areaTower, buff, decoy, sniperTower, fence];

        var m = s.SpawnMonster<FlyingMonster>((7, 7), health: 99999f);
        for (int i = 0; i < 1000; i++)
        {
            s.Tick();
            if (targets.All(t => t.IsDestroyed)) break;
        }

        Assert.Multiple(() =>
        {
            Assert.That(areaTower.IsDestroyed,   Is.True, "AreaTower not destroyed");
            Assert.That(buff.IsDestroyed,         Is.True, "Buff not destroyed");
            Assert.That(decoy.IsDestroyed,        Is.True, "Decoy not destroyed");
            Assert.That(sniperTower.IsDestroyed,  Is.True, "SniperTower not destroyed");
            Assert.That(fence.IsDestroyed,         Is.True, "Fence not destroyed");
        });
    }
}
