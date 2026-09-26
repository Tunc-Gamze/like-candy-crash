using System.Collections;
using System.Collections.Generic;
using Birdsong;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class GameplayTests
{
    GameController game;
    readonly Dictionary<string, string> saved = new Dictionary<string, string>();
    static readonly string[] Keys = { ProgressStore.Key, ProgressStore.BackupKey, ProgressStore.Key + ".damaged" };

    [UnitySetUp]
    public IEnumerator Setup()
    {
        foreach (string key in Keys)
        {
            saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            PlayerPrefs.DeleteKey(key);
        }
        SceneManager.LoadScene("MainScene");
        yield return null;
        yield return null;
        game = Object.FindObjectOfType<GameController>();
        Assert.NotNull(game);
        Assert.AreEqual(GameState.Menu, game.board.State);
        game.StartLevel(0);
        yield return Stable();
    }
    [UnityTearDown]
    public IEnumerator Teardown()
    {
        Time.timeScale = 1;
        if (game != null)
        {
            game.board.ClearBoard();
            Object.Destroy(game.gameObject);
        }
        yield return null;
        foreach (var entry in saved)
        {
            if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key);
            else PlayerPrefs.SetString(entry.Key, entry.Value);
        }
        PlayerPrefs.Save();
        saved.Clear();
    }
    IEnumerator Stable()
    {
        float deadline = Time.realtimeSinceStartup + 25;
        while (game.board.State != GameState.Ready && game.board.State != GameState.Won && game.board.State != GameState.Lost)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "Board failed to settle: " + game.board.State);
            yield return null;
        }
    }
    Bird BirdAt(Cell cell) => game.board.GetGridBird(cell.X, cell.Y).GetComponent<Bird>();
    bool FindSwap(bool valid, out Cell a, out Cell b)
    {
        var grid = game.board.Snapshot();
        for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
        {
            a = new Cell(x, y);
            foreach (var candidate in new[] { new Cell(x + 1, y), new Cell(x, y + 1) })
                if (MatchRules.Contains(grid, candidate) && MatchRules.IsValidSwap(grid, a, candidate) == valid)
                { b = candidate; return true; }
        }
        a = b = default;
        return false;
    }
    [UnityTest]
    public IEnumerator SoundEffectsAreAssignedNonSilentAndRespectToggle()
    {
        GameAudio audio = game.audioPlayer;
        var source = audio.GetComponent<AudioSource>();
        var clips = new[] { audio.select, audio.swap, audio.match, audio.invalid,
            audio.cascade, audio.win, audio.lose, audio.button };
        foreach (var clip in clips)
        {
            Assert.NotNull(clip, "MainScene has an unassigned sound effect.");
            Assert.Greater(clip.length, 0.05f);
            var samples = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(samples, 0));
            float peak = 0;
            foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.Greater(peak, 0.01f, clip.name + " is silent.");
            Assert.Less(peak, 0.95f, clip.name + " clips.");
        }
        Assert.IsTrue(game.Progress.Data.sound);
        audio.Play(SoundCue.Win);
        Assert.IsTrue(source.isPlaying);
        game.ToggleSound();
        Assert.IsTrue(source.mute);
        Assert.IsFalse(source.isPlaying, "OFF must stop an effect already playing.");
        Assert.IsFalse(new ProgressStore(new PlayerPreferences()).Data.sound);
        audio.Play(SoundCue.Match);
        Assert.IsFalse(source.isPlaying, "OFF must reject new effects.");
        game.ToggleSound();
        Assert.IsFalse(source.mute);
        Assert.IsTrue(new ProgressStore(new PlayerPreferences()).Data.sound);
        audio.Play(SoundCue.Win);
        Assert.IsTrue(source.isPlaying, "ON must allow effects again.");
        source.Stop();
        AudioClip original = audio.match;
        audio.match = null;
        Assert.DoesNotThrow(() => audio.Play(SoundCue.Match));
        Assert.IsFalse(source.isPlaying);
        audio.match = original;
        yield return null;
    }
    [UnityTest]
    public IEnumerator RealPlayerPrefsRoundTripPreservesProgressAndPreference()
    {
        game.Progress.Record(0, 1500, true);
        game.Progress.SetSound(false);
        var reloaded = new ProgressStore(new PlayerPreferences());
        Assert.IsTrue(reloaded.Data.completed[0]);
        Assert.AreEqual(2, reloaded.Data.highestUnlocked);
        Assert.AreEqual(1500, reloaded.Data.bestScores[0]);
        Assert.IsFalse(reloaded.Data.sound);
        yield return null;
    }
    [UnityTest]
    public IEnumerator SharedMouseAndTouchPointerPathSwapsBySwipeAndTap()
    {
        Assert.IsTrue(FindSwap(false, out var a, out var b));
        Physics2D.SyncTransforms();
        Vector2 screenA = game.gameCamera.WorldToScreenPoint(game.board.Position(a.X, a.Y));
        Vector2 screenB = game.gameCamera.WorldToScreenPoint(game.board.Position(b.X, b.Y));
        game.input.PointerDown(screenA);
        Assert.AreSame(BirdAt(a), game.input.GetSelectedBird());
        game.input.PointerUp(screenB);
        Assert.AreEqual(GameState.Swapping, game.board.State);
        game.input.PointerDown(screenA);
        Assert.IsNull(game.input.GetSelectedBird());
        yield return Stable();
        Assert.AreEqual(20, game.Session.Moves);
        Assert.IsTrue(FindSwap(true, out a, out b));
        Physics2D.SyncTransforms();
        screenA = game.gameCamera.WorldToScreenPoint(game.board.Position(a.X, a.Y));
        screenB = game.gameCamera.WorldToScreenPoint(game.board.Position(b.X, b.Y));
        game.input.PointerDown(screenA);
        game.input.PointerUp(screenA);
        Assert.NotNull(game.input.GetSelectedBird());
        game.input.PointerDown(screenB);
        yield return Stable();
        Assert.AreEqual(19, game.Session.Moves);
        game.Pause();
        game.input.PointerDown(screenA);
        Assert.IsNull(game.input.GetSelectedBird());
    }
    [UnityTest]
    public IEnumerator InvalidSwapReturnsSameBirdsWithoutSpendingMove()
    {
        Assert.IsTrue(FindSwap(false, out var a, out var b));
        var first = BirdAt(a); var second = BirdAt(b);
        int moves = game.Session.Moves;
        Assert.IsTrue(game.board.RequestSwap(first, second));
        Assert.IsFalse(game.board.CanInteract);
        Assert.IsFalse(game.board.RequestSwap(first, second));
        yield return Stable();
        Assert.AreSame(first, BirdAt(a)); Assert.AreSame(second, BirdAt(b));
        Assert.AreEqual(game.board.Position(a.X, a.Y), first.transform.position);
        Assert.AreEqual(moves, game.Session.Moves);
        Assert.AreEqual(0, game.Session.Score);
    }
    [UnityTest]
    public IEnumerator ValidSwapSpendsOneMoveRefillsAndResolvesEveryCascade()
    {
        Assert.IsTrue(FindSwap(true, out var a, out var b));
        int moves = game.Session.Moves;
        var removed = new List<Bird>();
        var probe = game.board.Snapshot();
        MatchRules.Swap(probe, a, b);
        foreach (var group in MatchRules.FindMatches(probe)) foreach (var cell in group)
        {
            var original = cell.Equals(a) ? b : cell.Equals(b) ? a : cell;
            removed.Add(BirdAt(original));
        }
        Assert.IsTrue(game.board.RequestSwap(BirdAt(a), BirdAt(b)));
        yield return Stable();
        Assert.AreEqual(moves - 1, game.Session.Moves);
        Assert.GreaterOrEqual(game.Session.Score, 100);
        foreach (var bird in removed) Assert.IsTrue(bird == null, "Matched bird survived.");
        Assert.IsEmpty(MatchRules.FindMatches(game.board.Snapshot()));
        Assert.IsTrue(MatchRules.HasMove(game.board.Snapshot()));
        for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
        {
            Bird bird = BirdAt(new Cell(x, y));
            Assert.AreEqual(x, bird.xIndex); Assert.AreEqual(y, bird.yIndex);
            Assert.AreEqual(game.board.Position(x, y), bird.transform.position);
            Assert.IsFalse(bird.isMoving);
        }
    }
    [UnityTest]
    public IEnumerator PauseAndRestartDuringSwapCancelsOldResolution()
    {
        Assert.IsTrue(FindSwap(true, out var a, out var b));
        game.board.RequestSwap(BirdAt(a), BirdAt(b));
        game.Pause();
        Assert.AreEqual(GameState.Paused, game.board.State);
        Assert.IsFalse(game.board.CanInteract);
        Assert.AreEqual(0, Time.timeScale);
        yield return null;
        game.Restart();
        yield return Stable();
        Assert.AreEqual(20, game.Session.Moves);
        Assert.AreEqual(0, game.Session.Score);
        Assert.AreEqual(1, Time.timeScale);
        Assert.AreEqual(36, game.board.GetComponentsInChildren<Bird>().Length);
    }
    [UnityTest]
    public IEnumerator PauseResumeKeepsResolutionAndLevelSelectCancelsIt()
    {
        Assert.IsTrue(FindSwap(true, out var a, out var b));
        game.board.RequestSwap(BirdAt(a), BirdAt(b));
        game.Pause(); yield return null;
        game.Resume();
        yield return Stable();
        Assert.AreEqual(19, game.Session.Moves);
        game.Restart();
        game.LevelSelect();
        yield return null;
        Assert.AreEqual(GameState.Menu, game.board.State);
        Assert.AreEqual(0, game.board.GetComponentsInChildren<Bird>().Length);
        Assert.IsNull(game.Session);
    }
    [UnityTest]
    public IEnumerator AllSixLevelsInitializeAndObjectivesHaveAvailableBirds()
    {
        for (int i = 0; i < 6; i++)
        {
            if (i > 0) game.Progress.Record(i - 1, 0, true);
            game.StartLevel(i);
            yield return Stable();
            Assert.AreEqual(game.Campaign.levels[i].moves, game.Session.Moves);
            Assert.IsEmpty(MatchRules.FindMatches(game.board.Snapshot()));
            Assert.IsTrue(MatchRules.HasMove(game.board.Snapshot()));
            foreach (var goal in game.Session.Level.objectives)
                if (goal.kind == ObjectiveKind.CollectColor) Assert.Contains(goal.color, new List<int>(game.board.AvailableColors()));
        }
    }
    [UnityTest]
    public IEnumerator LastMoveWinUnlocksNextAndLossDoesNot()
    {
        game.Session.Level.objectives = new[] { new ObjectiveDefinition { kind = ObjectiveKind.Score, label = "Score", target = 100 } };
        while (game.Session.Moves > 1) game.Session.ConsumeMove();
        Assert.IsTrue(FindSwap(true, out var a, out var b));
        game.board.RequestSwap(BirdAt(a), BirdAt(b));
        yield return Stable();
        Assert.AreEqual(GameState.Won, game.board.State);
        Assert.IsFalse(game.board.CanInteract);
        Assert.AreEqual(2, game.Progress.Data.highestUnlocked);
        game.NextLevel(); yield return Stable();
        game.Session.Level.objectives = new[] { new ObjectiveDefinition { kind = ObjectiveKind.Score, label = "Score", target = int.MaxValue } };
        while (game.Session.Moves > 1) game.Session.ConsumeMove();
        Assert.IsTrue(FindSwap(true, out a, out b));
        game.board.RequestSwap(BirdAt(a), BirdAt(b));
        yield return Stable();
        Assert.AreEqual(GameState.Lost, game.board.State);
        Assert.AreEqual(2, game.Progress.Data.highestUnlocked);
    }
    [UnityTest]
    public IEnumerator FinalLevelWinShowsFinaleAndNoNextButton()
    {
        for (int i = 0; i < 5; i++) game.Progress.Record(i, 1000, true);
        game.StartLevel(5); yield return Stable();
        foreach (var goal in game.Session.Level.objectives)
        { goal.kind = ObjectiveKind.Score; goal.label = "Score"; goal.target = 100; }
        Assert.IsTrue(FindSwap(true, out var a, out var b));
        game.board.RequestSwap(BirdAt(a), BirdAt(b)); yield return Stable();
        Assert.AreEqual(GameState.Won, game.board.State);
        Assert.AreEqual(6, game.Progress.Data.highestUnlocked);
        foreach (Button button in game.ui.GetComponentsInChildren<Button>()) Assert.AreNotEqual("Next", button.name);
        game.NextLevel();
        Assert.AreEqual(5, game.LevelIndex);
        Assert.AreEqual(GameState.Won, game.board.State);
    }
}
