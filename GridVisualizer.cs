using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 6;
    public int height = 6;
    public float offsetX = 2f;
    public float offsetY = 2f;
    
    [Header("Visualization")]
    public bool showGrid = true;
    public Color gridColor = Color.white;
    public Color occupiedColor = Color.green;
    public Color emptyColor = Color.red;
    public float lineWidth = 0.1f;
    
    [Header("Debug Info")]
    public bool showCoordinates = true;
    public bool showGridState = true;
    
    private BoardManager boardManager;
    
    void Start()
    {
        boardManager = FindObjectOfType<BoardManager>();
        if (boardManager != null)
        {
            width = boardManager.width;
            height = boardManager.height;
            offsetX = boardManager.offsetX;
            offsetY = boardManager.offsetY;
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showGrid) return;
        
        DrawGridLines();
        
        if (Application.isPlaying && showGridState)
        {
            DrawGridState();
        }
        
        if (showCoordinates)
        {
            DrawCoordinates();
        }
    }
    
    void DrawGridLines()
    {
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
    }
    
    void DrawGridState()
    {
        if (boardManager == null) return;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x * offsetX, y * offsetY, 0);
                
                // Grid durumunu kontrol et
                bool isOccupied = boardManager.IsGridOccupied(x, y);
                
                if (isOccupied)
                {
                    Gizmos.color = occupiedColor;
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                }
                else
                {
                    Gizmos.color = emptyColor;
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
                }
            }
        }
    }
    
    void DrawCoordinates()
    {
        Gizmos.color = Color.yellow;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x * offsetX, y * offsetY, 0);
                Vector3 labelPos = pos + Vector3.up * 0.4f;
                
                // Koordinat göstergesi
                Gizmos.DrawWireCube(labelPos, Vector3.one * 0.15f);
            }
        }
    }
    
    // Public fonksiyonlar
    public void ToggleGrid()
    {
        showGrid = !showGrid;
        Debug.Log($"Grid visualization: {(showGrid ? "ON" : "OFF")}");
    }
    
    public void ToggleCoordinates()
    {
        showCoordinates = !showCoordinates;
        Debug.Log($"Coordinate display: {(showCoordinates ? "ON" : "OFF")}");
    }
    
    public void ToggleGridState()
    {
        showGridState = !showGridState;
        Debug.Log($"Grid state display: {(showGridState ? "ON" : "OFF")}");
    }
    
    public void ChangeGridColor()
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
}
