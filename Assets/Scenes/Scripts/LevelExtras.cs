using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pone objetos EXTRA sobre los niveles que ya tienes, sin tocar sus mapas de texto:
///   - Fragmento: pergamino/chip de codigo que se recoge al pasar por su casilla.
///   - Terminal:  objeto interactivo. Al llegar a su casilla la secuencia se detiene un
///                momento, flota "Presiona E para leer" y, si pulsas E, se abre un
///                panel educativo (pseudocodigo). Si no pulsas E, la secuencia sigue sola.
///
/// Cada objeto se ubica por (capitulo, nivel, columna, fila). Capitulo y nivel empiezan
/// en 0. Columna 0 = izquierda, fila 0 = ARRIBA (igual que el mapa de texto del nivel).
/// Todo se edita desde la lista "Placements" del Inspector.
///
/// Uso: ponlo en el mismo objeto que LevelManager (por ejemplo "Level"). No necesita nada mas.
/// </summary>
public class LevelExtras : MonoBehaviour
{
    public enum Kind { Fragment, Terminal }

    [System.Serializable]
    public class Placement
    {
        [Tooltip("Etiqueta solo para ti (no se usa en el juego).")]
        public string note;
        [Tooltip("Capitulo (0 = Prologo, 1 = Capitulo 1...).")]
        public int chapter;
        [Tooltip("Nivel dentro del capitulo (0 = el primero).")]
        public int level;
        [Tooltip("Columna (0 = izquierda).")]
        public int column;
        [Tooltip("Fila (0 = ARRIBA).")]
        public int row;
        public Kind kind;
        [Tooltip("SOLO FRAGMENTO: algoritmo, depurar, bucle, booleano o funcion.")]
        public string fragmentId;
        [Tooltip("SOLO TERMINAL: cada elemento es una pagina del panel (Z/E/Enter/Espacio para avanzar). Maximo ~4 lineas por pagina.")]
        [TextArea(2, 6)] public string[] terminalPages;
    }

    [Header("Referencias (se buscan solas)")]
    public GridLevel level;
    public DialogueController dialogue;
    public Canvas canvas;

    [Header("Interaccion con terminales")]
    public KeyCode interactKey = KeyCode.E;
    public string terminalPrompt = "Presiona E para leer";
    [Tooltip("Cuanto espera la secuencia a que pulses E. Si no pulsas nada, sigue sola.")]
    public float terminalWaitSeconds = 3.5f;

    [Header("Apariencia")]
    public Color terminalColor = new Color(0.4f, 1f, 0.9f);
    [Tooltip("Tamaño del fragmento respecto a la casilla.")]
    [Range(0.2f, 1f)] public float fragmentSize = 0.55f;
    [Tooltip("Tamaño de la terminal respecto a la casilla.")]
    [Range(0.2f, 1f)] public float terminalSize = 0.6f;

    [Header("Objetos (editables)")]
    public List<Placement> placements = new List<Placement>
    {
        // ---------------- Nivel 1: Primeros pasos (8x3) ----------------
        new Placement { note = "Terminal 1: algoritmo", chapter = 0, level = 0, column = 2, row = 0, kind = Kind.Terminal,
            terminalPages = new[]
            {
                "> TERMINAL 01: ALGORITMO\nUn algoritmo es una lista de pasos, en orden, para resolver un problema.",
                "Tu primer algoritmo:\nINICIO\n  mover(derecha) x7\n  mover(abajo)\nFIN",
                "Cada flecha que armas es una instrucción.\nPresiona R y la máquina las ejecuta una por una."
            } },
        new Placement { note = "Fragmento ALGORITMO", chapter = 0, level = 0, column = 4, row = 0, kind = Kind.Fragment, fragmentId = "algoritmo" },

        // ---------------- Nivel 2: Paredes (5x5) ----------------
        new Placement { note = "Fragmento DEPURAR", chapter = 0, level = 1, column = 2, row = 2, kind = Kind.Fragment, fragmentId = "depurar" },

        // ---------------- Nivel 3: Hielo (5x4) ----------------
        new Placement { note = "Fragmento BUCLE (sobre el hielo)", chapter = 0, level = 2, column = 2, row = 2, kind = Kind.Fragment, fragmentId = "bucle" },

        // ---------------- Nivel 4: Botón y puerta (4x3) ----------------
        new Placement { note = "Terminal 2: booleanos", chapter = 0, level = 3, column = 2, row = 0, kind = Kind.Terminal,
            terminalPages = new[]
            {
                "> TERMINAL 02: BOOLEANOS\nUn booleano solo puede valer VERDADERO o FALSO.",
                "puerta_abierta = FALSO\nSI pisas el botón ENTONCES\n  puerta_abierta = VERDADERO\nFIN SI",
                "Cada vez que pisas el botón, el valor se invierte:\npuerta_abierta = NO puerta_abierta"
            } },
        new Placement { note = "Fragmento BOOLEANO (detras de la meta: pasa de largo y vuelve)", chapter = 0, level = 3, column = 1, row = 2, kind = Kind.Fragment, fragmentId = "booleano" },

        // ---------------- Nivel 5: Teletransporte (5x3) ----------------
        new Placement { note = "Fragmento FUNCIÓN", chapter = 0, level = 4, column = 2, row = 2, kind = Kind.Fragment, fragmentId = "funcion" },

        // ---------------- Nivel 6: A prueba (5x5) ----------------
        new Placement { note = "Terminal 3: eficiencia", chapter = 0, level = 5, column = 4, row = 2, kind = Kind.Terminal,
            terminalPages = new[]
            {
                "> TERMINAL 03: EFICIENCIA\nDos programas pueden dar el mismo resultado con distinta cantidad de pasos.",
                "REPETIR 4 VECES\n  mover(derecha)\nREPETIR 4 VECES\n  mover(abajo)",
                "8 instrucciones, ni una más.\nUn buen algoritmo no desperdicia pasos."
            } },
    };

