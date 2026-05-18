using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using DreamWardUtils.Graphics;
using Gamelab;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests;

public abstract class NightScenarioTestBase
{
    protected static void SetStaticProperty(Type type, string propertyName, object value)
    {
        type.GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, value);
    }

    
    protected static void SilenceAudio() // Prevents an OpenAL crash.
    {
        typeof(Gamelab.Managers.AudioManager)
            .GetField("_sounds", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, new Dictionary<string, Microsoft.Xna.Framework.Audio.SoundEffect>());
    }

    
    protected static T CreateUninitializedMonster<T>((int col, int row) tile, float health = 50f) // Avoid calling GetNextPosition at construction
        where T : Monster
    {
        T m = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        m.TilePosition = tile;
        m.NextTarget   = tile;
        typeof(Entity)
            .GetField("<CurrentHealth>k__BackingField",
                      BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(m, health);
        return m;
    }

    protected static BalanceParams LoadBalance()
    {
        string path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Content", "Data", "Balance.json");
        using var sr = new StreamReader(path);
        return JsonConvert.DeserializeObject<BalanceParams>(sr.ReadToEnd());
    }

    protected static void EnsureTestTextureAtlases()
    {
        var stubRegion = new TextureRegion();
        var stubAnim   = new Animation(
            new List<TextureRegion> { stubRegion }, TimeSpan.FromMilliseconds(125));

        // --- Monsters --------------------------------------------------------
        // MonsterGroup.SetAnimation builds keys of the form:
        //   type + isAmalgam + actionAndDir
        // where isAmalgam is "_" (≤2 monsters in group) or "_amalgam_" (>2).
        string[] types    = { "normal", "fly", "smol", "strong" };
        string[] amalgams = { "_", "_amalgam_" };
        string[] dirs     = { "S", "N", "NW", "NE", "SW", "SE" };

        var monstersAtlas = new TextureAtlas();
        var addedMonster  = new HashSet<string>();
        void AddMonster(string key)
        {
            if (addedMonster.Add(key)) monstersAtlas.AddAnimation(key, stubAnim);
        }

        foreach (var t in types)
        {
            // MonsterGroup constructor always calls CreateAnimatedSprite(type + "_idleS")
            AddMonster($"{t}_idleS");

            foreach (var a in amalgams)
            foreach (var d in dirs)
            {
                AddMonster($"{t}{a}idle{d}");
                AddMonster($"{t}{a}walk{d}");
                AddMonster($"{t}{a}attack{d}");
                AddMonster($"{t}{a}merge{d}_smokeWalk");
                AddMonster($"{t}{a}mergeIdle{d}_smokeWalk");
                AddMonster($"{t}{a}rebuild{d}_walk{d}");
                AddMonster($"{t}{a}rebuildIdle{d}_idle{d}");
                AddMonster($"{t}{a}rebuildIdle{d}_attack{d}");
            }
        }

        // Structure constructor calls CreateAnimatedSprite(animName) on day_top
        // and day_top_base atlases.
        string[] structureAnims =
        {
            "tent_idle", "wall_idle", "fence_idle",
            "towerSniper_idle", "towerArea_idle", "towerAOE_idle",
            "meadow_idle", "placableMeadow_idle",
            "bomb_idle", "spike_idle", "buff_idle", "decoy_idle",
            "treeBig_idle", "treeMid_idle", "treeSmall_idle",
            "wallTile_idle", "voidTile_idle", "void_idle",
            "sheepSmall_idle",
            "meadowPlaced_idle",
        };
        var structureAtlas = new TextureAtlas();
        foreach (var a in structureAnims)
            structureAtlas.AddAnimation(a, stubAnim);
        structureAtlas.AddRegion("sheepBig0", 0, 0, 0, 0); // Meadow constructor

        // Tent constructor: GamelabGame.TextureAtlas["day_player"].CreateSprite("player0")
        // CreateSprite calls GetRegion
        var dayPlayerAtlas = new TextureAtlas();
        dayPlayerAtlas.AddRegion("player0", 0, 0, 0, 0);

        GamelabGame.TextureAtlas = new Dictionary<string, TextureAtlas>
        {
            ["monsters"]       = monstersAtlas,
            ["day_top"]        = structureAtlas,
            ["day_top_base"]   = structureAtlas,
            ["night_top"]      = structureAtlas,
            ["night_top_base"] = structureAtlas,
            ["day_player"]     = dayPlayerAtlas,
            ["interface"]      = structureAtlas,
        };
    }

    // ------------------------------------------------------------------------
    protected sealed class NightScenario
    {
        public MapManager         MapManager  { get; }
        public PathFindingManager PathFinding { get; }
        public CombatManager      Combat      { get; }

        private static readonly TimeSpan TickElapsed = TimeSpan.FromMilliseconds(500);
        private TimeSpan _totalTime = TimeSpan.Zero;

        public NightScenario(int cols = 12, int rows = 12, int seed = 42)
        {
            var balance = LoadBalance();

            // GetUninitializedObject skips the GamelabGame constructor (MonoGame graphics device init).
            var game = (GamelabGame)RuntimeHelpers.GetUninitializedObject(typeof(GamelabGame));
            game.Balance = balance;
            SetStaticProperty(typeof(GamelabGame), "Instance", game);
            SetStaticProperty(typeof(GamelabGame), "Rng", new Random(seed));
            var stubRegion  = new TextureRegion(null, 0, 0, 128, 64);
            var stubTileSet = new TileSet(stubRegion, 64, 64);
            MapManager = new MapManager(stubTileSet, stubTileSet, cols, rows);
            SetStaticProperty(typeof(GamelabGame), "MapManager", MapManager);

            EnsureTestTextureAtlases(); // must precede any Structure or MonsterGroup creation

            SetStaticProperty(typeof(GamelabGame), "SanityManager",    new SanityManager());
            SetStaticProperty(typeof(GamelabGame), "GamePhaseManager", new GamePhaseManager());
            SetStaticProperty(typeof(GamelabGame), "CardManager",      new CardManager());
            PathFinding = new PathFindingManager();
            SetStaticProperty(typeof(GamelabGame), "PathFindingManager", PathFinding);

            Combat = new CombatManager();
            SetStaticProperty(typeof(GamelabGame), "CombatManager", Combat);

            SilenceAudio();
        }

        public Tent PlaceTent((int col, int row) tile)
        {
            var tent = new Tent("tent_idle", tile);
            Assert.That(MapManager.PlaceEntity(tent, tile), Is.True, $"PlaceTent at {tile}");
            return tent;
        }

        public Wall PlaceWall((int col, int row) tile)
        {
            var wall = new Wall(tile);
            Assert.That(MapManager.PlaceEntity(wall, tile), Is.True, $"PlaceWall at {tile}");
            return wall;
        }

        public Fence PlaceFence((int col, int row) tile)
        {
            var fence = new Fence(tile);
            Assert.That(MapManager.PlaceEntity(fence, tile), Is.True, $"PlaceFence at {tile}");
            return fence;
        }

        public PlacableMeadow PlacePlacableMeadow((int col, int row) tile, int sanityGain = 5)
        {
            var meadow = new PlacableMeadow(tile, sanityGain);
            Assert.That(MapManager.PlaceEntity(meadow, tile), Is.True, $"PlacePlacableMeadow at {tile}");
            return meadow;
        }

        public Meadow PlaceMeadowWithSheep((int col, int row) tile, int sanityGain = 5)
        {
            var meadow = new Meadow(tile, sanityGain);
            Assert.That(MapManager.PlaceSheep(meadow, tile), Is.True, $"PlaceMeadowWithSheep at {tile}");
            return meadow;
        }

        public T SpawnMonster<T>((int col, int row) tile, float health = 50f)
            where T : Monster
        {
            var m = CreateUninitializedMonster<T>(tile, health);
            MapManager.GetMonsterContainer(tile).AddExistingMonster(m);
            return m;
        }

        /// <summary>Advances the simulation by TickElapsed.</summary>
        public void Tick()
        {
            _totalTime += TickElapsed;
            Combat.ManageMoveAndAttack(new GameTime(_totalTime, TickElapsed));
        }

        public void Tick(int n) { for (int i = 0; i < n; i++) Tick(); }

        public IEnumerable<Monster> LivingMonsters()
        {
            for (int c = 0; c < MapManager.Columns; c++)
            for (int r = 0; r < MapManager.Rows;    r++)
            {
                var ctr = MapManager.GetMonsterContainer((c, r));
                if (ctr == null) continue;
                foreach (var g in ctr.MonsterGroups)
                foreach (var m in g.Monsters)
                    if (!m.IsDestroyed) yield return m;
            }
        }
    }
}
