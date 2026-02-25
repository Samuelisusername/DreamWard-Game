using System;
using System.IO;

namespace Tests;

public static class Utils
{
    public static string OutputDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Output");
}