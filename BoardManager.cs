using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 6;
    public int height = 6;
    public float offsetX = 2f;
    public float offsetY = 2f;

    [Header("Prefabs")]
    public GameObject[] birdPrefabs; // Tüm kuş prefablarını sürükle

    [Header("Game Settings")]
    public float matchCheckDelay = 0.2f;
    public float animationDelay = 0.6f;
    public float collapseDelay = 0.1f;

    [Header("Grid Visualization")]
    public bool showGrid = true;
    public Color gridColor = Color.white;
    public float gridLineWidth = 0.1f;
    public Material gridMaterial;

    private GameObject[,] grid;
    private bool isProcessingMatches = false;
    private InputManager inputManager;

    void Start()
    {
        InitializeGrid();
        SetupInputManager();
        SetupBoard();
        StartCoroutine(CheckMatchesAndClearRoutine());
    }

    void SetupInputManager()
    {
        // InputManager'ı bul veya oluştur
        inputManager = FindObjectOfType<InputManager>();
        if (inputManager == null)
        {
            // InputManager script'i varsa oluştur
            if (System.Type.GetType("InputManager") != null)
            {
                GameObject inputManagerGO = new GameObject("InputManager");
                inputManager = inputManagerGO.AddComponent<InputManager>();
                
                // InputManager event'lerini dinle
                inputManager.OnBirdSwap += OnBirdSwapRequested;
            }
            else
            {
                Debug.LogWarning("InputManager script not found. Drag & drop functionality will be disabled.");
            }
        }
        else
        {
            // InputManager event'lerini dinle
            inputManager.OnBirdSwap += OnBirdSwapRequested;
        }
    }

    void InitializeGrid()
    {
        grid = new GameObject[width, height];
    }

    void SetupBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SpawnBirdAt(x, y);
            }
        }
    }

    void SpawnBirdAt(int x, int y)
    {
        if (birdPrefabs == null || birdPrefabs.Length == 0)
        {
            Debug.LogError("Bird prefabs array is empty!");
            return;
        }

        // Akıllı kuş seçimi - eşleşme oluşturmayacak tip seç
        int safeBirdType = GetSafeBirdType(x, y);
        Vector3 spawnPos = new Vector3(x * offsetX, (y + height) * offsetY, 0); // Üstten başla
        GameObject go = Instantiate(birdPrefabs[safeBirdType], spawnPos, Quaternion.identity, this.transform);

        Bird bird = go.GetComponent<Bird>();
        if (bird != null)
        {
            bird.xIndex = x;
            bird.yIndex = y;
        }

        grid[x, y] = go;
        
        // Yumuşak düşme animasyonu
        StartCoroutine(AnimateBirdFall(go, new Vector3(x * offsetX, y * offsetY, 0)));
    }

    int GetSafeBirdType(int x, int y)
    {
        // Mevcut kuş tiplerini kontrol et
        List<int> safeTypes = new List<int>();
        
        for (int i = 0; i < birdPrefabs.Length; i++)
        {
            if (WouldCreateMatchAt(x, y, i))
            {
                continue; // Bu tip eşleşme oluşturur, güvenli değil
            }
            safeTypes.Add(i);
        }
        
        // Güvenli tip yoksa rastgele seç
        if (safeTypes.Count == 0)
        {
            return Random.Range(0, birdPrefabs.Length);
        }
        
        return safeTypes[Random.Range(0, safeTypes.Count)];
    }

    bool WouldCreateMatchAt(int x, int y, int birdType)
    {
        // Geçici olarak bu pozisyona bu tip kuş koy
        GameObject tempBird = null;
        if (grid[x, y] != null)
        {
            tempBird = grid[x, y];
        }
        
        // Geçici kuş oluştur
        GameObject testBird = Instantiate(birdPrefabs[birdType], Vector3.zero, Quaternion.identity);
        Bird birdComponent = testBird.GetComponent<Bird>();
        if (birdComponent != null)
        {
            birdComponent.xIndex = x;
            birdComponent.yIndex = y;
        }
        
        grid[x, y] = testBird;
        
        // Eşleşme kontrolü yap
        List<GameObject> matches = FindAllMatches();
        bool wouldMatch = matches.Count > 0;
        
        // Temizlik
        grid[x, y] = tempBird;
        DestroyImmediate(testBird);
        
        return wouldMatch;
    }

    IEnumerator AnimateBirdFall(GameObject bird, Vector3 targetPos)
    {
        Vector3 startPos = bird.transform.position;
        float fallDuration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fallDuration;
            
            // Yumuşak düşme eğrisi
            float curve = Mathf.SmoothStep(0f, 1f, progress);
            bird.transform.position = Vector3.Lerp(startPos, targetPos, curve);
            
            yield return null;
        }
        
        bird.transform.position = targetPos;
    }

    IEnumerator CheckMatchesAndClearRoutine()
    {
        yield return new WaitForSeconds(matchCheckDelay);

        while (true)
        {
            if (isProcessingMatches)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            List<GameObject> matches = FindAllMatches();

            if (matches.Count > 0)
            {
                isProcessingMatches = true;
                yield return StartCoroutine(ProcessMatches(matches));
                isProcessingMatches = false;
            }
            else
            {
                yield break;
            }
        }
    }

    IEnumerator ProcessMatches(List<GameObject> matches)
    {
        Debug.Log($"Processing {matches.Count} matches...");
        
        // Eşleşen kuşları işaretle ve yok et
        HashSet<GameObject> uniqueMatches = new HashSet<GameObject>(matches);
        foreach (GameObject go in uniqueMatches)
        {
            if (go == null) continue;
            
            Bird bird = go.GetComponent<Bird>();
            if (bird != null)
            {
                // Grid'den kaldır
                if (IsValidPosition(bird.xIndex, bird.yIndex))
                {
                    grid[bird.xIndex, bird.yIndex] = null;
                    Debug.Log($"Removed bird from grid at ({bird.xIndex}, {bird.yIndex})");
                }

                bird.PlayFlyAndDestroy();
            }
            else
            {
                Destroy(go);
            }
        }

        yield return new WaitForSeconds(animationDelay);
        
        // Grid'i temizle
        CleanGrid();
        
        // Board'ı çökert ve yeni kuşlar spawn et
        CollapseBoard();
        SpawnNewBirds();
        
        yield return new WaitForSeconds(collapseDelay);
        
        Debug.Log("Match processing completed");
    }
    
    void CleanGrid()
    {
        // Grid'deki null referansları temizle
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != null && grid[x, y] == null)
                {
                    grid[x, y] = null;
                }
            }
        }
    }

    List<GameObject> FindAllMatches()
    {
        List<GameObject> matched = new List<GameObject>();

        // Horizontal matches
        for (int y = 0; y < height; y++)
        {
            matched.AddRange(FindMatchesInRow(y));
        }

        // Vertical matches
        for (int x = 0; x < width; x++)
        {
            matched.AddRange(FindMatchesInColumn(x));
        }

        return matched;
    }

    List<GameObject> FindMatchesInRow(int y)
    {
        List<GameObject> matches = new List<GameObject>();
        
        for (int x = 0; x < width - 2; x++)
        {
            GameObject start = grid[x, y];
            if (start == null) continue;
            
            Bird startBird = start.GetComponent<Bird>();
            if (startBird == null) continue;

            int matchLength = 1;
            List<GameObject> currentMatch = new List<GameObject> { start };

            // Sağa doğru kontrol et
            for (int xx = x + 1; xx < width; xx++)
            {
                GameObject next = grid[xx, y];
                if (next == null) break;
                
                Bird nextBird = next.GetComponent<Bird>();
                if (nextBird == null) break;

                if (IsSameBirdType(startBird, nextBird))
                {
                    matchLength++;
                    currentMatch.Add(next);
                }
                else
                {
                    break;
                }
            }

            if (matchLength >= 3)
            {
                matches.AddRange(currentMatch);
            }
        }

        return matches;
    }

    List<GameObject> FindMatchesInColumn(int x)
    {
        List<GameObject> matches = new List<GameObject>();
        
        for (int y = 0; y < height - 2; y++)
        {
            GameObject start = grid[x, y];
            if (start == null) continue;
            
            Bird startBird = start.GetComponent<Bird>();
            if (startBird == null) continue;

            int matchLength = 1;
            List<GameObject> currentMatch = new List<GameObject> { start };

            // Yukarı doğru kontrol et
            for (int yy = y + 1; yy < height; yy++)
            {
                GameObject next = grid[x, yy];
                if (next == null) break;
                
                Bird nextBird = next.GetComponent<Bird>();
                if (nextBird == null) break;

                if (IsSameBirdType(startBird, nextBird))
                {
                    matchLength++;
                    currentMatch.Add(next);
                }
                else
                {
                    break;
                }
            }

            if (matchLength >= 3)
            {
                matches.AddRange(currentMatch);
            }
        }

        return matches;
    }

    bool IsSameBirdType(Bird bird1, Bird bird2)
    {
        return bird1.birdType == bird2.birdType && bird1.colorType == bird2.colorType;
    }

    void CollapseColumn(int x)
    {
        Debug.Log($"Collapsing column {x}");
        
        // Sütunu yukarıdan aşağıya kontrol et
        for (int y = 0; y < height - 1; y++)
        {
            if (grid[x, y] == null) // Boşluk varsa
            {
                // Yukarıda kuş ara
                for (int yy = y + 1; yy < height; yy++)
                {
                    if (grid[x, yy] != null) // Kuş bulundu
                    {
                        // Grid güncelle
                        grid[x, y] = grid[x, yy];
                        grid[x, yy] = null;

                        // Kuşun pozisyonunu güncelle
                        Bird bird = grid[x, y].GetComponent<Bird>();
                        if (bird != null)
                        {
                            bird.xIndex = x;
                            bird.yIndex = y;
                        }

                        // Yumuşak düşme animasyonu
                        Vector3 targetPos = new Vector3(x * offsetX, y * offsetY, 0);
                        Bird birdComponent = grid[x, y].GetComponent<Bird>();
                        if (birdComponent != null)
                        {
                            birdComponent.FallToPosition(targetPos, x, y);
                        }
                        else
                        {
                            StartCoroutine(AnimateBirdFall(grid[x, y], targetPos));
                        }
                        
                        Debug.Log($"Moved bird from ({x}, {yy}) to ({x}, {y})");

                        break; // Bir kuşu aşağı indirdik, çık
                    }
                }
            }
        }
        
        // Collapse sonrası grid durumunu kontrol et
        Debug.Log($"Column {x} collapse completed");
    }

    void CollapseBoard()
    {
        for (int x = 0; x < width; x++)
        {
            CollapseColumn(x);
        }
    }

    void SpawnNewBirds()
    {
        Debug.Log("Starting to spawn new birds...");
        
        // Staggered spawn - her sütunu ayrı ayrı spawn et
        for (int x = 0; x < width; x++)
        {
            StartCoroutine(SpawnColumnBirds(x));
        }
        
        Debug.Log("New bird spawning initiated");
    }

    IEnumerator SpawnColumnBirds(int x)
    {
        // Sütundaki boşlukları bul
        List<int> emptyPositions = new List<int>();
        for (int y = 0; y < height; y++)
        {
            if (grid[x, y] == null)
            {
                emptyPositions.Add(y);
            }
        }
        
        // Her boş pozisyon için kuş spawn et
        foreach (int y in emptyPositions)
        {
            SpawnBirdAt(x, y);
            Debug.Log($"Spawned new bird at ({x}, {y})");
            
            // Kısa gecikme ile staggered spawn
            yield return new WaitForSeconds(0.1f);
        }
        
        // Grid durumunu kontrol et
        ValidateGrid();
    }
    
    void ValidateGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null)
                {
                    Debug.LogWarning($"Grid position ({x}, {y}) is null after spawn!");
                }
                else
                {
                    Bird bird = grid[x, y].GetComponent<Bird>();
                    if (bird != null)
                    {
                        if (bird.xIndex != x || bird.yIndex != y)
                        {
                            Debug.LogWarning($"Bird at ({x}, {y}) has wrong indices: ({bird.xIndex}, {bird.yIndex})");
                            bird.xIndex = x;
                            bird.yIndex = y;
                        }
                    }
                }
            }
        }
    }

    bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Grid durumunu kontrol et (GridVisualizer için)
    public bool IsGridOccupied(int x, int y)
    {
        if (!IsValidPosition(x, y)) return false;
        return grid[x, y] != null;
    }

    // Grid'deki kuşu al
    public GameObject GetGridBird(int x, int y)
    {
        if (!IsValidPosition(x, y)) return null;
        return grid[x, y];
    }

    // InputManager'dan gelen swap isteği
    void OnBirdSwapRequested(Bird bird1, Bird bird2)
    {
        if (isProcessingMatches) return;
        
        // Grid'de pozisyonları güncelle
        UpdateGridPositions(bird1, bird2);
        
        // Eşleşme kontrolü yap
        StartCoroutine(CheckMatchesAfterSwap());
    }

    void UpdateGridPositions(Bird bird1, Bird bird2)
    {
        // Grid'de pozisyonları değiştir
        grid[bird1.xIndex, bird1.yIndex] = bird1.gameObject;
        grid[bird2.xIndex, bird2.yIndex] = bird2.gameObject;
        
        Debug.Log($"Grid updated: Bird1 at ({bird1.xIndex},{bird1.yIndex}), Bird2 at ({bird2.xIndex},{bird2.yIndex})");
    }

    IEnumerator CheckMatchesAfterSwap()
    {
        yield return new WaitForSeconds(0.1f); // Swap animasyonunun bitmesini bekle
        
        List<GameObject> matches = FindAllMatches();
        
        if (matches.Count > 0)
        {
            // Eşleşme var, işle
            isProcessingMatches = true;
            yield return StartCoroutine(ProcessMatches(matches));
            isProcessingMatches = false;
        }
        else
        {
            // Eşleşme yok, swap'ı geri al
            Debug.Log("No match found, reverting swap");
            yield return StartCoroutine(RevertSwap());
        }
    }

    IEnumerator RevertSwap()
    {
        // Son swap'ı geri al (InputManager'da saklanmalı)
        if (inputManager != null)
        {
            Bird lastBird1 = inputManager.GetSelectedBird();
            Bird lastBird2 = inputManager.GetTargetBird();
            
            if (lastBird1 != null && lastBird2 != null)
            {
                // Pozisyonları geri değiştir
                Vector3 pos1 = lastBird1.transform.position;
                Vector3 pos2 = lastBird2.transform.position;
                
                int x1 = lastBird1.xIndex;
                int y1 = lastBird1.yIndex;
                int x2 = lastBird2.xIndex;
                int y2 = lastBird2.yIndex;
                
                // Animasyonlu geri dönüş
                yield return StartCoroutine(AnimatedRevert(lastBird1, lastBird2, pos1, pos2, x1, y1, x2, y2));
            }
        }
        else
        {
            Debug.LogWarning("InputManager not available for swap revert.");
        }
    }

    IEnumerator AnimatedRevert(Bird bird1, Bird bird2, Vector3 pos1, Vector3 pos2, int x1, int y1, int x2, int y2)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        
        Vector3 startPos1 = bird1.transform.position;
        Vector3 startPos2 = bird2.transform.position;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float curve = Mathf.SmoothStep(0f, 1f, progress);
            
            bird1.transform.position = Vector3.Lerp(startPos1, pos1, curve);
            bird2.transform.position = Vector3.Lerp(startPos2, pos2, curve);
            
            yield return null;
        }
        
        // Pozisyonları güncelle
        bird1.transform.position = pos1;
        bird2.transform.position = pos2;
        bird1.xIndex = x1;
        bird1.yIndex = y1;
        bird2.xIndex = x2;
        bird2.yIndex = y2;
    }

    // InputManager için eşleşme kontrolü
    public bool WouldCreateMatch(Bird bird1, Bird bird2)
    {
        // Geçici olarak grid'de pozisyonları değiştir
        GameObject temp1 = grid[bird1.xIndex, bird1.yIndex];
        GameObject temp2 = grid[bird2.xIndex, bird2.yIndex];
        
        // Geçici pozisyon değişimi
        int tempX1 = bird1.xIndex;
        int tempY1 = bird1.yIndex;
        int tempX2 = bird2.xIndex;
        int tempY2 = bird2.yIndex;
        
        // Grid'de pozisyonları değiştir
        grid[tempX1, tempY1] = bird2.gameObject;
        grid[tempX2, tempY2] = bird1.gameObject;
        
        // Kuşların geçici pozisyonlarını güncelle
        bird1.xIndex = tempX2;
        bird1.yIndex = tempY2;
        bird2.xIndex = tempX1;
        bird2.yIndex = tempY1;
        
        // Eşleşme kontrolü yap
        List<GameObject> matches = FindAllMatches();
        bool wouldMatch = matches.Count > 0;
        
        Debug.Log($"Would create match check: {matches.Count} matches found");
        
        // Grid'i geri al
        grid[tempX1, tempY1] = temp1;
        grid[tempX2, tempY2] = temp2;
        
        // Kuşların pozisyonlarını geri al
        bird1.xIndex = tempX1;
        bird1.yIndex = tempY1;
        bird2.xIndex = tempX2;
        bird2.yIndex = tempY2;
        
        return wouldMatch;
    }

    // Swap sırasında grid'de kuşları değiştir
    public void SwapBirdsInGrid(Bird bird1, Bird bird2, int x1, int y1, int x2, int y2)
    {
        Debug.Log($"Swapping birds in grid: ({x1},{y1}) <-> ({x2},{y2})");
        
        // Grid'de pozisyonları değiştir
        grid[x1, y1] = bird2.gameObject;
        grid[x2, y2] = bird1.gameObject;
        
        Debug.Log($"Grid updated: ({x1},{y1}) = {bird2.name}, ({x2},{y2}) = {bird1.name}");
    }

    // Swap tamamlandığında çağrılır
    public void OnSwapCompleted(Bird bird1, Bird bird2)
    {
        // Grid pozisyonlarını güncelle
        UpdateGridPositions(bird1, bird2);
    }

    // Debug için grid durumunu yazdır
    [ContextMenu("Debug Grid")]
    void DebugGrid()
    {
        Debug.Log("=== GRID DEBUG ===");
        for (int y = height - 1; y >= 0; y--)
        {
            string row = "";
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] == null)
                    row += "X ";
                else
                {
                    Bird bird = grid[x, y].GetComponent<Bird>();
                    if (bird != null)
                        row += bird.birdType + " ";
                    else
                        row += "? ";
                }
            }
            Debug.Log("Row " + y + ": " + row);
        }
        Debug.Log("=== END GRID DEBUG ===");
    }

    // Grid görünürlüğünü aç/kapat
    [ContextMenu("Toggle Grid")]
    void ToggleGrid()
    {
        showGrid = !showGrid;
        Debug.Log($"Grid visualization: {(showGrid ? "ON" : "OFF")}");
    }

    // Grid rengini değiştir
    [ContextMenu("Change Grid Color")]
    void ChangeGridColor()
    {
        if (gridColor == Color.white)
            gridColor = Color.red;
        else if (gridColor == Color.red)
            gridColor = Color.green;
        else if (gridColor == Color.green)
            gridColor = Color.blue;
        else
            gridColor = Color.white;
        
        Debug.Log($"Grid color changed to: {gridColor}");
    }
    
    // Grid durumunu sürekli kontrol et
    void Update()
    {
        // Her 5 saniyede bir grid durumunu kontrol et
        if (Time.time % 5f < Time.deltaTime)
        {
            ValidateGrid();
        }
    }

    // Grid çizgilerini çiz
    void OnDrawGizmos()
    {
        if (!showGrid) return;

        Gizmos.color = gridColor;
        
        // Dikey çizgiler
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = new Vector3(x * offsetX - offsetX * 0.5f, -offsetY * 0.5f, 0);
            Vector3 end = new Vector3(x * offsetX - offsetX * 0.5f, (height - 0.5f) * offsetY, 0);
            Gizmos.DrawLine(start, end);
        }
        
        // Yatay çizgiler
        for (int y = 0; y <= height; y++)
        {
            Vector3 start = new Vector3(-offsetX * 0.5f, y * offsetY - offsetY * 0.5f, 0);
            Vector3 end = new Vector3((width - 0.5f) * offsetX, y * offsetY - offsetY * 0.5f, 0);
            Gizmos.DrawLine(start, end);
        }
        
        // Grid hücrelerini numaralandır
        if (Application.isPlaying)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 pos = new Vector3(x * offsetX, y * offsetY, 0);
                    
                    // Hücre durumunu göster
                    if (grid != null && grid[x, y] != null)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                    }
                }
            }
        }
    }

    // Grid koordinatlarını göster
    void OnDrawGizmosSelected()
    {
        if (!showGrid) return;

        Gizmos.color = Color.yellow;
        
        // Koordinat numaralarını göster
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x * offsetX, y * offsetY, 0);
                Vector3 labelPos = pos + Vector3.up * 0.3f;
                
                // Unity'de text çizemeyiz ama küçük küp ile gösterelim
                Gizmos.DrawWireCube(labelPos, Vector3.one * 0.2f);
            }
        }
    }
}
