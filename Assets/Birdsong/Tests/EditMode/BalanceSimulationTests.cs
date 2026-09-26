using System;
using System.Collections.Generic;
using System.Text;
using Birdsong;
using NUnit.Framework;
using UnityEngine;

public sealed class BalanceSimulationTests
{
    // A deterministic, one-ply greedy player. This measures feasibility, not human difficulty.
    [TestCase(false)]
    [TestCase(true)]
    public void SimulateSixLevelsWithSeededLegalMoves(bool greedy)
    {
        var campaign = JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Campaign").text);
        int[] colorIds = { 0, 1, 2, 5, 7 };
        var report = new StringBuilder("Seeded balance simulation (100 games per level, greedy=" + greedy + "):\n");
        for (int level = 0; level < 6; level++)
        {
                int wins = 0, totalScore = 0, movesUsed = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var random = new System.Random(level * 10000 + seed);
                var grid = MatchRules.CreateBoard(6, 6, 5, random);
                var session = new LevelSession(campaign.levels[level], campaign.scoring);
                while (!session.Won && !session.Lost)
                {
                    if (!MatchRules.HasMove(grid)) grid = MatchRules.Reshuffle(grid, 5, random);
                    Cell bestA = default, bestB = default;
                    int bestValue = -1;
                    for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
                    {
                        var a = new Cell(x, y);
                        foreach (var b in new[] { new Cell(x + 1, y), new Cell(x, y + 1) })
                        {
                            if (!MatchRules.IsValidSwap(grid, a, b)) continue;
                            MatchRules.Swap(grid, a, b);
                            int value = 0;
                            foreach (var group in MatchRules.FindMatches(grid))
                            {
                                value += campaign.scoring.Points(group.Count, 1);
                                foreach (var cell in group) foreach (var goal in session.Level.objectives)
                                    if (goal.kind == ObjectiveKind.CollectColor && goal.color == colorIds[grid[cell.X, cell.Y]] &&
                                        session.Progress(goal) < goal.target) value += 100;
                            }
                            MatchRules.Swap(grid, a, b);
                            if (!greedy) value = random.Next(100000);
                            if (value > bestValue) { bestValue = value; bestA = a; bestB = b; }
                        }
                    }
                    Assert.GreaterOrEqual(bestValue, 0);
                    MatchRules.Swap(grid, bestA, bestB);
                    session.ConsumeMove();
                    for (int cascade = 1; cascade <= 100; cascade++)
                    {
                        var matches = MatchRules.FindMatches(grid);
                        if (matches.Count == 0) break;
                        var sizes = new List<int>();
                        var colors = new Dictionary<int, int>();
                        foreach (var group in matches)
                        {
                            sizes.Add(group.Count);
                            foreach (var cell in group)
                            {
                                int color = colorIds[grid[cell.X, cell.Y]];
                                colors.TryGetValue(color, out int count);
                                colors[color] = count + 1;
                                grid[cell.X, cell.Y] = -1;
                            }
                        }
                        session.Award(sizes, colors, cascade);
                        MatchRules.Collapse(grid);
                        for (int x = 0; x < 6; x++) for (int y = 0; y < 6; y++)
                            if (grid[x, y] < 0) grid[x, y] = random.Next(5);
                        if (cascade == 100) grid = MatchRules.Reshuffle(grid, 5, random);
                    }
                }
                if (session.Won) wins++;
                totalScore += session.Score;
                movesUsed += session.Level.moves - session.Moves;
            }
            report.AppendLine("Level " + (level + 1) + ": wins=" + wins + "/100, mean score=" + totalScore / 100 + ", mean moves used=" + movesUsed / 100f);
            Assert.Greater(wins, 0, "No simulated completion of level " + (level + 1));
        }
        Debug.Log(report.ToString());
    }
}
