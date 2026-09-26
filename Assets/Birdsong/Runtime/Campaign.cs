using System;
using System.Collections.Generic;

namespace Birdsong
{
    public enum ObjectiveKind { Score, CollectColor }
    public enum GameState { Menu, Initializing, Ready, Swapping, Resolving, Paused, Won, Lost, Error }

    [Serializable]
    public class ObjectiveDefinition
    {
        public ObjectiveKind kind;
        public string label;
        public int target;
        public int color;
    }

    [Serializable]
    public class LevelDefinition
    {
        public string title;
        public int moves;
        public ObjectiveDefinition[] objectives;
    }

    [Serializable]
    public class Scoring
    {
        public int three = 100, four = 200, five = 300, extra = 100;
        public int Points(int count, int cascade)
        {
            if (count < 3 || cascade < 1) return 0;
            return (count == 3 ? three : count == 4 ? four : five + (count - 5) * extra) * cascade;
        }
    }

    [Serializable]
    public class Campaign
    {
        public Scoring scoring;
        public LevelDefinition[] levels;
        public string Validate(HashSet<int> availableColors)
        {
            if (levels == null || levels.Length != 6) return "Campaign must contain exactly six levels.";
            if (scoring == null || scoring.three <= 0 || scoring.four <= 0 || scoring.five <= 0 || scoring.extra < 0)
                return "Invalid score configuration.";
            foreach (var level in levels)
            {
                if (level == null || level.moves <= 0 || string.IsNullOrWhiteSpace(level.title) ||
                    level.objectives == null || level.objectives.Length == 0) return "Invalid level configuration.";
                foreach (var goal in level.objectives)
                    if (goal == null || goal.target <= 0 || string.IsNullOrWhiteSpace(goal.label) ||
                        !Enum.IsDefined(typeof(ObjectiveKind), goal.kind) ||
                        (goal.kind == ObjectiveKind.CollectColor && !availableColors.Contains(goal.color)))
                        return "Objective is invalid or requests an unavailable bird color.";
            }
            return null;
        }
    }

    public sealed class LevelSession
    {
        public LevelDefinition Level { get; }
        public int Score { get; private set; }
        public int Moves { get; private set; }
        readonly Dictionary<int, int> collected = new Dictionary<int, int>();
        readonly Scoring scoring;
        public LevelSession(LevelDefinition level, Scoring scoring)
        { Level = level; Moves = level.moves; this.scoring = scoring; }
        public bool ConsumeMove()
        {
            if (Moves <= 0) return false;
            Moves--;
            return true;
        }
        public int Award(IList<int> groupSizes, IDictionary<int, int> colors, int cascade)
        {
            int points = 0;
            foreach (int size in groupSizes) points += scoring.Points(size, cascade);
            Score += points;
            foreach (var color in colors)
                collected[color.Key] = Collected(color.Key) + color.Value;
            return points;
        }
        public int Collected(int color) => collected.TryGetValue(color, out int count) ? count : 0;
        public int Progress(ObjectiveDefinition goal) => goal.kind == ObjectiveKind.Score ? Score : Collected(goal.color);
        public bool Won
        {
            get
            {
                foreach (var goal in Level.objectives) if (Progress(goal) < goal.target) return false;
                return true;
            }
        }
        public bool Lost => Moves == 0 && !Won;
    }
}
