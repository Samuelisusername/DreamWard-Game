using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Gamelab;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class NightFuzzer : NightScenarioTestBase
{
    private record CountRange(int Min, int Max)
    {
        public int   PickInt  (Random rng) => rng.Next(Min, Max + 1);
        public float PickFloat(Random rng) => (float)rng.Next(Min, Max + 1);
    }

    private sealed class GeneratorParams
    {
        public int  Seed    = 1;
        public int  Repeat  = 50;
        public int  MaxTick = 1000;
        public int  Cols = 7, Rows = 7;
        public float MinStructuresFill = 0f;
        public float MaxStructuresFill = 1f;
        public float PlaceSheepProbability = 0.8f;
        public Dictionary<Type, CountRange> MonsterHealth   = new();
        public Dictionary<Type, CountRange> MonsterCounts = new()
        {
            [typeof(RegularMonster)] = new(0, 10),
            [typeof(FlyingMonster)]  = new(0, 10),
            [typeof(SmallMonster)]  = new(0, 20),
            [typeof(BigMonster)]     = new(0, 10),
        };
        public Dictionary<Type, CountRange> StructureCounts = new()
        {
            [typeof(Wall)]        = new(0, 100),
            [typeof(Fence)]       = new(0, 100),
            [typeof(SniperTower)] = new(0, 30),
            [typeof(AreaTower)]   = new(0, 30),
            [typeof(Buff)]        = new(0, 20),
            [typeof(Decoy)]       = new(0, 20),
            [typeof(Meadow)]      = new(0, 10),
            [typeof(VoidTile)]    = new(0, 40),
            [typeof(TreeTile)]    = new(0, 60),
        };
    }

    private static (NightScenario s, List<Monster> monsters) GenerateScenario(int seed, GeneratorParams p)
    {
        var rng = new Random(seed);
        var s   = new NightScenario(p.Cols, p.Rows, seed);

        (int col, int row)? tentTile = null;
        tentTile = PickFreeInterior(rng, s);
        s.PlaceTent(tentTile.Value);

        if (rng.NextDouble() < p.PlaceSheepProbability)
        {
            var sheepTile = PickFreeInterior(rng, s);
            s.PlaceMeadowWithSheep(sheepTile);
        }

        var placedBlockers    = new HashSet<(int, int)>();
        int bfsThreshold      = Math.Min(p.Cols, Math.Min(p.Rows, 6));
        int totalTiles        = p.Cols * p.Rows;
        int minBudget         = (int)(p.MinStructuresFill * totalTiles);
        int maxBudget         = (int)(p.MaxStructuresFill * totalTiles);
        int structureBudget   = rng.Next(minBudget, Math.Max(minBudget, maxBudget) + 1);
        int structuresPlaced  = 0;

        foreach (var (type, range) in p.StructureCounts)
        {
            if (structuresPlaced >= structureBudget) break;
            bool isBlocker = IsPermanentBlocker(type);
            int  count     = range.PickInt(rng);

            for (int i = 0; i < count && structuresPlaced < structureBudget; i++)
            {
                var candidates = FreeTiles(s)
                    .Where(t => t != tentTile)
                    .OrderBy(_ => rng.Next())
                    .ToList();

                foreach (var tile in candidates)
                {
                    if (isBlocker && tentTile.HasValue)
                    {
                        var testSet = new HashSet<(int, int)>(placedBlockers) { tile };
                        if (testSet.Count >= bfsThreshold && !IsTentReachable(tentTile.Value, testSet, s))
                            continue;
                        PlaceGeneratedStructure(type, tile, s, rng);
                        placedBlockers.Add(tile);
                    }
                    else
                    {
                        PlaceGeneratedStructure(type, tile, s, rng);
                    }
                    structuresPlaced++;
                    break;
                }
            }
        }

        var reachableFromTent = tentTile.HasValue
            ? GetReachableTiles(tentTile.Value, placedBlockers)
            : null;

        var monsters = new List<Monster>();
        foreach (var (type, range) in p.MonsterCounts)
        {
            int count    = range.PickInt(rng);
            var freeEdge = EdgeTiles(s)
                .Where(t => s.MapManager.GetStructure(t) == null)
                .Where(t => reachableFromTent == null || reachableFromTent.Contains(t))
                .ToList();

            for (int i = 0; i < count && freeEdge.Count > 0; i++)
            {
                int idx  = rng.Next(freeEdge.Count);
                var tile = freeEdge[idx];
                freeEdge.RemoveAt(idx);
                float health = p.MonsterHealth.TryGetValue(type, out var hr)
                    ? hr.PickFloat(rng) : 50f;
                monsters.Add(SpawnGeneratedMonster(s, type, tile, health));
            }
        }

        return (s, monsters);
    }

    private static void CheckInvariantsAndOutcome(
        NightScenario s, List<Monster> monsters, int maxTick, int seed)
    {
        var prevPos          = monsters.ToDictionary(m => m, m => m.TilePosition);
        var posHistory       = monsters.ToDictionary(m => m, _ => new Queue<(int, int)>());
        var attackTgtHistory = monsters.ToDictionary(m => m, _ => new Queue<(int, int)?>());

        for (int tick = 1; tick <= maxTick; tick++)
        {
            s.Tick();

            foreach (var m in monsters)
            {
                if (m.IsDestroyed) continue;
                var history = posHistory[m];
                history.Enqueue(m.TilePosition);
                if (history.Count > 150) history.Dequeue();
                var tgtHist = attackTgtHistory[m];
                tgtHist.Enqueue(m.AttackTarget);
                if (tgtHist.Count > 150) tgtHist.Dequeue();
                CheckMonsterInvariants(m, prevPos[m], history, tgtHist, s, seed, tick);
                prevPos[m] = m.TilePosition;
            }

            if (monsters.All(m => m.IsDestroyed) || GamelabGame.SanityManager.Sanity <= 0)
                return;
        }

        int survivors = monsters.Count(m => !m.IsDestroyed);
        Assert.That(survivors == 0 || GamelabGame.SanityManager.Sanity <= 0, Is.True,
            $"[seed {seed}] Night unresolved: {survivors} monster(s) still alive after {maxTick} ticks");
    }

    private static void CheckMonsterInvariants(
        Monster m, (int, int) prev, Queue<(int, int)> history, Queue<(int, int)?> tgtHistory,
        NightScenario s, int seed, int tick)
    {
        string ctx   = $"--- ERROR ---\n[seed {seed} tick {tick}] {m.GetType().Name}";
        var pos      = m.TilePosition;
        var (cc, cr) = pos;
        var (pc, pr) = prev;
        string Diag() => $"\n\n{NeighborInfo(m, pos, s)}\n{FormatHistory(history, tgtHistory, s)}\n{RenderMap(s, m)}";

        // Position
        if (MapUtils.CalculateTileDistance(pos, prev) > 1)
            Assert.Fail($"{ctx} teleported ({pc},{pr}) → ({cc},{cr}){Diag()}");
        if (!MapUtils.IsInBounds(pos))
            Assert.Fail($"{ctx} out of bounds at ({cc},{cr}){Diag()}");
        var tile = s.MapManager.GetStructure(pos);
        if (tile != null && !tile.IsDestroyed && !m.CanStepOn(tile))
            Assert.Fail($"{ctx} standing on forbidden {tile.GetType().Name} at ({cc},{cr}){Diag()}");

        // Attack target
        if (m.AttackTarget.HasValue)
        {
            var tgt          = m.AttackTarget.Value;
            var tgtStructure = s.MapManager.GetStructure(tgt);
            if (MapUtils.CalculateTileDistance(pos, tgt) > 1)
                Assert.Fail($"{ctx} AttackTarget {tgt} not adjacent{Diag()}");
            if (tgtStructure == null)
                Assert.Fail($"{ctx} AttackTarget {tgt} has no structure{Diag()}");
            if (tgtStructure?.IsDestroyed == true)
                Assert.Fail($"{ctx} AttackTarget {tgt} is already destroyed{Diag()}");
        }

        // Progress
        if (history.Count < 150) return;
        var tgtInfo = m.AttackTarget.HasValue
            ? $"AttackTarget={m.AttackTarget.Value} ({s.MapManager.GetStructure(m.AttackTarget.Value)?.GetType().Name ?? "none"})"
            : "no AttackTarget";
        var progressTiles = history.Zip(tgtHistory)
            .Where(pair =>
            {
                var tgt = pair.Second;
                if (!tgt.HasValue) return true;
                var st = s.MapManager.GetStructure(tgt.Value);
                return st != null && !st.IsDestroyed;
            })
            .Select(pair => pair.First)
            .ToList();
        int maxRepeats = progressTiles.Count > 0
            ? progressTiles.GroupBy(p => p).Max(g => g.Count()) : 0;
        if (maxRepeats >= 150)
            Assert.Fail($"{ctx} one tile visited {maxRepeats}/150 ticks, {tgtInfo}{Diag()}");
    }

    private static string FormatHistory(
        Queue<(int, int)> posHistory, Queue<(int, int)?> tgtHistory, NightScenario s)
    {
        var sb = new System.Text.StringBuilder("--- HISTORY ---\nPosition    AttackTarget\n");
        foreach (var (pos, tgt) in posHistory.Reverse().Zip(tgtHistory.Reverse()))
        {
            string tgtStr = tgt.HasValue
                ? $"{s.MapManager.GetStructure(tgt.Value)?.GetType().Name ?? "?"}@{tgt.Value}"
                : "null";
            sb.AppendLine($"{pos}  >>   {tgtStr}");
        }
        return sb.ToString();
    }

    private static IEnumerable<(int col, int row)> FreeTiles(NightScenario s)
    {
        for (int c = 0; c < s.MapManager.Columns; c++)
        for (int r = 0; r < s.MapManager.Rows;    r++)
            if (s.MapManager.GetStructure((c, r)) == null)
                yield return (c, r);
    }

    private static IEnumerable<(int col, int row)> EdgeTiles(NightScenario s)
    {
        for (int c = 0; c < s.MapManager.Columns; c++)
        for (int r = 0; r < s.MapManager.Rows;    r++)
            if (c == 0 || c == s.MapManager.Columns - 1 || r == 0 || r == s.MapManager.Rows - 1)
                yield return (c, r);
    }

    private static (int col, int row) PickFreeInterior(Random rng, NightScenario s)
    {
        int margin = Math.Max(0, Math.Min(2, Math.Min(s.MapManager.Columns, s.MapManager.Rows) / 2 - 1));
        var candidates = new List<(int col, int row)>();
        for (int c = margin; c < s.MapManager.Columns - margin; c++)
        for (int r = margin; r < s.MapManager.Rows    - margin; r++)
            if (s.MapManager.GetStructure((c, r)) == null)
                candidates.Add((c, r));
        if (candidates.Count == 0)
            candidates.AddRange(FreeTiles(s));
        return candidates[rng.Next(candidates.Count)];
    }

    private static HashSet<(int, int)> GetReachableTiles(
        (int, int) tentTile, HashSet<(int, int)> blockers)
    {
        var visited = new HashSet<(int, int)> { tentTile };
        var queue   = new Queue<(int, int)>();
        queue.Enqueue(tentTile);
        while (queue.Count > 0)
        {
            foreach (var nb in MapUtils.GetSurroundingTiles(queue.Dequeue()))
            {
                if (visited.Contains(nb) || blockers.Contains(nb)) continue;
                visited.Add(nb);
                queue.Enqueue(nb);
            }
        }
        return visited;
    }

    private static bool IsTentReachable(
        (int, int) tentTile, HashSet<(int, int)> blockers, NightScenario s)
    {
        var reachable = GetReachableTiles(tentTile, blockers);
        return reachable.Any(t =>
        {
            var (c, r) = t;
            return c == 0 || c == s.MapManager.Columns - 1 || r == 0 || r == s.MapManager.Rows - 1;
        });
    }

    private static bool IsPermanentBlocker(Type structureType)
    {
        var dummy = (Structure)RuntimeHelpers.GetUninitializedObject(structureType);
        Monster[] samples =
        [
            (RegularMonster)RuntimeHelpers.GetUninitializedObject(typeof(RegularMonster)),
            (BigMonster)    RuntimeHelpers.GetUninitializedObject(typeof(BigMonster)),
            (SmallMonster)  RuntimeHelpers.GetUninitializedObject(typeof(SmallMonster)),
            (FlyingMonster) RuntimeHelpers.GetUninitializedObject(typeof(FlyingMonster)),
        ];
        return samples.Any(m => !m.CanStepOn(dummy) && !m.CanTarget(dummy));
    }

    private static void PlaceGeneratedStructure(
        Type type, (int, int) tile, NightScenario s, Random rng)
    {
        if (type == typeof(Meadow)) { s.PlaceMeadowWithSheep(tile); return; }

        string[] treeAnims = ["treeBig_idle", "treeMid_idle", "treeSmall_idle"];
        Structure st;
        if      (type == typeof(Wall))        st = new Wall(tile);
        else if (type == typeof(Fence))       st = new Fence(tile);
        else if (type == typeof(SniperTower)) st = new SniperTower(tile);
        else if (type == typeof(AreaTower))   st = new AreaTower(tile);
        else if (type == typeof(Buff))        st = new Buff(tile);
        else if (type == typeof(Decoy))       st = new Decoy(tile);
        else if (type == typeof(WallTile))    st = new WallTile("wallTile_idle", tile);
        else if (type == typeof(VoidTile))    st = new VoidTile(tile);
        else if (type == typeof(TreeTile))    st = new TreeTile(treeAnims[rng.Next(treeAnims.Length)], tile);
        else throw new ArgumentException($"Unrecognized structure type {type.Name}");
        s.MapManager.PlaceEntity(st, tile);
    }

    private static string NeighborInfo(Monster m, (int, int) pos, NightScenario s)
    {
        var sb = new System.Text.StringBuilder("--- NEIGHBORS ---\n");
        foreach (var nb in MapUtils.GetSurroundingTiles(pos))
        {
            var st = s.MapManager.GetStructure(nb);
            string stName = st == null ? "empty" : $"{st.GetType().Name}{(st.IsDestroyed ? "(destroyed)" : "")}";
            bool canStep   = st == null || m.CanStepOn(st);
            bool canTarget = st != null && m.CanTarget(st);

            var monsterCounts = s.MapManager.GetMonsterContainer(nb)?.MonsterGroups
                .SelectMany(g => g.Monsters)
                .Where(x => !x.IsDestroyed)
                .GroupBy(x => x.GetType().Name)
                .Select(g => $"{g.Count()}×{g.Key}");
            string monsterStr = monsterCounts != null && monsterCounts.Any()
                ? "  monsters=[" + string.Join(", ", monsterCounts) + "]"
                : "";

            sb.Append($"{nb}: {stName}  canStep={canStep}  canTarget={canTarget}{monsterStr}\n");
        }
        return sb.ToString();
    }

    private static string RenderMap(NightScenario s, Monster focus)
    {
        var others   = s.LivingMonsters().Where(m => m != focus).Select(m => m.TilePosition).ToHashSet();
        var focusPos = focus.TilePosition;

        char Cell((int c, int r) t)
        {
            if (t == focusPos)       return '@';
            if (others.Contains(t))  return '*';
            var st = s.MapManager.GetStructure(t);
            if (st == null || st.IsDestroyed) return '.';
            return st switch
            {
                Tent           => 'T',
                TreeTile       => 't',
                Wall           => 'W',
                WallTile       => 'w',
                Fence          => 'F',
                SniperTower    => 'S',
                AreaTower      => 'A',
                Buff           => 'B',
                Decoy          => 'D',
                Meadow         => 'M',
                PlacableMeadow => 'm',
                VoidTile       => 'V',
                _              => '?',
            };
        }

        var freq = new Dictionary<char, int>();
        var map  = new System.Text.StringBuilder();
        map.AppendLine("--- MAP ---");
        for (int r = 0; r < s.MapManager.Rows; r++)
        {
            for (int c = 0; c < s.MapManager.Columns; c += 2)
            {
                var ch = Cell((c, r));
                freq[ch] = freq.GetValueOrDefault(ch) + 1;
                map.Append(ch); map.Append("   ");
            }
            map.AppendLine();

            if (s.MapManager.Columns > 1)
            {
                map.Append("  ");
                for (int c = 1; c < s.MapManager.Columns; c += 2)
                {
                    var ch = Cell((c, r));
                    freq[ch] = freq.GetValueOrDefault(ch) + 1;
                    map.Append(ch); map.Append("   ");
                }
                map.AppendLine();
            }
        }

        var names = new Dictionary<char, string>
        {
            ['.'] = "empty",
            ['@'] = "this monster",
            ['*'] = "other monster",
            ['T'] = "Tent",       ['t'] = "TreeTile",
            ['W'] = "Wall",       ['w'] = "WallTile",
            ['F'] = "Fence",
            ['S'] = "SniperTower",
            ['A'] = "AreaTower",
            ['B'] = "Buff",
            ['D'] = "Decoy",
            ['M'] = "Meadow",     ['m'] = "PlacableMeadow",
            ['V'] = "VoidTile",
            ['?'] = "Unknown",
        };
        var legend = "Legend:\n" + string.Join("\n", freq
            .OrderByDescending(kv => kv.Value)
            .Select(kv => $"  {kv.Key} = {names.GetValueOrDefault(kv.Key, "??")} ({kv.Value})"));

        return map + legend + "\n";
    }

    private static Monster SpawnGeneratedMonster(
        NightScenario s, Type type, (int, int) tile, float health)
    {
        if      (type == typeof(RegularMonster)) return s.SpawnMonster<RegularMonster>(tile, health);
        else if (type == typeof(BigMonster))     return s.SpawnMonster<BigMonster>(tile, health);
        else if (type == typeof(SmallMonster))   return s.SpawnMonster<SmallMonster>(tile, health);
        else if (type == typeof(FlyingMonster))  return s.SpawnMonster<FlyingMonster>(tile, health);
        throw new ArgumentException($"Unrecognized monster type {type.Name}");
    }


    // ---------------------------------------------------
    // TESTS START HERE
    // ---------------------------------------------------

        [Test]
    public void InvariantsHold_WithRegularMonsters()
    {
        var p = new GeneratorParams
        {
            MonsterCounts = new()
            {
                [typeof(RegularMonster)] = new(1, 20),
            },
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithSmallMonsters()
    {
        var p = new GeneratorParams
        {
            MonsterCounts = new()
            {
                [typeof(SmallMonster)] = new(1, 20),
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithBigMonsters()
    {
        var p = new GeneratorParams
        {
            MonsterCounts = new()
            {
                [typeof(BigMonster)] = new(1, 20),
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithFlyingMonsters()
    {
        var p = new GeneratorParams
        {
            MonsterCounts = new()
            {
                [typeof(FlyingMonster)] = new(1, 20),
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithLargeMap()
    {
        var p = new GeneratorParams
        {
            Repeat  = 30,
            Cols    = 9,
            Rows    = 9,
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithSmallMap()
    {
        var p = new GeneratorParams
        {
            Seed    = 1,
            Repeat  = 200,
            Cols    = 6,
            Rows    = 6,
            MonsterCounts = new()
            {
                [typeof(RegularMonster)] = new(0, 5),
                [typeof(FlyingMonster)]  = new(0, 5),
                [typeof(SmallMonster)]  = new(0, 5),
                [typeof(BigMonster)]     = new(0, 5),
            },
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithTinyMap()
    {
        var p = new GeneratorParams
        {
            Seed    = 1,
            Repeat  = 300,
            Cols    = 4,
            Rows    = 4,
            MonsterCounts = new()
            {
                [typeof(RegularMonster)] = new(0, 3),
                [typeof(FlyingMonster)]  = new(0, 3),
                [typeof(SmallMonster)]  = new(0, 3),
                [typeof(BigMonster)]     = new(0, 3),
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithEmptyMap()
    {
        var p = new GeneratorParams
        {
            Seed    = 1,
            Repeat  = 1,
            Cols    = 7,
            Rows    = 7,
            MonsterCounts = new()
            {
            },
            StructureCounts = new()
            {
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithFewMonsters()
    {
        var p = new GeneratorParams
        {
            MonsterCounts = new()
            {
                [typeof(RegularMonster)] = new(0, 1),
                [typeof(FlyingMonster)]  = new(0, 1),
                [typeof(SmallMonster)]  = new(0, 1),
                [typeof(BigMonster)]     = new(0, 1),
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

    [Test]
    public void InvariantsHold_WithBigMonstersTakingSpace()
    {
        var p = new GeneratorParams
        {
            Repeat = 100,
            Cols = 5,
            Rows = 5,
            MaxStructuresFill = 0.2f,
            PlaceSheepProbability = 0f,
            StructureCounts = new()
            {
                [typeof(VoidTile)]    = new(10, 20),
            },
            MonsterCounts = new()
            {
                [typeof(BigMonster)]     = new(5, 15),
                [typeof(RegularMonster)]     = new(1, 5),
            }
        };

        for (int i = 0; i < p.Repeat; i++)
        {
            var (s, monsters) = GenerateScenario(p.Seed + i, p);
            CheckInvariantsAndOutcome(s, monsters, p.MaxTick, p.Seed + i);
        }
    }

}
