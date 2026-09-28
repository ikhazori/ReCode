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
    [Tooltip("Opcional. Si existe en la escena, se reproduce su dialogo al ganar.")]
    public DialogueController dialogue;

    [Header("Al ganar")]
    [Tooltip("Oculta la cuadricula mientras habla el personaje, para que la escena quede limpia.")]
    public bool hideGridDuringDialogue = true;

    [Header("Tiempos")]
    [Tooltip("Cuanto tarda el personaje en avanzar una casilla (segundos).")]
    public float stepDuration = 0.3f;
    [Tooltip("Pausa antes de reiniciar cuando termina la secuencia (sin chocar).")]
    public float pauseBeforeReset = 0.8f;

    [Header("Eventos")]
    public UnityEvent OnLevelComplete;

    private bool running;
    private Vector2Int currentCell;

    void Start()
    {
        if (level == null) level = FindFirstObjectByType<GridLevel>();
        if (queue == null) queue = FindFirstObjectByType<CommandQueueUI>();
        if (effects == null) effects = FindFirstObjectByType<ScreenEffects>();
        if (dialogue == null) dialogue = FindFirstObjectByType<DialogueController>();

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

        bool crashed = false;

        foreach (Direction dir in instructions)
        {
            Vector2Int next = currentCell + ToDelta(dir);
            Vector3 from = level.CellToWorld(currentCell);

            if (!level.IsWalkable(next))
            {
                // Avanza un poquito hacia el muro/borde y choca
                Vector3 bump = Vector3.Lerp(from, level.CellToWorld(next), 0.4f);
                yield return MoveTo(from, bump, stepDuration * 0.5f);
                crashed = true;
                break;
            }

            SoundFX.Step();
            yield return MoveTo(from, level.CellToWorld(next), stepDuration);
            currentCell = next;
        }

        if (crashed)
        {
            SoundFX.Crash();
            if (effects != null) yield return effects.PlayCrashEffect();
        }
        else if (currentCell == level.GoalCell)
        {
            Debug.Log("¡Nivel completado!");
            OnLevelComplete?.Invoke();

            // Destello verde suave de victoria
            SoundFX.Win();
            if (effects != null) yield return effects.PlayWinEffect();

            if (dialogue != null)
            {
                queue.ClearInstructions(); // quita los iconos para que no tapen el texto
                if (hideGridDuringDialogue) level.SetVisible(false);
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
            yield return new WaitForSeconds(pauseBeforeReset);
        }

        ResetLevel();
        queue.Locked = false;
        running = false;
    }

    private void ResetLevel()
    {
        currentCell = level.StartCell;
        level.ResetPlayer();
        queue.ClearInstructions();
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