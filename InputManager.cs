using System.Collections;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [Header("Input Settings")]
    public float dragThreshold = 0.5f; // Minimum sürükleme mesafesi
    public LayerMask birdLayerMask = -1; // Kuş layer'ı
    
    [Header("Visual Feedback")]
    public float dragScale = 1.2f; // Sürüklenen kuşun büyüklüğü
    public Color dragColor = Color.white; // Sürüklenen kuşun rengi
    public Color validDropColor = Color.green; // Geçerli drop rengi
    public Color invalidDropColor = Color.red; // Geçersiz drop rengi
    
    private Camera mainCamera;
    private Bird selectedBird;
    private Vector3 dragOffset;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isDragging = false;
    private bool isValidDrop = false;
    private Bird targetBird;
    
    // Events
    public System.Action<Bird, Bird> OnBirdSwap; // Kuş değişimi eventi
    
    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartDrag();
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDrag();
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }
    }
    
    void StartDrag()
    {
        // Eğer zaten sürüklüyorsak, yeni sürükleme başlatma
        if (isDragging) return;
        
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        
        Debug.Log($"Mouse clicked at world position: {mousePos}");
        Debug.Log($"Bird Layer Mask: {birdLayerMask.value}");
        
        Collider2D hit = Physics2D.OverlapPoint(mousePos, birdLayerMask);
        Debug.Log($"Hit collider: {(hit != null ? hit.name : "None")}");
        
        if (hit != null)
        {
            Bird bird = hit.GetComponent<Bird>();
            Debug.Log($"Bird component found: {(bird != null ? "Yes" : "No")}");
            
            if (bird != null)
            {
                Debug.Log($"Bird state - Matched: {bird.IsMatched()}, Moving: {bird.IsMoving()}");
                
                if (!bird.IsMatched() && !bird.IsMoving())
                {
                    selectedBird = bird;
                    dragOffset = mousePos - bird.transform.position;
                    originalPosition = bird.transform.position;
                    originalScale = bird.transform.localScale;
                    
                    // Görsel geri bildirim
                    if (bird.GetComponent<SpriteRenderer>() != null)
                    {
                        originalColor = bird.GetComponent<SpriteRenderer>().color;
                        bird.GetComponent<SpriteRenderer>().color = dragColor;
                    }
                    
                    bird.transform.localScale = originalScale * dragScale;
                    bird.transform.SetAsLastSibling(); // En üstte görünsün
                    
                    isDragging = true;
                    Debug.Log($"Started dragging bird at ({bird.xIndex}, {bird.yIndex})");
                }
                else
                {
                    Debug.Log("Bird is matched or moving, cannot drag");
                }
            }
        }
        else
        {
            Debug.Log("No collider hit at mouse position");
        }
    }
    
    void UpdateDrag()
    {
        if (selectedBird == null) return;
        
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        
        // Sürükleme kısıtlaması - sadece yatay/dikey hareket
        Vector3 constrainedPos = ConstrainDragMovement(mousePos);
        selectedBird.transform.position = constrainedPos;
        
        // Drop hedefini kontrol et
        CheckDropTarget(mousePos);
    }
    
    Vector3 ConstrainDragMovement(Vector3 mousePos)
    {
        Vector3 targetPos = mousePos - dragOffset;
        Vector3 originalPos = originalPosition;
        
        // Sadece yatay veya dikey hareket izin ver
        float horizontalDistance = Mathf.Abs(targetPos.x - originalPos.x);
        float verticalDistance = Mathf.Abs(targetPos.y - originalPos.y);
        
        Vector3 constrainedPos = originalPos;
        
        if (horizontalDistance > verticalDistance)
        {
            // Yatay hareket
            constrainedPos.x = targetPos.x;
            constrainedPos.y = originalPos.y;
        }
        else
        {
            // Dikey hareket
            constrainedPos.x = originalPos.x;
            constrainedPos.y = targetPos.y;
        }
        
        return constrainedPos;
    }
    
    void CheckDropTarget(Vector3 mousePos)
    {
        // Tüm collider'ları kontrol et
        Collider2D[] hits = Physics2D.OverlapPointAll(mousePos, birdLayerMask);
        Bird newTargetBird = null;
        
        // En yakın kuşu bul
        float closestDistance = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            if (hit != null)
            {
                Bird bird = hit.GetComponent<Bird>();
                if (bird != null && bird != selectedBird)
                {
                    float distance = Vector3.Distance(mousePos, hit.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        newTargetBird = bird;
                    }
                }
            }
        }
        
        // Hedef değişti mi?
        if (newTargetBird != targetBird)
        {
            // Eski hedefin rengini sıfırla
            if (targetBird != null && targetBird != selectedBird)
            {
                ResetBirdColor(targetBird);
            }
            
            targetBird = newTargetBird;
            
            // Yeni hedefin rengini ayarla
            if (targetBird != null && targetBird != selectedBird)
            {
                isValidDrop = IsValidSwap(selectedBird, targetBird);
                Color feedbackColor = isValidDrop ? validDropColor : invalidDropColor;
                SetBirdColor(targetBird, feedbackColor);
                
                Debug.Log($"Target bird: ({targetBird.xIndex},{targetBird.yIndex}), Valid swap: {isValidDrop}");
            }
            else
            {
                isValidDrop = false;
            }
        }
    }
    
    void EndDrag()
    {
        if (selectedBird == null) return;
        
        bool swapPerformed = false;
        
        if (targetBird != null && targetBird != selectedBird && isValidDrop)
        {
            // Geçerli swap yap
            PerformSwap(selectedBird, targetBird);
            swapPerformed = true;
        }
        else
        {
            // Orijinal pozisyona geri döndür
            ReturnToOriginalPosition();
        }
        
        // Görsel geri bildirimleri sıfırla
        ResetBirdColor(selectedBird);
        if (targetBird != null && targetBird != selectedBird)
        {
            ResetBirdColor(targetBird);
        }
        
        selectedBird.transform.localScale = originalScale;
        selectedBird = null;
        targetBird = null;
        isDragging = false;
        isValidDrop = false;
        
        Debug.Log($"Drag ended. Swap performed: {swapPerformed}");
    }
    
    bool IsValidSwap(Bird bird1, Bird bird2)
    {
        // Komşu kontrolü
        if (!AreNeighbors(bird1, bird2))
        {
            return false;
        }
        
        // Geçici swap yap ve eşleşme kontrolü
        return WouldCreateMatch(bird1, bird2);
    }
    
    bool AreNeighbors(Bird bird1, Bird bird2)
    {
        int dx = Mathf.Abs(bird1.xIndex - bird2.xIndex);
        int dy = Mathf.Abs(bird1.yIndex - bird2.yIndex);
        
        // Sadece yatay veya dikey komşular (çapraz değil)
        bool isNeighbor = (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        
        Debug.Log($"Neighbor check: Bird1({bird1.xIndex},{bird1.yIndex}) vs Bird2({bird2.xIndex},{bird2.yIndex}) - dx:{dx}, dy:{dy}, isNeighbor:{isNeighbor}");
        
        return isNeighbor;
    }
    
    bool WouldCreateMatch(Bird bird1, Bird bird2)
    {
        BoardManager boardManager = FindObjectOfType<BoardManager>();
        if (boardManager == null) return false;
        
        Debug.Log($"Checking if swap would create match: Bird1({bird1.xIndex},{bird1.yIndex}) <-> Bird2({bird2.xIndex},{bird2.yIndex})");
        
        // Eşleşme kontrolü yap
        bool createsMatch = boardManager.WouldCreateMatch(bird1, bird2);
        
        Debug.Log($"Would create match: {createsMatch}");
        
        return createsMatch;
    }
    
    void PerformSwap(Bird bird1, Bird bird2)
    {
        // Pozisyonları değiştir
        Vector3 pos1 = bird1.transform.position;
        Vector3 pos2 = bird2.transform.position;
        
        int x1 = bird1.xIndex;
        int y1 = bird1.yIndex;
        int x2 = bird2.xIndex;
        int y2 = bird2.yIndex;
        
        // Animasyonlu swap
        StartCoroutine(AnimatedSwap(bird1, bird2, pos1, pos2, x1, y1, x2, y2));
        
        // Event tetikle
        OnBirdSwap?.Invoke(bird1, bird2);
    }
    
    IEnumerator AnimatedSwap(Bird bird1, Bird bird2, Vector3 pos1, Vector3 pos2, int x1, int y1, int x2, int y2)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        
        Vector3 startPos1 = bird1.transform.position;
        Vector3 startPos2 = bird2.transform.position;
        
        // Hemen pozisyonları güncelle (grid için)
        bird1.xIndex = x2;
        bird1.yIndex = y2;
        bird2.xIndex = x1;
        bird2.yIndex = y1;
        
        Debug.Log($"Swap started: Bird1({x1},{y1}) -> ({x2},{y2}), Bird2({x2},{y2}) -> ({x1},{y1})");
        
        // BoardManager'ı hemen bilgilendir
        BoardManager boardManager = FindObjectOfType<BoardManager>();
        if (boardManager != null)
        {
            boardManager.OnSwapCompleted(bird1, bird2);
        }
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float curve = Mathf.SmoothStep(0f, 1f, progress);
            
            bird1.transform.position = Vector3.Lerp(startPos1, pos2, curve);
            bird2.transform.position = Vector3.Lerp(startPos2, pos1, curve);
            
            yield return null;
        }
        
        // Final pozisyonları ayarla
        bird1.transform.position = pos2;
        bird2.transform.position = pos1;
        
        Debug.Log($"Swap completed: Bird1 at ({bird1.xIndex},{bird1.yIndex}), Bird2 at ({bird2.xIndex},{bird2.yIndex})");
    }
    
    void ReturnToOriginalPosition()
    {
        if (selectedBird != null)
        {
            StartCoroutine(ReturnToPosition(selectedBird, originalPosition));
        }
    }
    
    IEnumerator ReturnToPosition(Bird bird, Vector3 targetPos)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 startPos = bird.transform.position;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float curve = Mathf.SmoothStep(0f, 1f, progress);
            
            bird.transform.position = Vector3.Lerp(startPos, targetPos, curve);
            yield return null;
        }
        
        bird.transform.position = targetPos;
    }
    
    void SetBirdColor(Bird bird, Color color)
    {
        if (bird != null && bird.GetComponent<SpriteRenderer>() != null)
        {
            bird.GetComponent<SpriteRenderer>().color = color;
        }
    }
    
    void ResetBirdColor(Bird bird)
    {
        if (bird != null && bird.GetComponent<SpriteRenderer>() != null)
        {
            bird.GetComponent<SpriteRenderer>().color = originalColor;
        }
    }
    
    // Public getter'lar
    public bool IsDragging() => isDragging;
    public Bird GetSelectedBird() => selectedBird;
    public Bird GetTargetBird() => targetBird;
}