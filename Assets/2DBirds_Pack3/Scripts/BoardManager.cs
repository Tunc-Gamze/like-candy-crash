using System;
using System.Collections;
using System.Collections.Generic;
using Birdsong;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 6;
    public int height = 6;
    public float offsetX = 2f;
    public float offsetY = 2f;
    [Header("Prefabs")]
    public GameObject[] birdPrefabs;
    [Header("Game Settings")]
    public float matchCheckDelay = 0.2f;
    public float animationDelay = 0.6f;
    public float collapseDelay = 0.1f;
    [Range(0.5f, 2f)] public float birdVisualScale = 1.55f;
    [Min(1)] public int maximumCascades = 100;
    [Header("Grid Visualization")]
    public bool showGrid = true;
    public Color gridColor = Color.white;
    public float gridLineWidth = 0.1f;
    public Material gridMaterial;

    public GameState State { get; private set; } = GameState.Menu;
    public bool CanInteract => State == GameState.Ready;
    public event Action<GameState> StateChanged;
    public event Action SwapAccepted;
    public event Action<List<int>, Dictionary<int, int>, int> MatchesResolved;
    public event Action Settled;
    public event Action<SoundCue> SoundRequested;
    public event Action<string> Feedback;
    public event Action<string> Failed;
    GameObject[,] grid;
    readonly System.Random random = new System.Random();
    readonly Dictionary<int, int> identities = new Dictionary<int, int>();
    GameState beforePause;
    Transform pieces;

    public string ValidateConfiguration()
    {
        if (width < 3 || height < 3 || width > 12 || height > 12 || offsetX <= 0 || offsetY <= 0)
            return "Invalid board dimensions or spacing.";
        if (birdPrefabs == null || birdPrefabs.Length < 3) return "Assign at least three bird prefabs.";
        identities.Clear();
        for (int i = 0; i < birdPrefabs.Length; i++)
        {
            if (birdPrefabs[i] == null) return "A bird prefab reference is missing.";
            var bird = birdPrefabs[i].GetComponent<Bird>();
            if (bird == null || bird.GetComponent<SpriteRenderer>() == null || bird.GetComponent<Collider2D>() == null ||
                bird.GetComponent<SpriteRenderer>().sprite == null)
                return "Every bird prefab needs Bird, SpriteRenderer and Collider2D components.";
            int key = Identity(bird);
            if (identities.ContainsKey(key)) return "Bird prefabs must have distinct type/color pairs.";
            identities.Add(key, i);
        }
        return null;
    }

    public HashSet<int> AvailableColors()
    {
        var result = new HashSet<int>();
        if (birdPrefabs != null) foreach (var prefab in birdPrefabs)
            if (prefab != null && prefab.TryGetComponent<Bird>(out var bird)) result.Add(bird.colorType);
        return result;
    }
    static int Identity(Bird bird) => bird.birdType * 100 + bird.colorType;

    public void BeginLevel()
    {
        ClearBoard();
        string error = ValidateConfiguration();
        if (error != null) { Fail(error); return; }
        SetState(GameState.Initializing);
        pieces = new GameObject("Live birds").transform;
        pieces.SetParent(transform, false);
        grid = new GameObject[width, height];
        StartCoroutine(InitializeBoard());
    }
    IEnumerator InitializeBoard()
    {
        int[,] layout = MatchRules.CreateBoard(width, height, birdPrefabs.Length, random);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) SpawnBirdAt(x, y, layout[x, y], y + height);
        yield return WaitForBirds();
        if (State != GameState.Error) { SetState(GameState.Ready); Settled?.Invoke(); }
    }
    void SpawnBirdAt(int x, int y, int type, int spawnY)
    {
        GameObject go = Instantiate(birdPrefabs[type], Position(x, spawnY), Quaternion.identity, pieces);
        Bird bird = go.GetComponent<Bird>();
        var renderer = go.GetComponent<SpriteRenderer>();
        Vector2 size = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.one;
        float scale = birdVisualScale * Mathf.Min(offsetX * 0.82f / Mathf.Max(size.x, 0.01f), offsetY * 0.82f / Mathf.Max(size.y, 0.01f));
        go.transform.localScale = Vector3.one * scale;
        if (go.TryGetComponent<BoxCollider2D>(out var collider))
        {
            collider.offset = Vector2.zero;
            collider.size = new Vector2(offsetX * 0.94f / scale, offsetY * 0.94f / scale);
        }
        renderer.sortingOrder = 2;
        grid[x, y] = go;
        bird.FallToPosition(Position(x, y), x, y);
    }
    public Vector3 Position(int x, int y) => new Vector3(x * offsetX, y * offsetY, 0);
    bool IsValidPosition(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
    public bool IsGridOccupied(int x, int y) => GetGridBird(x, y) != null;
    public GameObject GetGridBird(int x, int y) => grid != null && IsValidPosition(x, y) ? grid[x, y] : null;
    public int[,] Snapshot()
    {
        var result = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var go = GetGridBird(x, y);
                result[x, y] = go != null && identities.TryGetValue(Identity(go.GetComponent<Bird>()), out int type) ? type : -1;
            }
        return result;
    }
    bool Owns(Bird bird) => bird != null && GetGridBird(bird.xIndex, bird.yIndex) == bird.gameObject;
    public bool WouldCreateMatch(Bird a, Bird b) => Owns(a) && Owns(b) &&
        MatchRules.IsValidSwap(Snapshot(), new Cell(a.xIndex, a.yIndex), new Cell(b.xIndex, b.yIndex));
    public bool RequestSwap(Bird a, Bird b)
    {
        if (!CanInteract || !Owns(a) || !Owns(b) || !a.IsSelectable() || !b.IsSelectable() ||
            !MatchRules.Adjacent(new Cell(a.xIndex, a.yIndex), new Cell(b.xIndex, b.yIndex))) return false;
        SetState(GameState.Swapping);
        StartCoroutine(SwapAndResolve(a, b));
        return true;
    }
    IEnumerator SwapAndResolve(Bird a, Bird b)
    {
        SoundRequested?.Invoke(SoundCue.Swap);
        SwapPositions(a, b);
        yield return WaitForBirds();
        if (State == GameState.Error) yield break;
        var matches = MatchRules.FindMatches(Snapshot());
        if (matches.Count == 0)
        {
            SoundRequested?.Invoke(SoundCue.Invalid);
            Feedback?.Invoke("Try a match of three");
            SwapPositions(a, b);
            yield return WaitForBirds();
            if (State != GameState.Error) SetState(GameState.Ready);
            yield break;
        }
        SwapAccepted?.Invoke();
        SetState(GameState.Resolving);
        int cascade = 1;
        while (matches.Count > 0)
        {
            var sizes = new List<int>();
            var colors = new Dictionary<int, int>();
            float removalDuration = Mathf.Max(0, animationDelay);
            foreach (var group in matches)
            {
                sizes.Add(group.Count);
                foreach (var cell in group)
                {
                    Bird bird = grid[cell.X, cell.Y].GetComponent<Bird>();
                    colors.TryGetValue(bird.colorType, out int count);
                    colors[bird.colorType] = count + 1;
                    grid[cell.X, cell.Y] = null;
                    removalDuration = Mathf.Max(removalDuration, bird.flyAnimDuration);
                    bird.PlayFlyAndDestroy();
                }
            }
            MatchesResolved?.Invoke(sizes, colors, cascade);
            SoundRequested?.Invoke(cascade == 1 ? SoundCue.Match : SoundCue.Cascade);
            yield return new WaitForSeconds(removalDuration);
            CollapseBoard();
            SpawnNewBirds();
            yield return WaitForBirds();
            if (State == GameState.Error) yield break;
            matches = MatchRules.FindMatches(Snapshot());
            if (matches.Count > 0 && cascade >= Mathf.Max(1, maximumCascades))
            {
                Debug.LogWarning("Cascade safety limit reached; refreshing the board without spending a move.");
                yield return Reshuffle();
                break;
            }
            cascade++;
        }
        if (!MatchRules.HasMove(Snapshot())) yield return Reshuffle();
        if (State != GameState.Error) { SetState(GameState.Ready); Settled?.Invoke(); }
    }
    void SwapPositions(Bird a, Bird b)
    {
        int ax = a.xIndex, ay = a.yIndex, bx = b.xIndex, by = b.yIndex;
        grid[ax, ay] = b.gameObject;
        grid[bx, by] = a.gameObject;
        a.MoveToPosition(Position(bx, by), bx, by);
        b.MoveToPosition(Position(ax, ay), ax, ay);
    }
    void CollapseBoard()
    {
        // Preserve the original bottom-up collapse and Bird's falling animation.
        for (int x = 0; x < width; x++)
        {
            int destination = 0;
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null) continue;
                if (y != destination)
                {
                    grid[x, destination] = grid[x, y];
                    grid[x, y] = null;
                    grid[x, destination].GetComponent<Bird>().FallToPosition(Position(x, destination), x, destination);
                }
                destination++;
            }
        }
    }
    void SpawnNewBirds()
    {
        for (int x = 0; x < width; x++)
        {
            int spawnY = height;
            for (int y = 0; y < height; y++)
                if (grid[x, y] == null) SpawnBirdAt(x, y, random.Next(birdPrefabs.Length), spawnY++);
        }
    }
    IEnumerator WaitForBirds()
    {
        while (true)
        {
            if (State == GameState.Paused) { yield return null; continue; }
            bool moving = false;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] == null || !grid[x, y].TryGetComponent<Bird>(out var bird))
                    { Fail("A live bird reference was lost. Please restart the level."); yield break; }
                    moving |= bird.IsMoving();
                }
            if (!moving) yield break;
            yield return null;
        }
    }
    IEnumerator Reshuffle()
    {
        Feedback?.Invoke("A fresh flock! Shuffling...");
        int[,] layout = MatchRules.Reshuffle(Snapshot(), birdPrefabs.Length, random);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null) { grid[x, y].SetActive(false); Destroy(grid[x, y]); }
                SpawnBirdAt(x, y, layout[x, y], y + height);
            }
        yield return WaitForBirds();
    }
    public void SetState(GameState state) { State = state; StateChanged?.Invoke(state); }
    public void Pause()
    {
        if (State != GameState.Ready && State != GameState.Swapping && State != GameState.Resolving && State != GameState.Initializing) return;
        beforePause = State;
        SetState(GameState.Paused);
    }
    public void Resume() { if (State == GameState.Paused) SetState(beforePause); }
    void Fail(string message) { SetState(GameState.Error); Debug.LogError(message); Failed?.Invoke(message); }
    public void ClearBoard()
    {
        StopAllCoroutines();
        if (pieces != null) { pieces.gameObject.SetActive(false); Destroy(pieces.gameObject); }
        pieces = null;
        grid = null;
        SetState(GameState.Menu);
    }
    void OnDrawGizmos()
    {
        if (!showGrid) return;
        Gizmos.color = gridColor;
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
            Gizmos.DrawWireCube(Position(x, y), new Vector3(offsetX * 0.95f, offsetY * 0.95f, 0));
    }
}
