using System;
using System.Text.Json.Serialization;

namespace Gamelab.Utils.Logging;

public class Logger
{
    [JsonConverter(typeof(JsonStringEnumConverter<Level>))]
    public enum Level {
        Debug,
        Info,
        Warning,
        Error
    }


    public readonly string name;

    public Logger(string name)
    {
        this.name = name;
    }

    public void Log(Level level, string message)
    {
        if (GamelabGame.Instance.IsRelease && level < Level.Info)
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] [{name}] [{level}] {message}");
    }

    public void Debug(string message) => Log(Level.Debug, message);
    public void Info(string message) => Log(Level.Info, message);
    public void Warning(string message) => Log(Level.Warning, message);
    public void Error(string message) => Log(Level.Error, message);

    public void Exception(string message, Exception ex)
    {
        Error($"{message}: {ex}");
    }
}