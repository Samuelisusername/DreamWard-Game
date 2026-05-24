using Gamelab.Managers;
using Microsoft.Xna.Framework;

/// <summary>
///  Inflicts Damage on Ground Monsters
/// </summary>
public class Spike : Tower
{

    public Spike((int,int) pos) : base("spikes_idle", pos){}
    // protected override float damagePerSecond => 2f;

    // protected override int rangeDepth => 0;
    public override (string, string)[] Infos => [("Health:", $"Yes"), ("DPS:", $"{damagePerSecond}"), ("Total Damage Dealt:", $"{(int)TotalDamage}")];

    public override void Attack(GameTime gameTime)
    {

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var groupsInRage = mapManager.GetMonsterGroupsInRange(TilePosition, rangeDepth);
        bool dealtDamageThisFrame = false;

        foreach (MonsterGroup group in groupsInRage)
        {
            if(group.MonsterType != MonsterType.Flying)
            {
                group.TakeDamageAOE(damagePerSecond * dt);
                TotalDamage += (damagePerSecond * dt) * group.Count;
                dealtDamageThisFrame = true;
            }
        }
        
        if (dealtDamageThisFrame)
        {
            soundTimer -= dt;

            if (soundTimer <= 0f)
            {
                string soundToPlay = "AOE-tower_stomp"; //TODO: change this to spike sound 
                float cooldown = 1.5f; 

                AudioManager.Play(soundToPlay);
                soundTimer = cooldown; 
            }
        }
    }
}