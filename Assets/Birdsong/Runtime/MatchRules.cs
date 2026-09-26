using System;
using System.Collections.Generic;

namespace Birdsong
{
    public struct Cell : IEquatable<Cell>
    {
        public int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => X * 397 ^ Y;
    }

    // -1 denotes an empty cell. Identity is the prefab's (birdType, colorType) pair.
    public static class MatchRules
    {
        public static bool Contains(int[,] grid, Cell p) => p.X >= 0 && p.Y >= 0 &&
            p.X < grid.GetLength(0) && p.Y < grid.GetLength(1);

        public static bool Adjacent(Cell a, Cell b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;

        public static void Swap(int[,] grid, Cell a, Cell b)
        {
            int value = grid[a.X, a.Y];
            grid[a.X, a.Y] = grid[b.X, b.Y];
            grid[b.X, b.Y] = value;
        }

        public static List<HashSet<Cell>> FindMatches(int[,] grid)
        {
            var groups = new List<HashSet<Cell>>();
            int width = grid.GetLength(0), height = grid.GetLength(1);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width;)
                {
                    int end = x + 1;
                    while (end < width && grid[end, y] == grid[x, y]) end++;
                    if (grid[x, y] >= 0 && end - x >= 3)
                    {
                        var run = new HashSet<Cell>();
                        for (int i = x; i < end; i++) run.Add(new Cell(i, y));
                        groups.Add(run);
                    }
                    x = end;
                }
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height;)
                {
                    int end = y + 1;
                    while (end < height && grid[x, end] == grid[x, y]) end++;
                    if (grid[x, y] >= 0 && end - y >= 3)
                    {
                        var run = new HashSet<Cell>();
                        for (int i = y; i < end; i++) run.Add(new Cell(x, i));
                        groups.Add(run);
                    }
                    y = end;
                }
            // Intersecting lines form one match; count a crossing bird only once.
            for (int i = 0; i < groups.Count; i++)
                for (int j = i + 1; j < groups.Count; j++)
                    if (groups[i].Overlaps(groups[j]))
                    {
                        groups[i].UnionWith(groups[j]);
                        groups.RemoveAt(j);
                        j = i;
                    }
            return groups;
        }

        public static bool IsValidSwap(int[,] grid, Cell a, Cell b)
        {
            if (!Contains(grid, a) || !Contains(grid, b) || !Adjacent(a, b) ||
                grid[a.X, a.Y] < 0 || grid[b.X, b.Y] < 0 || grid[a.X, a.Y] == grid[b.X, b.Y]) return false;
            Swap(grid, a, b);
            try
            {
                foreach (var match in FindMatches(grid))
                    if (match.Contains(a) || match.Contains(b)) return true;
                return false;
            }
            finally { Swap(grid, a, b); }
        }

        public static bool HasMove(int[,] grid)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
                for (int y = 0; y < grid.GetLength(1); y++)
                    if (IsValidSwap(grid, new Cell(x, y), new Cell(x + 1, y)) ||
                        IsValidSwap(grid, new Cell(x, y), new Cell(x, y + 1))) return true;
            return false;
        }

        public static int[,] CreateBoard(int width, int height, int types, Random random, int attempts = 64)
        {
            if (width < 3 || height < 3 || types < 3 || random == null)
                throw new ArgumentException("Board requires dimensions >= 3, at least three identities and a random source.");
            var grid = new int[width, height];
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var choices = new List<int>();
                        for (int type = 0; type < types; type++)
                            if (!(x > 1 && grid[x - 1, y] == type && grid[x - 2, y] == type) &&
                                !(y > 1 && grid[x, y - 1] == type && grid[x, y - 2] == type)) choices.Add(type);
                        grid[x, y] = choices[random.Next(choices.Count)];
                    }
                if (HasMove(grid)) return grid;
            }
            // Constructive fallback: no retry loop, no triples, and (1,0)<->(1,1) makes a match.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) grid[x, y] = (x + y) % 3;
            grid[2, 0] = 0;
            grid[1, 1] = 0;
            return grid;
        }

        public static int[,] Reshuffle(int[,] source, int types, Random random)
        {
            var board = (int[,])source.Clone();
            int width = board.GetLength(0), height = board.GetLength(1), count = width * height;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                for (int i = count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    Swap(board, new Cell(i % width, i / width), new Cell(j % width, j / width));
                }
                if (FindMatches(board).Count == 0 && HasMove(board)) return board;
            }
            return CreateBoard(width, height, types, random);
        }

        public static void Collapse(int[,] grid)
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                int destination = 0;
                for (int y = 0; y < grid.GetLength(1); y++)
                    if (grid[x, y] >= 0) grid[x, destination++] = grid[x, y];
                while (destination < grid.GetLength(1)) grid[x, destination++] = -1;
            }
        }
    }
}
