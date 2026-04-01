// using System;
// using System.Collections.Generic;
// using System.Globalization;
// using System.Linq;
// using System.Numerics;
// using System.Security.Cryptography;

// public class MonsterGroupContainer // one MonsterGroupContainer per tile
// {   
//     public List<MonsterGroup> _Monstergroups; //each monstergroup has its own direction
//     public bool NoMonsters => (this.numberofgroups ==0);
//     public int numberofgroups =>  this._Monstergroups.Count;
//     public int numberOfMonsters() => _Monstergroups.Sum(m => m.size); // if this is 1 then only draw single monster
//     public int how_many_monsters_of_that_type(MonsterType monstertype)
//     {   
//         int nr_monsters_of_specific_type = 0;
//         foreach (var monstergroup in _Monstergroups)
//         {
//             if(monstergroup.monsterType == monstertype) nr_monsters_of_specific_type += monstergroup.size;
//         }
//         return nr_monsters_of_specific_type;
//     }
//     public bool SingleMonster(MonsterType monstertype) => (how_many_monsters_of_that_type(monstertype)==1);
//     public (int,int) position_on_tile(MonsterType MonsterGrouptype) // 1, 1 means in middle; 2, 1 means there are two groups on this tile and this monstertype group is on pos 1.
//     {
//         //returns position on the hexagon that the monstergroup of type type will be positioned in (we dont want the monstegroups to overlap visually)
//         int x1 = this.numberofgroups;
//         int x2 = -1;
//         var sorted_grouped_monstergroups = _Monstergroups.GroupBy(m => m.monsterType).OrderBy(bucket => bucket.Key);
//         int curr_rank = 0; 
//         foreach (var group in sorted_grouped_monstergroups)
//         {
//             if(group.Key == MonsterGrouptype)
//             {
//                 x2 = curr_rank;
//             }
//             curr_rank++;
//         }
//         return (x1, x2);
//     }

//     public void addMonsterGroup(){}
//     public void removeMonsterGroup(){}
// }