    private class Spawned
    {
        public Placement p;
        public Vector2Int cell;
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 basePos;
        public Vector3 baseScale;
        public Color baseColor;
        public float phase;
        public bool taken;
    }

    private readonly Dictionary<Vector2Int, Spawned> fragments = new Dictionary<Vector2Int, Spawned>();
    private readonly Dictionary<Vector2Int, Spawned> terminals = new Dictionary<Vector2Int, Spawned>();

    void Awake()
    {
        if (level == null) level = FindFirstObjectByType<GridLevel>();
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueController>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
    }

    // ---------------- Construccion (la llama LevelManager al cargar cada nivel) ----------------

    public void BuildFor(int chapterIndex, int levelIndex)
    {
        // Los objetos viejos se destruyeron junto con el tablero anterior: solo olvidamos las referencias.
        fragments.Clear();
        terminals.Clear();
        FragmentInventory.DiscardPending();

        if (level == null) level = FindFirstObjectByType<GridLevel>();
        if (level == null || level.VisualsRoot == null) return;

        foreach (Placement p in placements)
        {
            if (p == null || p.chapter != chapterIndex || p.level != levelIndex) continue;

            Vector2Int cell = new Vector2Int(p.column, p.row);
            if (!IsFreeCell(cell, p))
            {
                Debug.LogWarning("[LevelExtras] '" + p.note + "' no se puede poner en (" + p.column + "," + p.row +
                                 "): esta fuera del mapa, es muro/puerta/inicio/meta/boton/teletransportador, o ya hay otro objeto ahi.");
                continue;
            }

            if (p.kind == Kind.Fragment)
            {
                FragmentInventory.Fragment f = FragmentInventory.Get(p.fragmentId);
                if (f == null)
                {
                    Debug.LogWarning("[LevelExtras] '" + p.note + "': fragmentId '" + p.fragmentId + "' no existe.");
                    continue;
                }
                if (FragmentInventory.IsCommitted(f.id)) continue; // ya lo tienes guardado: no vuelve a aparecer
                fragments[cell] = Spawn(p, cell, PixelSprites.Fragment(), f.color, fragmentSize, "Fragment_" + f.id);
            }
            else
            {
                terminals[cell] = Spawn(p, cell, PixelSprites.Terminal(), terminalColor, terminalSize, "Terminal");
            }
        }
    }

    private bool IsFreeCell(Vector2Int cell, Placement p)
    {
        if (!level.InBounds(cell)) return false;
        if (!level.IsWalkable(cell)) return false; // muro o puerta cerrada
        if (cell == level.StartCell || cell == level.GoalCell) return false;
        if (level.IsButton(cell) || level.IsTeleporter(cell)) return false;
        if (fragments.ContainsKey(cell) || terminals.ContainsKey(cell)) return false;
        return true;
    }

