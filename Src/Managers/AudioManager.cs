using System.Collections.Generic; //for the Dict
using Microsoft.Xna.Framework.Audio; //for the SoundEffects class
using Microsoft.Xna.Framework.Content; //for the Content Manager class
using System.IO;
/*
current possible sounds 
AudioManager.Play("coinsplash");
AudioManager.Play("shuffle");
AudioManager.Play("AOE-tower_stomp");
AudioManager.Play("dream_catcher_placement_sound"); 
AudioManager.Play("metal_09");
AudioManager.Play("shoot_01");
AudioManager.Play("slam_02");
AudioManager.Play("tower_breaking");
AudioManager.Play("cute_03");
AudioManager.Play("grunt_03");
AudioManager.Play("qubodupRatAttack");
AudioManager.Play("qubodupRatDeath");
AudioManager.Play("qubodupRatPain");
AudioManager.Play("scream_01");
AudioManager.Play("sheep_baa");

*/
namespace Gamelab.Managers;

public static class AudioManager
{
    private static Dictionary<string, SoundEffect> _sounds = new Dictionary<string, SoundEffect>();


    public static void LoadAllSounds(ContentManager content, string folderPath)
{
    // 1. Get the actual folder path on your computer
    string fullPath = Path.Combine(content.RootDirectory, folderPath);
    
    // 2. Get all file names in that folder
    DirectoryInfo dir = new DirectoryInfo(fullPath);
    if (!dir.Exists) return;

    FileInfo[] files = dir.GetFiles("*.*"); // Look for all files

    foreach (FileInfo file in files)
    {
        // 3. Get the name without the extension (e.g., "shuffle" instead of "shuffle.xnb")
        string name = Path.GetFileNameWithoutExtension(file.Name);
        
        // 4. Create the internal path for MonoGame (e.g., "Sounds/action_cards/shuffle")
        string relativePath = Path.Combine(folderPath, name);

        // 5. Use your existing logic to load it
        if (!_sounds.ContainsKey(name))
        {
            _sounds[name] = content.Load<SoundEffect>(relativePath);
        }
    }
}
    public static void LoadSound(ContentManager content, string name, string path)
    {
        if (!_sounds.ContainsKey(name))
        {
            SoundEffect sound = content.Load<SoundEffect>(path);
            _sounds[name] = sound;
        }
    }
    
    public static void Play(string name)
    {
        _sounds[name].Play();
    }
}