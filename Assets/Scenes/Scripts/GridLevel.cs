using UnityEngine;

/// <summary>
/// Construye el nivel (la cuadricula y el personaje) por codigo a partir de un mapa
/// escrito con texto. No necesita imagenes: todo se dibuja con cuadrados y un circulo.
/// </summary>
public class GridLevel : MonoBehaviour
{
    [Header("Mapa (una linea por fila; la primera linea es la de ARRIBA)")]
    [Tooltip("S = inicio del personaje, G = meta, # = muro, . = casilla libre. Todas las filas deberian tener el mismo largo.")]
    public string[] layout = { "S.......", ".......G", "........" };

    [Header("Tamaño y posicion")]
    [Tooltip("Tamaño de cada casilla en unidades del mundo.")]
    public float cellSize = 1f;
    [Tooltip("Centro de toda la cuadricula en el mundo. Si queda muy arriba o abajo, cambia la Y (con el juego detenido).")]
    public Vector2 gridCenter = new Vector2(0f, 2.7f);

    [Header("Colores")]
    public Color lineColor = new Color(0.1f, 0.8f, 0.2f);
    public Color cellColor = Color.black;
    public Color goalColor = new Color(0.15f, 0.7f, 0.25f);
    public Color wallColor = new Color(0.4f, 0.4f, 0.4f);
    public Color playerColor = new Color(0f, 0.64f, 0.91f);

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public Vector2Int StartCell { get; private set; }
    public Vector2Int GoalCell { get; private set; }
    public Transform Player { get; private set; }

    private char[,] cells;
    private Transform visualsRoot;
    private static Sprite squareSprite;
    private static Sprite circleSprite;

    void Awake()
    {
        ParseLayout();
        BuildVisuals();
    }

    private void ParseLayout()
    {
        Rows = layout.Length;
        Columns = 0;
        foreach (string row in layout) Columns = Mathf.Max(Columns, row.Length);

        cells = new char[Columns, Rows];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                char ch = c < layout[r].Length ? layout[r][c] : '.';
                if (ch == 'S') { StartCell = new Vector2Int(c, r); ch = '.'; }
                else if (ch == 'G') { GoalCell = new Vector2Int(c, r); }
                cells[c, r] = ch;
            }
        }
    }

    private void BuildVisuals()
    {
        Transform parent = new GameObject("LevelVisuals").transform;
        parent.SetParent(transform, false);
        visualsRoot = parent;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                Vector3 pos = CellToWorld(new Vector2Int(c, r));
                char ch = cells[c, r];

                // Borde verde de la casilla + relleno encima
                CreateSprite("Line", parent, GetSquare(), pos, cellSize, lineColor, 0);
                Color fill = ch == 'G' ? goalColor : (ch == '#' ? wallColor : cellColor);
                CreateSprite("Cell", parent, GetSquare(), pos, cellSize - 0.06f, fill, 1);
            }
        }

        GameObject player = CreateSprite("Player", parent, GetCircle(), CellToWorld(StartCell), cellSize * 0.7f, playerColor, 10);
        Player = player.transform;
    }

    private GameObject CreateSprite(string objName, Transform parent, Sprite sprite, Vector3 pos, float size, Color color, int order)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size, size, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    // ---------- Utilidades que usa el Runner ----------

    public Vector3 CellToWorld(Vector2Int cell)
    {
        float x = gridCenter.x + (cell.x - (Columns - 1) / 2f) * cellSize;
        float y = gridCenter.y - (cell.y - (Rows - 1) / 2f) * cellSize;
        return new Vector3(x, y, 0f);
    }

    public bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Columns && cell.y < Rows;
    }

    public bool IsWalkable(Vector2Int cell)
    {
        return InBounds(cell) && cells[cell.x, cell.y] != '#';
    }

    public void ResetPlayer()
    {
        Player.position = CellToWorld(StartCell);
    }

    /// <summary>Muestra u oculta toda la cuadricula y el personaje.</summary>
    public void SetVisible(bool visible)
    {
        visualsRoot.gameObject.SetActive(visible);
    }

    // ---------- Sprites generados por codigo ----------

    private static Sprite GetSquare()
    {
        if (squareSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            squareSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        return squareSprite;
    }

    private static Sprite GetCircle()
    {
        if (circleSprite == null)
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    tex.SetPixel(x, y, (dx * dx + dy * dy <= radius * radius) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return circleSprite;
    }
}