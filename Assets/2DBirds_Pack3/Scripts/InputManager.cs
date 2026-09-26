using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    public float dragThreshold = 0.5f;
    public LayerMask birdLayerMask = -1;
    public float dragScale = 1.12f;
    public Color dragColor = Color.white;
    public Color validDropColor = Color.green;
    public Color invalidDropColor = Color.red;
    public Action<Bird, Bird> OnBirdSwap;
    public event Action Selected;
    BoardManager board;
    Camera mainCamera;
    Bird selectedBird;
    Vector2 startScreen;
    Vector3 originalScale;
    int finger = -1;
    bool pointerDown;
    public void Configure(BoardManager boardManager, Camera camera)
    {
        board = boardManager;
        mainCamera = camera;
    }
    void Update()
    {
        if (board == null || mainCamera == null || !board.CanInteract) { CancelSelection(); return; }
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && finger < 0)
                {
                    if (BlockedByUI(touch.fingerId)) continue;
                    PointerDown(touch.position);
                    finger = touch.fingerId;
                }
                if (touch.fingerId != finger) continue;
                if (touch.phase == TouchPhase.Canceled) CancelSelection();
                else if (touch.phase == TouchPhase.Ended) { PointerUp(touch.position); finger = -1; }
            }
            return;
        }
        if (finger >= 0) { CancelSelection(); return; }
        if (Input.GetMouseButtonDown(0) && !BlockedByUI(-1)) PointerDown(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) PointerUp(Input.mousePosition);
    }
    bool BlockedByUI(int id) => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id);
    Vector3 World(Vector2 position)
    {
        Vector3 p = mainCamera.ScreenToWorldPoint(new Vector3(position.x, position.y, -mainCamera.transform.position.z));
        p.z = 0;
        return p;
    }
    Bird At(Vector2 position)
    {
        var hit = Physics2D.OverlapPoint(World(position), birdLayerMask);
        return hit != null ? hit.GetComponent<Bird>() : null;
    }
    public void PointerDown(Vector2 position)
    {
        if (board == null || mainCamera == null || !board.CanInteract) return;
        Bird bird = At(position);
        if (bird == null || !bird.IsSelectable()) { CancelSelection(); return; }
        if (selectedBird != null && selectedBird != bird)
        {
            Bird previous = selectedBird;
            CancelSelection();
            if (board.RequestSwap(previous, bird)) { OnBirdSwap?.Invoke(previous, bird); return; }
        }
        if (selectedBird == null)
        {
            selectedBird = bird;
            originalScale = bird.transform.localScale;
            bird.transform.localScale = originalScale * dragScale;
            Selected?.Invoke();
        }
        startScreen = position;
        pointerDown = true;
    }
    public void PointerUp(Vector2 position)
    {
        if (board == null || !board.CanInteract) { CancelSelection(); return; }
        if (!pointerDown || selectedBird == null) return;
        pointerDown = false;
        Vector3 delta = World(position) - World(startScreen);
        if (delta.magnitude < dragThreshold) return;
        int dx = 0, dy = 0;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) dx = delta.x > 0 ? 1 : -1;
        else dy = delta.y > 0 ? 1 : -1;
        Bird a = selectedBird;
        GameObject target = board.GetGridBird(a.xIndex + dx, a.yIndex + dy);
        CancelSelection();
        if (target == null) return;
        Bird b = target.GetComponent<Bird>();
        if (board.RequestSwap(a, b)) OnBirdSwap?.Invoke(a, b);
    }
    public void CancelSelection()
    {
        if (selectedBird != null) selectedBird.transform.localScale = originalScale;
        selectedBird = null;
        finger = -1;
        pointerDown = false;
    }
    void OnDisable() => CancelSelection();
    public bool IsDragging() => pointerDown;
    public Bird GetSelectedBird() => selectedBird;
    public Bird GetTargetBird() => null;
}
