using System;
using System.IO;
using Newtonsoft.Json;

namespace Gamelab.Utils;

public class JsonLoader{
    private readonly GamelabGame game;
    private readonly string basePath;
    private readonly JsonSerializer serializer = new();

    public JsonLoader(GamelabGame game, string basePath)
    {
        this.game = game;
        this.basePath = basePath;
    }

    public T LoadJson<T>(string path)
    {
        string jsonPath = GetFullPath(path);
        return LoadJSONFromPath<T>(jsonPath);
    }

    private T LoadJSONFromPath<T>(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"JSON file not found at path: {fullPath}");
        }

        using var fs = File.Open(fullPath, FileMode.Open, FileAccess.Read);
        using var sr = new StreamReader(fs);
        using var reader = new JsonTextReader(sr);

        T obj;
        try {
            obj = serializer.Deserialize<T>(reader)!;
        } catch (Exception e) {
            throw new InvalidDataException($"Failed to deserialize JSON file at path: {fullPath}", e);
        }

        return obj;
    }

    public void SaveJson<T>(string path, T obj)
    {
        string jsonPath = GetFullPath(path);

        using var fs = File.Open(jsonPath, FileMode.Create, FileAccess.Write);
        using var sw = new StreamWriter(fs);
        using var writer = new JsonTextWriter(sw)
        {
            Formatting = Formatting.Indented,
        };
        serializer.Serialize(writer, obj);
    }

    public bool Exists(string path) => File.Exists(GetFullPath(path));

    private string GetFullPath(string path) {
        var contentDir = game.uncompiledContentDir ?? game.contentDir;
        return Path.Combine(contentDir, basePath, path);
    }
}