using System.Collections;
using UnityEngine;

public class Bird : MonoBehaviour
{
    [Header("Identity")]
    public int birdType = 1;   // 1..5 (prefab tipi)
    public int colorType = 0;  // Renk ID'si (0 = Red, 1 = Pink, 2 = Blue, 3 = Green, 4 = Yellow)

    [Header("Runtime")]
    public int xIndex = -1;    // Board'daki x koordinatı
    public int yIndex = -1;    // Board'daki y koordinatı
    [HideInInspector] public bool isMatched = false;
    [HideInInspector] public bool isMoving = false;

    [Header("Animation Settings")]
    public float flyAnimDuration = 0.6f;
    public float moveAnimDuration = 0.3f;
    public float fallAnimDuration = 0.5f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve fallCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Animator animator;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    private Vector3 targetPosition;
    private Coroutine moveCoroutine;

    void Awake()
    {
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // Eğer pozisyon atanmamışsa, mevcut pozisyondan hesapla
        if (xIndex == -1 || yIndex == -1)
        {
            CalculateIndicesFromPosition();
        }
    }

    void CalculateIndicesFromPosition()
    {
        BoardManager boardManager = FindObjectOfType<BoardManager>();
        if (boardManager != null)
        {
            xIndex = Mathf.RoundToInt(transform.position.x / boardManager.offsetX);
            yIndex = Mathf.RoundToInt(transform.position.y / boardManager.offsetY);
        }
    }

    // Kuş uçma animasyonu oynat ve yok et
    public void PlayFlyAndDestroy()
    {
        if (!isMatched)
        {
            isMatched = true;
            StartCoroutine(FlyAndDestroy());
        }
    }

    private IEnumerator FlyAndDestroy()
    {
        // Collider'ı devre dışı bırak
        if (col != null) 
            col.enabled = false;

        // Uçma animasyonunu tetikle
        if (animator != null)
        {
            animator.SetTrigger("Fly");
        }
        else
        {
            // Animator yoksa basit bir scale animasyonu
            StartCoroutine(SimpleFlyAnimation());
        }

        yield return new WaitForSeconds(flyAnimDuration);

        // Nesneyi yok et
        Destroy(gameObject);
    }

    private IEnumerator SimpleFlyAnimation()
    {
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < flyAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / flyAnimDuration;
            
            // Scale ve rotation ile basit uçma efekti
            transform.localScale = originalScale * (1f + progress * 0.5f);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(progress * Mathf.PI * 4) * 10f);
            
            yield return null;
        }

        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
    }

    // Kuşu belirli bir pozisyona hareket ettir
    public void MoveToPosition(Vector3 newPosition, int newX, int newY)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        xIndex = newX;
        yIndex = newY;
        targetPosition = newPosition;
        
        moveCoroutine = StartCoroutine(MoveToTarget());
    }

    // Kuşu yumuşak düşme animasyonu ile hareket ettir
    public void FallToPosition(Vector3 newPosition, int newX, int newY)
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        xIndex = newX;
        yIndex = newY;
        targetPosition = newPosition;
        
        moveCoroutine = StartCoroutine(FallToTarget());
    }

    private IEnumerator MoveToTarget()
    {
        isMoving = true;
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < moveAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / moveAnimDuration;
            float curveValue = moveCurve.Evaluate(progress);
            
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
        moveCoroutine = null;
    }

    private IEnumerator FallToTarget()
    {
        isMoving = true;
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < fallAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fallAnimDuration;
            float curveValue = fallCurve.Evaluate(progress);
            
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            // Hafif sallanma efekti
            float shake = Mathf.Sin(progress * Mathf.PI * 4) * 0.1f;
            transform.position += Vector3.right * shake;
            
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
        moveCoroutine = null;
    }

    // Kuş tipini rastgele ayarla (yeni spawn edilenler için)
    public void SetRandomType()
    {
        if (spriteRenderer != null)
        {
            birdType = Random.Range(1, 6); // 1-5 arası
            colorType = Random.Range(0, 5); // 0-4 arası
        }
    }

    // Kuş tipini manuel olarak ayarla
    public void SetBirdType(int type, int color)
    {
        birdType = type;
        colorType = color;
    }

    // Kuşun eşleşip eşleşmediğini kontrol et
    public bool IsMatched()
    {
        return isMatched;
    }

    // Kuşun hareket edip etmediğini kontrol et
    public bool IsMoving()
    {
        return isMoving;
    }

    // Debug için kuş bilgilerini yazdır
    [ContextMenu("Debug Bird Info")]
    void DebugBirdInfo()
    {
        Debug.Log($"Bird at ({xIndex}, {yIndex}): Type={birdType}, Color={colorType}, Matched={isMatched}, Moving={isMoving}");
    }

    // Mouse tıklaması için (InputManager tarafından kullanılır)
    void OnMouseDown()
    {
        if (!isMatched && !isMoving)
        {
            Debug.Log($"Clicked bird at ({xIndex}, {yIndex})");
            // InputManager bu eventi yakalayacak
        }
    }

    // Drag & drop için ek özellikler
    public void SetDragState(bool isDragging)
    {
        if (isDragging)
        {
            // Sürükleme sırasında collider'ı devre dışı bırak
            if (col != null) col.enabled = false;
        }
        else
        {
            // Sürükleme bittiğinde collider'ı tekrar aktif et
            if (col != null) col.enabled = true;
        }
    }

    // Kuşun seçilebilir olup olmadığını kontrol et
    public bool IsSelectable()
    {
        return !isMatched && !isMoving && gameObject.activeInHierarchy;
    }

    // Gizmos ile pozisyon gösterimi (Scene view'da)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        
        if (xIndex >= 0 && yIndex >= 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.7f, 0.1f);
        }
    }
}
