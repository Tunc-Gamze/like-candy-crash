using System;
using System.Collections.Generic;
using Birdsong;
using NUnit.Framework;
using UnityEngine;

public sealed class RulesTests
{
    [Test]
    public void ThousandInitialBoardsAreFullStableAndPlayable()
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var grid = MatchRules.CreateBoard(6, 6, 5, new System.Random(seed));
            Assert.That(MatchRules.FindMatches(grid), Is.Empty, "Seed " + seed);
            Assert.That(MatchRules.HasMove(grid), Is.True, "Seed " + seed);
            foreach (int value in grid) Assert.That(value, Is.InRange(0, 4));
        }
    }
    [Test]
    public void ConstructiveFallbackTerminatesAndAlwaysHasAMove()
    {
        for (int width = 3; width <= 12; width++) for (int height = 3; height <= 12; height++)
        {
            var grid = MatchRules.CreateBoard(width, height, 3, new System.Random(1), 0);
            Assert.That(MatchRules.FindMatches(grid), Is.Empty);
            Assert.That(MatchRules.IsValidSwap(grid, new Cell(1, 0), new Cell(1, 1)), Is.True);
        }
    }
    [Test]
    public void SwapProbeDoesNotMutateBoard()
    {
        var grid = MatchRules.CreateBoard(6, 6, 5, new System.Random(40));
        var before = (int[,])grid.Clone();
        for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
            MatchRules.IsValidSwap(grid, new Cell(x, y), new Cell(x + 1, y));
        CollectionAssert.AreEqual(before, grid);
        Assert.IsFalse(MatchRules.IsValidSwap(grid, new Cell(0, 0), new Cell(1, 1)));
    }
    [Test]
    public void CrossingLinesCountFiveDistinctBirds()
    {
        int[,] grid = { { 1, 0, 2 }, { 0, 0, 0 }, { 2, 0, 1 } };
        var groups = MatchRules.FindMatches(grid);
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual(5, groups[0].Count);
    }
    [Test]
    public void SeparateMatchesScoreSeparately()
    {
        int[,] grid = { { 0, 1, 2 }, { 0, 1, 2 }, { 0, 1, 2 } };
        var groups = MatchRules.FindMatches(grid);
        Assert.AreEqual(3, groups.Count);
        foreach (var group in groups) Assert.AreEqual(3, group.Count);
    }
    [Test]
    public void CollapsePreservesOrderAndEmptiesTop()
    {
        int[,] grid = { { -1, 2, -1, 3, 4, -1 }, { -1, -1, -1, -1, -1, -1 } };
        MatchRules.Collapse(grid);
        CollectionAssert.AreEqual(new[,] { { 2, 3, 4, -1, -1, -1 }, { -1, -1, -1, -1, -1, -1 } }, grid);
    }
    [Test]
    public void DeadBoardReshufflesWithoutImmediateMatch()
    {
        var grid = new int[6, 6];
        for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++) grid[x, y] = (x + y) % 3;
        Assert.IsFalse(MatchRules.HasMove(grid));
        var shuffled = MatchRules.Reshuffle(grid, 5, new System.Random(6));
        Assert.IsTrue(MatchRules.HasMove(shuffled));
        Assert.IsEmpty(MatchRules.FindMatches(shuffled));
    }
    [Test]
    public void DegenerateShuffleUsesBoundedFallback()
    {
        var grid = MatchRules.Reshuffle(new int[6, 6], 3, new System.Random(7));
        Assert.IsTrue(MatchRules.HasMove(grid));
        Assert.IsEmpty(MatchRules.FindMatches(grid));
    }
    [TestCase(3, 1, 100)] [TestCase(4, 1, 200)] [TestCase(5, 1, 300)]
    [TestCase(6, 1, 400)] [TestCase(3, 3, 300)] [TestCase(2, 1, 0)]
    public void ScoreConfigurationAndCascadeMultiplier(int size, int cascade, int expected)
        => Assert.AreEqual(expected, new Scoring().Points(size, cascade));

    [Test]
    public void CombinedObjectivesRequireEveryTargetAndLastMoveCanWin()
    {
        var level = new LevelDefinition { moves = 1, objectives = new[] {
            new ObjectiveDefinition { kind = ObjectiveKind.Score, target = 200 },
            new ObjectiveDefinition { kind = ObjectiveKind.CollectColor, target = 6, color = 2 } } };
        var session = new LevelSession(level, new Scoring());
        Assert.IsTrue(session.ConsumeMove());
        Assert.IsFalse(session.ConsumeMove());
        session.Award(new[] { 3 }, new Dictionary<int, int> { { 2, 3 } }, 1);
        Assert.IsFalse(session.Won); Assert.IsTrue(session.Lost);
        session.Award(new[] { 3 }, new Dictionary<int, int> { { 2, 3 } }, 2);
        Assert.IsTrue(session.Won); Assert.IsFalse(session.Lost);
        Assert.AreEqual(300, session.Score);
        Assert.AreEqual(6, session.Collected(2));
        Assert.AreEqual(0, session.Moves);
    }
    [Test]
    public void SixCampaignLevelsAreValid()
    {
        var asset = Resources.Load<TextAsset>("Campaign");
        Assert.NotNull(asset);
        var campaign = JsonUtility.FromJson<Campaign>(asset.text);
        Assert.IsNull(campaign.Validate(new HashSet<int> { 0, 1, 2, 5, 7 }));
        Assert.AreEqual(6, campaign.levels.Length);
        Assert.NotNull(campaign.Validate(new HashSet<int> { 0 }));
    }

    sealed class MemoryPreferences : IPreferences
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public string Get(string key) => values.TryGetValue(key, out string value) ? value : "";
        public void Set(string key, string value) => values[key] = value;
        public void Delete(string key) => values.Remove(key);
        public void Flush() { }
    }
    [Test]
    public void SaveRoundTripUnlocksOnlyOnWinAndKeepsBestAndSound()
    {
        var memory = new MemoryPreferences();
        var store = new ProgressStore(memory);
        store.Record(0, 500, false);
        Assert.AreEqual(1, store.Data.highestUnlocked);
        store.Record(0, 1200, true);
        store.Record(0, 100, true);
        store.SetSound(false);
        var loaded = new ProgressStore(memory);
        Assert.AreEqual(2, loaded.Data.highestUnlocked);
        Assert.AreEqual(1200, loaded.Data.bestScores[0]);
        Assert.IsTrue(loaded.Data.completed[0]);
        Assert.IsFalse(loaded.Data.sound);
    }
    [Test]
    public void CampaignCompletionNeverUnlocksSeven()
    {
        var store = new ProgressStore(new MemoryPreferences());
        for (int i = 0; i < 6; i++) store.Record(i, 1000, true);
        Assert.AreEqual(6, store.Data.highestUnlocked);
        CollectionAssert.AreEqual(new[] { true, true, true, true, true, true }, store.Data.completed);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Record(6, 100, true));
    }
    [Test]
    public void CorruptSaveIsPreservedAndNotOverwritten()
    {
        var memory = new MemoryPreferences();
        memory.Set(ProgressStore.Key, "broken");
        var store = new ProgressStore(memory);
        Assert.IsTrue(store.ReadOnly);
        store.Record(0, 1000, true);
        Assert.AreEqual("broken", memory.Get(ProgressStore.Key));
        memory.Set("other-game", "keep");
        store.ResetProgress();
        Assert.AreEqual("keep", memory.Get("other-game"));
        Assert.IsFalse(store.ReadOnly);
        Assert.AreEqual(1, new ProgressStore(memory).Data.highestUnlocked);
    }
    [Test]
    public void CorruptPrimaryRecoversBackup()
    {
        var memory = new MemoryPreferences();
        var store = new ProgressStore(memory);
        store.Record(0, 1000, true);
        store.SetSound(false);
        memory.Set(ProgressStore.Key, "broken");
        var recovered = new ProgressStore(memory);
        Assert.AreEqual(2, recovered.Data.highestUnlocked);
        Assert.IsFalse(recovered.ReadOnly);
        Assert.AreEqual("broken", memory.Get(ProgressStore.Key + ".damaged"));
    }
    [TestCase("{}")]
    [TestCase("{\"version\":9}")]
    public void InvalidSaveSchemaIsRejected(string json) => Assert.IsNull(ProgressStore.Parse(json));

    [Test]
    public void MissingPrimaryRecoversValidBackup()
    {
        var memory = new MemoryPreferences();
        var store = new ProgressStore(memory);
        store.Record(0, 1000, true);
        memory.Delete(ProgressStore.Key);
        var recovered = new ProgressStore(memory);
        Assert.AreEqual(2, recovered.Data.highestUnlocked);
        Assert.IsFalse(recovered.ReadOnly);
    }
    [Test]
    public void NewerSaveVersionIsNeverOverwrittenByOldBackup()
    {
        var memory = new MemoryPreferences();
        memory.Set(ProgressStore.Key, JsonUtility.ToJson(new ProgressData { version = 2 }));
        memory.Set(ProgressStore.BackupKey, JsonUtility.ToJson(new ProgressData()));
        var store = new ProgressStore(memory);
        Assert.IsTrue(store.ReadOnly);
        Assert.IsFalse(store.Save());
        Assert.AreEqual(2, JsonUtility.FromJson<ProgressData>(memory.Get(ProgressStore.Key)).version);
    }
}
