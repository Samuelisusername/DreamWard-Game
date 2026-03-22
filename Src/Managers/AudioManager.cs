using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

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
AudioManager.Play("dragonflap");
*/

namespace Gamelab.Managers;

public static class AudioManager
{
    private static Dictionary<string, SoundEffect> _sounds = new Dictionary<string, SoundEffect>();

    // ---------------------------------------------------
    // Loads an entire folder of sounds automatically
    // ---------------------------------------------------
    public static void LoadAllSounds(ContentManager content, string folderPath)
    {
        try
        {
            // AppContext.BaseDirectory finds the actual bin/Debug folder where your compiled game is running
            string physicalPath = Path.Combine(AppContext.BaseDirectory, content.RootDirectory, folderPath);

            // Check if the directory even exists
            if (!Directory.Exists(physicalPath))
            {
                Console.WriteLine($"[Audio Warning] Could not find folder: {physicalPath}");
                return;
            }

            // Look ONLY for compiled MonoGame .xnb files (ignores hidden Mac files like .DS_Store)
            string[] files = Directory.GetFiles(physicalPath, "*.xnb");

            if (files.Length == 0)
            {
                Console.WriteLine($"[Audio Warning] Folder found, but no .xnb files inside: {physicalPath}");
                return;
            }

            foreach (string file in files)
            {
                // Extract just the name (e.g. "shuffle")
                string name = Path.GetFileNameWithoutExtension(file);

                // Create the MonoGame asset path (e.g. "Sounds/action_cards/shuffle")
                // Using forward slash explicitly to be safe across Mac/Windows
                string assetPath = $"{folderPath}/{name}";

                if (!_sounds.ContainsKey(name))
                {
                    _sounds[name] = content.Load<SoundEffect>(assetPath);
                    Console.WriteLine($"[Audio Success] Loaded: {name} from {assetPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio Error] Failed to load sounds from {folderPath}. Reason: {ex.Message}");
        }
    }

    // ---------------------------------------------------
    // Loads a single sound manually
    // ---------------------------------------------------
    public static void LoadSound(ContentManager content, string name, string path)
    {
        try 
        {
            if (!_sounds.ContainsKey(name))
            {
                SoundEffect sound = content.Load<SoundEffect>(path);
                _sounds[name] = sound;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audio Error] Failed to load individual sound '{name}' at '{path}'. Reason: {ex.Message}");
        }
    }
    
    // ---------------------------------------------------
    // Plays a loaded sound
    // ---------------------------------------------------
    public static void Play(string name)
    {
        if (_sounds.ContainsKey(name)){
            float volume = GamelabGame.Instance.MasterVolume;
            _sounds[name].Play(volume, 0f, 0f);
        }
        else{
            Console.WriteLine($"[Audio Warning] Tried to play '{name}', but it hasn't been loaded!");
        }
    }
}