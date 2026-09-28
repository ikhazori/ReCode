using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Ejecuta la secuencia de instrucciones armada por el jugador: mueve al personaje
/// casilla por casilla. Si choca con un muro o se sale del grid, dispara el efecto
/// de choque y reinicia el nivel. Se conecta solo a los demas scripts.
/// </summary>
public class LevelRunner : MonoBehaviour
{
    [Header("Referencias (se buscan solas si las dejas vacias)")]
    public GridLevel level;
    public CommandQueueUI queue;
    public ScreenEffects effects;
    public AnimatedBackground background;
    [Tooltip("Opcional. Si existe en la escena, se reproduce su dialogo al ganar.")]
    public DialogueController dialogue;
    [Tooltip("Opcional. De aqui salen las frases del guia de cada nivel.")]
    public LevelManager levelManager;

    [Header("Rebobinado (cuando la secuencia termina y no llegaste a la meta)")]
    [Tooltip("Cuanto tarda cada paso al retroceder. Mas bajo = mas rapido (efecto camara rapida).")]
    public float rewindStepDuration = 0.08f;
    [Tooltip("Cuanto tardan en subir/bajar el blanco y negro.")]
    public float rewindFadeDuration = 0.2f;
    [Tooltip("Pausa ya de vuelta en el inicio, antes de que suene el jingle.")]
    public float rewindHoldAtStart = 0.3f;

    [Header("Al ganar")]
    [Tooltip("Oculta la cuadricula mientras habla el personaje, para que la escena quede limpia.")]
    public bool hideGridDuringDialogue = true;

    [Header("Tiempos")]
    [Tooltip("Cuanto tarda el personaje en avanzar una casilla (segundos).")]
    public float stepDuration = 0.3f;
    [Tooltip("Multiplica la duracion del paso mientras te deslizas por hielo (menor = mas rapido).")]
    public float iceSlideSpeedFactor = 0.55f;
    [Tooltip("Pausa antes de reiniciar cuando termina la secuencia (sin chocar).")]
    public float pauseBeforeReset = 0.8f;

    [Header("Eventos")]
    public UnityEvent OnLevelComplete;

    /// <summary>True mientras se esta ejecutando una secuencia (moviendose, chocando, ganando o rebobinando).</summary>
    public bool IsBusy => running;

    private bool running;
    private Vector2Int currentCell;
    private bool stepCrashed;
    private readonly List<Vector2Int> path = new List<Vector2Int>();

    void Start()
    {
        if (level == null) level = FindFirstObjectByType<GridLevel>();
        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        if (effects == null) effects = FindFirstObjectByType<ScreenEffects>();
        if (background == null) background = FindFirstObjectByType<AnimatedBackground>();
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueController>();
        if (levelManager == null) levelManager = FindFirstObjectByType<LevelManager>();

        currentCell = level.StartCell;
        queue.OnRunRequested.AddListener(Run);
    }

    public void Run()
    {
        if (running) return;
        StartCoroutine(RunRoutine());
    }

    private IEnumerator RunRoutine()
    {
        running = true;
        queue.Locked = true;

        List<Direction> instructions = new List<Direction>(queue.Instructions);
        currentCell = level.StartCell;
        level.ResetPlayer();
        level.ClearVisited();

        path.Clear();
        path.Add(currentCell);
        level.MarkVisited(currentCell);

        bool crashed = false;

        foreach (Direction dir in instructions)
        {
            stepCrashed = false;
            yield return ExecuteInstruction(dir);
            if (stepCrashed) { crashed = true; break; }
        }

        if (crashed)
        {
            SoundFX.Crash();
            Coroutine burst = StartCoroutine(level.PlayCrashBurst(level.Player.position));
            if (effects != null) yield return effects.PlayCrashEffect();
            yield return burst;
        }
        else if (currentCell == level.GoalCell)
        {
            Debug.Log("¡Nivel completado!");
            OnLevelComplete?.Invoke();

            // Destello verde suave de victoria
            SoundFX.Win();
            Coroutine burst = StartCoroutine(level.PlayWinBurst()); // chispas, anillo y onda en la cuadricula
            if (effects != null) yield return effects.PlayWinEffect();
            yield return burst;

            if (dialogue != null)
            {
                queue.ClearInstructions(); // quita los iconos para que no tapen el texto
                if (hideGridDuringDialogue) level.SetVisible(false);

                // Frases propias de este nivel; si no tiene, usa las del DialogueController
                string[] guideLines = levelManager != null ? levelManager.GetCurrentGuideLines() : null;
                if (guideLines != null && guideLines.Length > 0)
                    yield return dialogue.PlayDialogue(guideLines);
                else
                    yield return dialogue.PlayWinDialogue();

                level.SetVisible(true);
            }
            else
            {
                yield return new WaitForSeconds(pauseBeforeReset);
            }
        }
        else
        {
            Debug.Log("La secuencia termino pero no llegaste a la meta.");
            yield return PlayRewind();
        }

        ResetLevel();
        queue.Locked = false;
        running = false;
    }