    private Spawned Spawn(Placement p, Vector2Int cell, Sprite sprite, Color color, float sizeInCells, string objName)
    {
        float s = level.cellSize * sizeInCells;
        GameObject go = new GameObject(objName);
        go.transform.SetParent(level.VisualsRoot, false);
        go.transform.position = level.CellToWorld(cell);
        go.transform.localScale = new Vector3(s, s, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 6; // encima de las casillas y de los iconos, debajo del jugador (10)

        return new Spawned
        {
            p = p, cell = cell, tr = go.transform, sr = sr,
            basePos = go.transform.position, baseScale = go.transform.localScale,
            baseColor = color, phase = Random.Range(0f, 6.28f)
        };
    }

    // ---------------- Animacion: flotan suavemente para que se noten ----------------

    void Update()
    {
        float t = Time.time;
        foreach (Spawned s in fragments.Values)
        {
            if (s.tr == null || s.taken) continue;
            float k = Mathf.Sin(t * 3f + s.phase);
            s.tr.position = s.basePos + new Vector3(0f, k * 0.04f * level.cellSize, 0f);
            s.tr.localScale = s.baseScale * (1f + k * 0.06f);
        }
        foreach (Spawned s in terminals.Values)
        {
            if (s.tr == null) continue;
            float a = 0.75f + 0.25f * Mathf.Sin(t * 4f + s.phase);
            Color c = s.baseColor; c.a = a;
            s.sr.color = c;
        }
    }

    // ---------------- Fragmentos ----------------

    /// <summary>El jugador llego a 'cell': si hay un fragmento lo recoge (pendiente hasta ganar).</summary>
    public bool TryCollect(Vector2Int cell)
    {
        Spawned s;
        if (!fragments.TryGetValue(cell, out s) || s.taken) return false;
        if (!FragmentInventory.AddPending(s.p.fragmentId)) return false;

        s.taken = true;
        SoundFX.Pickup();
        StartCoroutine(PopOut(s));
        return true;
    }

    private IEnumerator PopOut(Spawned s)
    {
        float t = 0f;
        const float dur = 0.25f;
        while (t < dur && s.taken && s.tr != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            s.tr.localScale = s.baseScale * (1f + k * 0.8f);
            Color c = s.baseColor; c.a = 1f - k;
            s.sr.color = c;
            yield return null;
        }
        if (s.taken && s.tr != null) s.tr.gameObject.SetActive(false);
    }

    /// <summary>El rebobinado paso por esta casilla: si el fragmento estaba pendiente, vuelve a aparecer.</summary>
    public bool UndoCollectAt(Vector2Int cell)
    {
        Spawned s;
        if (!fragments.TryGetValue(cell, out s) || !s.taken) return false;
        if (!FragmentInventory.RemovePending(s.p.fragmentId)) return false;
        Restore(s);
        return true;
    }

    private void Restore(Spawned s)
    {
        s.taken = false;
        if (s.tr == null) return;
        s.tr.gameObject.SetActive(true);
        s.tr.localScale = s.baseScale;
        s.sr.color = s.baseColor;
    }

    /// <summary>Ganaste el nivel: los fragmentos pendientes se guardan. Devuelve cuantos fueron.</summary>
    public int CommitRun()
    {
        return FragmentInventory.CommitPending();
    }

    /// <summary>Chocaste o no llegaste: los pendientes se devuelven y reaparecen en el tablero.</summary>
    public void DiscardRun()
    {
        FragmentInventory.DiscardPending();
        foreach (Spawned s in fragments.Values)
        {
            if (s.taken && !FragmentInventory.IsCommitted(s.p.fragmentId)) Restore(s);
        }
    }

    // ---------------- Terminales ----------------

    public bool HasTerminal(Vector2Int cell)
    {
        return terminals.ContainsKey(cell);
    }

    /// <summary>
    /// La secuencia llego a una terminal: flota el indicador y espera unos segundos.
    /// Si pulsas E abre el panel educativo; si no, la secuencia continua sola.
    /// </summary>
    public IEnumerator PlayTerminal(Vector2Int cell)
    {
        Spawned t;
        if (!terminals.TryGetValue(cell, out t)) yield break;

        PromptBubble bubble = PromptBubble.Get(canvas);
        if (bubble != null)
            bubble.ShowAtWorld(t.tr.position + Vector3.up * level.cellSize * 0.4f, terminalPrompt);
        SoundFX.Blip();

        bool pressed = false;
        float timer = 0f;
        while (timer < terminalWaitSeconds)
        {
            if (Time.timeScale > 0f) // en pausa no cuenta ni lee la tecla
            {
                timer += Time.unscaledDeltaTime;
                if (Input.GetKeyDown(interactKey)) { pressed = true; break; }
            }
            yield return null;
        }

        if (bubble != null) bubble.Hide();
        if (!pressed) yield break;

        SoundFX.Confirm();
        if (dialogue != null && t.p.terminalPages != null && t.p.terminalPages.Length > 0)
            yield return dialogue.PlayTerminal(t.p.terminalPages);
    }
}
