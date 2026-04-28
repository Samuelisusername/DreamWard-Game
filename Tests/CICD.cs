/*
###########################
# DO NOT MODIFY THIS FILE #
###########################
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Gamelab;
using Gamelab.Screens;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;
using static Gamelab.GamelabGame;

namespace Tests;

public class CICD
{
#pragma warning disable NUnit1032 // game is disposed on the game thread, not in the OneTimeTearDown method
    private GamelabGame game;
#pragma warning restore NUnit1032
    private Thread gameThread;
    private Exception gameException;
    private readonly Dictionary<string, double> metrics = [];

    private bool isFirstTest = true;

    [OneTimeSetUp]
    public void Setup()
    {
        metrics.Clear();
        isFirstTest = true;

        game = new GamelabGame(RunMode.Release);

        gameThread = new Thread(() =>
        {
            try
            {
                game.Run();
            }
            catch (Exception ex)
            {
                gameException = ex;
            }

            game?.Dispose();
        })
        {
            IsBackground = true,
        };
        gameThread.Start();
    }

    [Test, NonParallelizable, Order(1)]
    public void TestGameStartup()
    {
        WaitForGameToStart();
        CheckGraphics();
    }

    [Test, TestCaseSource(typeof(GamelabGameScreen), nameof(GamelabGameScreen.GetScreenFactories)), NonParallelizable, Order(2)]
    public void TestScreen(GamelabGameScreen.Factory screenFactory)
    {
        WaitForGameToStart();
        CheckGraphics();

        var screen = screenFactory.Instantiate(game);

        game.SwitchToScreen(screen);
        AssertTimeout(() => screen.IsActive, 5, $"Switching to screen \"{screen}\"");

        AssertPerformance(screen);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        var outputDir = Utils.OutputDir;
        Directory.CreateDirectory(outputDir);

        var metricsPath = Path.Combine(outputDir, "Metrics.txt");
        var metricsFileContent = string.Join(Environment.NewLine, metrics.Select(kvp =>
        {
            var metricName = kvp.Key.Replace("[^a-zA-Z0-9]", "_").ToLowerInvariant();
            var metricValue = kvp.Value.ToString("F3", CultureInfo.InvariantCulture);
            return $"{metricName} {metricValue}";
        }));
        File.WriteAllText(metricsPath, metricsFileContent);

        game?.Exit();
        gameThread?.Join();
    }

    private void WaitForGameToStart()
    {
        if (isFirstTest)
        {
            AssertTimeout(() => game.IsRunning, 5, "Waiting for game to start");

            isFirstTest = false;

            Thread.Sleep(1000);
        }

        Assert.That(game.IsRunning, "Game should be running");
    }

    private void CheckGraphics()
    {
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var presentationParameters = game.GraphicsDevice.PresentationParameters;
        Assert.Multiple(() =>
        {
            Assert.That(game.graphics.IsFullScreen, "Game should render in fullscreen mode");
            Assert.That(presentationParameters.BackBufferWidth, Is.EqualTo(displayMode.Width), "Back buffer width should match display width");
            Assert.That(presentationParameters.BackBufferHeight, Is.EqualTo(displayMode.Height), "Back buffer height should match display height");
        });
    }

    private void AssertNoGameExceptions()
    {
        Thread.Sleep(100); // Mitigate potential race condition

        if (gameException != null)
        {
            TestContext.Out.WriteLine($"Game threw an exception: {gameException}\n{gameException.StackTrace}");
        }

        Assert.That(gameException, Is.Null, gameException?.ToString());
    }

    private void AssertThatAndCheckForGameExceptions<TActual>(TActual actual, IResolveConstraint expression, NUnitString message = default)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual, expression, message);
            AssertNoGameExceptions();
        });
    }

    private double AssertTimeout(Func<bool> condition, double timeoutSeconds, string message)
    {
        var startTime = DateTime.Now;
        var elapsedSeconds = 0.0;
        while (!condition())
        {
            elapsedSeconds = (DateTime.Now - startTime).TotalSeconds;
            AssertThatAndCheckForGameExceptions(elapsedSeconds, Is.LessThan(timeoutSeconds), $"{message} exceeded timeout of {elapsedSeconds:F2} seconds");
            Thread.Yield();
        }

        TestContext.Out.WriteLine($"{message} completed in {elapsedSeconds:F2} seconds");

        return elapsedSeconds;
    }

    private void SaveScreenshot(string filename)
    {
        var path = Path.GetFullPath(Path.Combine(Utils.OutputDir, "Screenshots", $"{filename}.png"));
        game.SaveScreenshot(path);
        AssertTimeout(() => File.Exists(path), 2, "Saving screenshot");
    }

    private void AssertPerformance(GamelabGameScreen screen, double loadTimeout = 3, double measuringTime = 5, double minFps = 30)
    {
        var name = screen.GetType().Name;
        TestContext.Out.WriteLine($"Measuring performance of {name}...");

        var loadTime = AssertTimeout(() => screen.NumFramesDrawn > 0, loadTimeout, $"Drawing first frame for screen \"{name}\"");
        metrics[$"screen_load_time_{name}"] = loadTime;

        SaveScreenshot($"{name}_0");

        var numFramesToDraw = (int)(measuringTime * minFps);
        var elapsedTime = AssertTimeout(() => screen.NumFramesDrawn >= numFramesToDraw, measuringTime * 2, $"Drawing {numFramesToDraw} frames for screen \"{name}\"");

        SaveScreenshot($"{name}_1");

        var actualFps = screen.NumFramesDrawn / elapsedTime;
        metrics[$"screen_fps_{name}"] = actualFps;
        TestContext.Out.WriteLine($"{name} drew {numFramesToDraw} frames within {elapsedTime:F2} seconds ({actualFps:F2} FPS)");
        AssertThatAndCheckForGameExceptions(actualFps, Is.GreaterThanOrEqualTo(minFps), $"Screen \"{name}\" rendered at less than {minFps} FPS");
    }
}