    /// <summary>
    /// Rebobina: la pantalla se pone en blanco y negro con estatica tipo VHS (y su
    /// "shhh"), el fondo se desplaza al reves y mas rapido, y el personaje retrocede
    /// por el mismo camino que recorrio -cada paso un poco mas rapido que el anterior,
    /// como una cinta acelerando- borrando las casillas visitadas a su paso. Justo al
    /// llegar al inicio hay un destello blanco bien rapido ("aqui no paso nada"), y
    /// despues suena un pequeño jingle (como un punto de guardado) antes de que todo
    /// vuelva a la normalidad.
    /// </summary>
    private IEnumerator PlayRewind()
    {
        yield return RewindEffect.Fade(1f, rewindFadeDuration);
        if (background != null) background.BeginRewindScroll();
        if (effects != null) effects.SetRewindStatic(true);

        float duration = rewindStepDuration;
        for (int i = path.Count - 1; i > 0; i--)
        {
            Vector3 from = level.CellToWorld(path[i]);
            Vector3 to = level.CellToWorld(path[i - 1]);

            bool arriving = (i == 1); // este es el ultimo paso: llega justo al inicio
            if (arriving && effects != null) StartCoroutine(effects.PlayRewindArrivalFlash());

            SoundFX.RewindStep();
            yield return MoveTo(from, to, duration);
            level.UnmarkVisited(path[i]);

            duration = Mathf.Max(rewindStepDuration * 0.35f, duration * 0.85f); // se acelera paso a paso
        }

        if (effects != null) effects.SetRewindStatic(false);
        if (background != null) background.EndRewindScroll();
        yield return new WaitForSeconds(rewindHoldAtStart);
        SoundFX.Save();
        yield return RewindEffect.Fade(0f, rewindFadeDuration);
    }

    private void ResetLevel()
    {
        currentCell = level.StartCell;
        level.ResetPlayer();
        level.ShowPlayer();
        level.ClearVisited();
        queue.ClearInstructions();
    }

    /// <summary>
    /// Mueve al jugador una casilla en la direccion 'dir'. Si esa casilla es hielo, sigue
    /// deslizandose solo (sin gastar otra instruccion) hasta chocar o salir del hielo.
    /// Si pisa un boton, abre/cierra las puertas. Si pisa un teletransportador, salta al
    /// otro. Si choca contra un muro/puerta cerrada/borde, marca stepCrashed = true.
    /// </summary>
    private IEnumerator ExecuteInstruction(Direction dir)
    {
        Vector2Int delta = ToDelta(dir);

        while (true)
        {
            Vector2Int next = currentCell + delta;
            Vector3 from = level.CellToWorld(currentCell);

            if (!level.IsWalkable(next))
            {
                // Avanza un poquito hacia el muro/puerta/borde y choca
                Vector3 bump = Vector3.Lerp(from, level.CellToWorld(next), 0.4f);
                yield return MoveTo(from, bump, stepDuration * 0.5f);
                stepCrashed = true;
                yield break;
            }

            bool wasSliding = level.IsIce(currentCell);
            float duration = wasSliding ? stepDuration * iceSlideSpeedFactor : stepDuration;

            SoundFX.Step();
            yield return MoveTo(from, level.CellToWorld(next), duration);
            currentCell = next;
            path.Add(currentCell);
            level.MarkVisited(currentCell);

            if (level.IsButton(currentCell))
            {
                SoundFX.Click();
                level.PressButton(currentCell);
            }

            if (level.IsTeleporter(currentCell))
            {
                Vector2Int dest = level.GetTeleportTarget(currentCell);
                yield return TeleportTo(dest);
                currentCell = dest;
                path.Add(currentCell);
                level.MarkVisited(currentCell);
            }

            if (!level.IsIce(currentCell)) yield break; // se quedo quieto: instruccion terminada
            // si sigue en hielo, se sigue deslizando en la misma direccion (vuelve al while)
        }
    }

    /// <summary>Pequeño "pop" de encogerse/crecer al teletransportarse.</summary>
    private IEnumerator TeleportTo(Vector2Int dest)
    {
        SoundFX.Teleport();
        Transform p = level.Player;
        Vector3 baseScale = p.localScale;
        float t = 0f;
        const float half = 0.1f;

        while (t < half)
        {
            t += Time.deltaTime;
            p.localScale = baseScale * (1f - Mathf.Clamp01(t / half));
            yield return null;
        }

        p.position = level.CellToWorld(dest);

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            p.localScale = baseScale * Mathf.Clamp01(t / half);
            yield return null;
        }
        p.localScale = baseScale;
    }

    private IEnumerator MoveTo(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            level.Player.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        level.Player.position = to;
    }

    private static Vector2Int ToDelta(Direction dir)
    {
        switch (dir)
        {
            case Direction.Up: return new Vector2Int(0, -1);    // la fila 0 es la de arriba
            case Direction.Down: return new Vector2Int(0, 1);
            case Direction.Left: return new Vector2Int(-1, 0);
            default: return new Vector2Int(1, 0);
        }
    }
}