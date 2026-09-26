using System.Collections;
using System.IO;
using Birdsong;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class VisualPreviewTests
{
    [UnityTest, Category("Visual")]
    public IEnumerator CapturePortraitFlow()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Run this visual capture with a graphics device (omit -nographics).");
        SceneManager.LoadScene("MainScene");
        yield return null; yield return null;
        var game = Object.FindObjectOfType<GameController>();
        Assert.NotNull(game);
        yield return Capture(game, "home", 540, 960);
        game.LevelSelect();
        yield return Capture(game, "levels", 540, 960);
        game.StartLevel(0);
        yield return WaitReady(game);
        yield return Capture(game, "gameplay", 540, 960);
        yield return Capture(game, "gameplay-tall", 540, 1200);
        yield return Capture(game, "gameplay-tablet", 768, 1024);
        game.Pause();
        yield return Capture(game, "pause", 540, 960);
        game.Resume();
        game.ui.ShowResult(true);
        yield return Capture(game, "win", 540, 960);
        game.ui.ShowResult(false);
        yield return Capture(game, "lose", 540, 960);
        game.MainMenu();
    }
    static IEnumerator WaitReady(GameController game)
    {
        float timeout = Time.realtimeSinceStartup + 15;
        while (game.board.State != GameState.Ready)
        {
            Assert.Less(Time.realtimeSinceStartup, timeout);
            yield return null;
        }
    }
    static IEnumerator Capture(GameController game, string name, int width, int height)
    {
        var canvas = game.GetComponentInChildren<Canvas>();
        Camera camera = game.gameCamera;
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        yield return null;
        Canvas.ForceUpdateCanvases();
        game.ui.FrameBoard();
        yield return null;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        string folder = Path.Combine(Application.dataPath, "../TestResults/Previews");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.Destroy(image);
        Object.Destroy(target);
        yield return null;
        Canvas.ForceUpdateCanvases();
        game.ui.FrameBoard();
    }
}
