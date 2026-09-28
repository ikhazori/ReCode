using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye el nivel (la cuadricula y el personaje) por codigo a partir de un mapa
/// escrito con texto. No necesita imagenes: todo se dibuja con cuadrados y un circulo.
/// Diseño: marco blanco como el cuadro de dialogo, casillas tipo tablero de circuito,
/// esquinas en la casilla de inicio, un rombo giratorio en la meta y muros bloqueados.
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

    [Header("Diseño del tablero")]
    [Tooltip("Que tan brillantes son las lineas verdes (1 = como el color de arriba, menos = mas tenues).")]
    [Range(0f, 1f)] public float lineBrightness = 0.45f;
    [Tooltip("Grosor de las lineas entre casillas (unidades del mundo).")]
    public float lineThickness = 0.05f;
    [Tooltip("Que tan marcado es el efecto de tablero de ajedrez entre casillas (0 = ninguno).")]
    [Range(0f, 0.3f)] public float checkerStrength = 0.08f;
    [Tooltip("Puntitos en el centro de las casillas libres.")]
    public bool showCellDots = true;
    public Color frameColor = Color.white;
    [Tooltip("Grosor del marco blanco.")]
    public float frameThickness = 0.09f;
    [Tooltip("Espacio negro entre el marco y las casillas.")]
    public float framePadding = 0.14f;

    [Header("Efecto de victoria")]
    [Tooltip("Cuanto dura (segundos). Deberia ser parecido a 'Win Duration' de ScreenEffects.")]
    public float burstDuration = 1.2f;
    public int sparkCount = 28;
    [Tooltip("Color de la onda que recorre las casillas desde la meta.")]
    public Color waveColor = new Color(0.5f, 1f, 0.6f);

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public Vector2Int StartCell { get; private set; }
    public Vector2Int GoalCell { get; private set; }
    public Transform Player { get; private set; }

    private char[,] cells;
    private Transform visualsRoot;
    private SpriteRenderer goalRenderer;
    private Transform goalMark;
    private readonly List<SpriteRenderer> startMarks = new List<SpriteRenderer>();
    private SpriteRenderer[,] cellSr;
    private Color[,] cellBase;

    private static Sprite squareSprite;
    private static Sprite circleSprite;
    private static Sprite ringSprite;

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

        float w = Columns * cellSize;
        float h = Rows * cellSize;
        Vector3 center = new Vector3(gridCenter.x, gridCenter.y, 0f);
        Sprite sq = GetSquare();

        // Marco blanco + fondo negro (igual que el cuadro de dialogo)
        float outer = framePadding + frameThickness;
        CreateRect("FrameBorder", parent, sq, center, new Vector2(w + 2f * outer, h + 2f * outer), frameColor, -3);
        CreateRect("FrameFill", parent, sq, center, new Vector2(w + 2f * framePadding, h + 2f * framePadding), Color.black, -2);

        // Un solo rectangulo con el color de las lineas; las casillas se dibujan encima, un poco mas chicas
        Color dimLine = new Color(lineColor.r * lineBrightness, lineColor.g * lineBrightness, lineColor.b * lineBrightness, 1f);
        CreateRect("Lines", parent, sq, center, new Vector2(w, h), dimLine, 0);

        cellSr = new SpriteRenderer[Columns, Rows];
        cellBase = new Color[Columns, Rows];
        float inner = cellSize - lineThickness;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                Vector3 pos = CellToWorld(new Vector2Int(c, r));
                char ch = cells[c, r];

                Color fill;
                if (ch == 'G') fill = goalColor;
                else if (ch == '#') fill = Color.Lerp(wallColor, Color.black, 0.55f);
                else fill = ((c + r) % 2 == 0) ? cellColor : Color.Lerp(cellColor, lineColor, checkerStrength);

                GameObject cellGo = CreateRect("Cell", parent, sq, pos, Vector2.one * inner, fill, 1);
                cellSr[c, r] = cellGo.GetComponent<SpriteRenderer>();
                cellBase[c, r] = fill;

                if (ch == 'G')
                {
                    goalRenderer = cellSr[c, r];
                }
                else if (ch == '#')
                {
                    BuildWall(parent, pos);
                }
                else if (showCellDots && !(c == StartCell.x && r == StartCell.y))
                {
                    CreateRect("Dot", parent, sq, pos, Vector2.one * (cellSize * 0.07f),
                        new Color(lineColor.r, lineColor.g, lineColor.b, 0.35f), 2);
                }
            }
        }

        BuildStartMarker(parent, CellToWorld(StartCell));
        BuildGoalMark(parent, CellToWorld(GoalCell));

        GameObject player = CreateRect("Player", parent, GetCircle(), CellToWorld(StartCell), Vector2.one * (cellSize * 0.7f), playerColor, 10);
        player.AddComponent<PlayerFX>();
        Player = player.transform;
    }

    // Muro: bloque gris con una "X" oscura (se ve bloqueado)
    private void BuildWall(Transform parent, Vector3 pos)
    {
        Sprite sq = GetSquare();
        CreateRect("WallBlock", parent, sq, pos, Vector2.one * (cellSize * 0.62f), wallColor, 2);

        Color dark = Color.Lerp(wallColor, Color.black, 0.6f);
        GameObject a = CreateRect("WallX1", parent, sq, pos, new Vector2(cellSize * 0.7f, 0.07f), dark, 3);
        a.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        GameObject b = CreateRect("WallX2", parent, sq, pos, new Vector2(cellSize * 0.7f, 0.07f), dark, 3);
        b.transform.rotation = Quaternion.Euler(0f, 0f, -45f);
    }

    // Inicio: cuatro esquinas del color del jugador (parpadean suavemente)
    private void BuildStartMarker(Transform parent, Vector3 pos)
    {
        Sprite sq = GetSquare();
        float half = cellSize * 0.5f - 0.11f;
        float len = cellSize * 0.24f;
        float th = 0.055f;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                Vector3 corner = pos + new Vector3(sx * half, sy * half, 0f);
                Vector3 hPos = corner + new Vector3(-sx * (len / 2f - th / 2f), 0f, 0f);
                Vector3 vPos = corner + new Vector3(0f, -sy * (len / 2f - th / 2f), 0f);
                startMarks.Add(CreateRect("StartH", parent, sq, hPos, new Vector2(len, th), playerColor, 2).GetComponent<SpriteRenderer>());
                startMarks.Add(CreateRect("StartV", parent, sq, vPos, new Vector2(th, len), playerColor, 2).GetComponent<SpriteRenderer>());
            }
        }
    }

    // Meta: rombo blanco con centro oscuro que gira y late
    private void BuildGoalMark(Transform parent, Vector3 pos)
    {
        goalMark = new GameObject("GoalMark").transform;
        goalMark.SetParent(parent, false);
        goalMark.position = pos;

        Sprite sq = GetSquare();
        GameObject outer = CreateRect("DiamondOuter", goalMark, sq, pos, Vector2.one * (cellSize * 0.5f), Color.white, 3);
        outer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        GameObject innerD = CreateRect("DiamondInner", goalMark, sq, pos, Vector2.one * (cellSize * 0.34f), Color.Lerp(goalColor, Color.black, 0.4f), 4);
        innerD.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private GameObject CreateRect(string objName, Transform parent, Sprite sprite, Vector3 pos, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    // Animaciones del tablero: la meta late, el rombo gira, las esquinas de inicio parpadean
    void Update()
    {
        float t = Time.time;

        if (goalRenderer != null)
        {
            float k = (Mathf.Sin(t * 3f) + 1f) / 2f;
            goalRenderer.color = Color.Lerp(goalColor, Color.white, k * 0.35f);
        }

        if (goalMark != null)
        {
            goalMark.rotation = Quaternion.Euler(0f, 0f, t * 40f);
            float s = 1f + Mathf.Sin(t * 4f) * 0.08f;
            goalMark.localScale = new Vector3(s, s, 1f);
        }

        float a = 0.4f + 0.6f * (Mathf.Sin(t * 3f) + 1f) / 2f;
        for (int i = 0; i < startMarks.Count; i++)
        {
            Color c = startMarks[i].color;
            c.a = a;
            startMarks[i].color = c;
        }
    }

    // ---------- Efecto de victoria (lo llama el Runner) ----------

    /// <summary>Chispas que salen de la meta, un anillo que se expande, una onda que recorre las casillas y el jugador salta.</summary>
    public IEnumerator PlayWinBurst()
    {
        Vector3 goalPos = CellToWorld(GoalCell);

        PlayerFX fx = Player != null ? Player.GetComponent<PlayerFX>() : null;
        if (fx != null) fx.Celebrate();

        // Chispas
        int n = Mathf.Max(0, sparkCount);
        GameObject[] sparks = new GameObject[n];
        SpriteRenderer[] sparkSr = new SpriteRenderer[n];
        Vector3[] vel = new Vector3[n];
        float[] spin = new float[n];
        float[] baseSize = new float[n];
        for (int i = 0; i < n; i++)
        {
            baseSize[i] = cellSize * Random.Range(0.08f, 0.18f);
            Color col = Color.Lerp(goalColor, Color.white, Random.Range(0f, 0.8f));
            sparks[i] = CreateRect("Spark", visualsRoot, GetSquare(), goalPos, Vector2.one * baseSize[i], col, 30);
            sparkSr[i] = sparks[i].GetComponent<SpriteRenderer>();
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float spd = Random.Range(1.5f, 5f) * cellSize;
            vel[i] = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * spd;
            spin[i] = Random.Range(-540f, 540f);
        }

        // Anillo
        GameObject ring = CreateRect("Ring", visualsRoot, GetRing(), goalPos, Vector2.one * 0.5f, Color.white, 25);
        SpriteRenderer ringSr = ring.GetComponent<SpriteRenderer>();

        float maxDist = Mathf.Sqrt(Columns * Columns + Rows * Rows);
        float waveTime = burstDuration * 0.75f;

        float t = 0f;
        while (t < burstDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            float k = Mathf.Clamp01(t / burstDuration);

            for (int i = 0; i < n; i++)
            {
                sparks[i].transform.position += vel[i] * dt;
                vel[i] *= Mathf.Max(0f, 1f - 2f * dt); // frenan poco a poco
                sparks[i].transform.Rotate(0f, 0f, spin[i] * dt);
                float s = baseSize[i] * (1f - 0.7f * k);
                sparks[i].transform.localScale = new Vector3(s, s, 1f);
                Color sc = sparkSr[i].color;
                sc.a = 1f - k;
                sparkSr[i].color = sc;
            }

            float ease = 1f - Mathf.Pow(1f - k, 3f);
            ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, cellSize * 7f, ease);
            ringSr.color = new Color(1f, 1f, 1f, (1f - k) * 0.8f);

            // Onda: las casillas se iluminan segun su distancia a la meta
            float front = Mathf.Clamp01(t / waveTime) * (maxDist + 2f);
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (c == GoalCell.x && r == GoalCell.y) continue;
                    float d = Vector2.Distance(new Vector2(c, r), new Vector2(GoalCell.x, GoalCell.y));
                    float intensity = Mathf.Clamp01(1f - Mathf.Abs(d - front) / 1.5f);
                    cellSr[c, r].color = Color.Lerp(cellBase[c, r], waveColor, intensity * 0.7f);
                }
            }

            yield return null;
        }

        // Limpieza
        for (int i = 0; i < n; i++) Destroy(sparks[i]);
        Destroy(ring);
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                if (c == GoalCell.x && r == GoalCell.y) continue;
                cellSr[c, r].color = cellBase[c, r];
            }
        }
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

    // Anillo delgado (para la onda de victoria)
    private static Sprite GetRing()
    {
        if (ringSprite == null)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    tex.SetPixel(x, y, (d <= 1f && d >= 0.92f) ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        return ringSprite;
    }
}