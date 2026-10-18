using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Navegacion de menu tipo Undertale: mueve la seleccion entre opciones
/// organizadas en una grilla usando las flechas del teclado.
/// Resalta la opcion seleccionada cambiando su color y/o moviendo (con
/// suavizado) un icono de corazon al lado izquierdo de ella.
/// </summary>
public class MenuNavigator : MonoBehaviour
{
    /// <summary>Evento generico: se dispara al confirmar (Z/Enter), pasando el indice elegido.
    /// Sirve para conectar este mismo menu a distintos usos (mostrar un mensaje,
    /// agregar una instruccion a una cola, etc.) sin tener que duplicar el script.</summary>
    [System.Serializable]
    public class IntUnityEvent : UnityEvent<int> { }

    [Header("Opciones del menu")]
    [Tooltip("Arrastra aqui los 4 (o mas) Text (TMP) EN ORDEN: fila por fila, de izquierda a derecha. Ej: [TopLeft, TopRight, BottomLeft, BottomRight]")]
    public TextMeshProUGUI[] options;

    [Header("Configuracion de la grilla")]
    [Tooltip("Cuantas columnas tiene la grilla. Con 4 opciones en 2x2, esto es 2. Con 4 opciones en una sola fila, esto es 4.")]
    public int columns = 2;

    [Header("Colores (opcional, puedes dejarlos iguales si solo quieres el corazon)")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.white;

    [Header("Corazon indicador")]
    [Tooltip("El RectTransform del Image del corazon. Debe ser hijo del mismo padre que las opciones (ej. DialogueBox).")]
    public RectTransform heartIcon;
    [Tooltip("Separacion entre el borde izquierdo del texto y el corazon. Numero negativo = mas lejos a la izquierda.")]
    public float heartOffsetX = -30f;
    [Tooltip("Ajuste vertical del corazon respecto al texto. Positivo = sube, negativo = baja.")]
    public float heartOffsetY = 0f;
    [Tooltip("Que tan rapido se desliza el corazon hacia la nueva posicion. Mas alto = mas rapido.")]
    public float heartMoveSpeed = 20f;

    [Header("Cuadro de dialogo (efecto cerrar/abrir al confirmar) - opcional")]
    [Tooltip("Opcional. Si lo asignas, al presionar Z/Enter se dispara la animacion de cerrar y mostrar el mensaje.")]
    public BattleBoxController battleBox;

    [Header("Evento de confirmacion (para otros usos, ej. cola de instrucciones)")]
    [Tooltip("Se dispara siempre al presionar Z/Enter, pasando el indice de la opcion elegida. Conectalo en el Inspector a lo que necesites.")]
    public IntUnityEvent OnConfirm;

    [Tooltip("Si esta en true, el menu no lee input (lo usa el PauseMenuUI mientras el juego esta en pausa).")]
    public bool externalLock = false;

    [Header("Flotar de las opciones (igual que los iconos de las flechas)")]
    public bool floatOptions = true;
    [Tooltip("Cuanto se mueve de lado a lado (unidades del Canvas).")]
    public float floatAmplitudeX = 1.5f;
    [Tooltip("Cuanto se mueve de arriba a abajo (unidades del Canvas).")]
    public float floatAmplitudeY = 2.5f;
    [Tooltip("Mas bajo = mas tranquilo.")]
    public float floatSpeed = 2f;

    private int currentIndex = 0;
    private Vector2 heartTargetPos;

    private Vector2[] basePos;
    private float[] phaseX;
    private float[] phaseY;
    private bool floatReady;

    void Start()
    {
        // El corazon se queda oculto hasta saber donde va (evita que aparezca en el lugar equivocado).
        if (heartIcon != null) heartIcon.gameObject.SetActive(false);
        StartCoroutine(InitRoutine());
    }

    // Esperamos un frame para que TextMeshPro ya haya dibujado las letras y podamos medirlas bien.
    private IEnumerator InitRoutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        foreach (TextMeshProUGUI o in options)
        {
            if (o != null) o.ForceMeshUpdate();
        }
        CaptureBasePositions();
        UpdateSelection();
        if (heartIcon != null)
        {
            heartIcon.anchoredPosition = heartTargetPos;
            heartIcon.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        if (options == null || options.Length == 0) return;

        // Mientras el cuadro esta animando o mostrando el mensaje, no leemos
        // el input del menu (pero el corazon sigue deslizandose abajo).
        bool inputBlocked = (battleBox != null && battleBox.IsBusy) || externalLock;

        if (!inputBlocked)
        {
            int rows = Mathf.CeilToInt((float)options.Length / columns);
            int currentRow = currentIndex / columns;
            int currentCol = currentIndex % columns;

            bool moved = false;

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                currentCol = (currentCol + 1) % columns;
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                currentCol = (currentCol - 1 + columns) % columns;
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                currentRow = (currentRow + 1) % rows;
                moved = true;
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                currentRow = (currentRow - 1 + rows) % rows;
                moved = true;
            }

            if (moved)
            {
                int newIndex = currentRow * columns + currentCol;
                if (newIndex < options.Length)
                {
                    currentIndex = newIndex;
                    SoundFX.Move();
                    UpdateSelection();
                }
            }

            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return))
            {
                SoundFX.Confirm();
                string selected = options[currentIndex].text;
                if (battleBox != null)
                {
                    battleBox.ShowMessage("Seleccionaste: " + selected);
                }
                OnConfirm?.Invoke(currentIndex);
            }
        }

        ApplyFloat();

        // Desliza el corazon suavemente hacia su posicion objetivo cada frame
        // (le suma el flotar de la opcion elegida para que la acompañe)
        if (heartIcon != null)
        {
            heartIcon.anchoredPosition = Vector2.Lerp(
                heartIcon.anchoredPosition,
                heartTargetPos + FloatOf(currentIndex),
                Time.deltaTime * heartMoveSpeed
            );
        }
    }

    // ---------- Flotar de las opciones ----------

    private void CaptureBasePositions()
    {
        int n = options.Length;
        basePos = new Vector2[n];
        phaseX = new float[n];
        phaseY = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (options[i] != null) basePos[i] = options[i].rectTransform.anchoredPosition;
            // Fases al azar para que cada opcion flote distinto a las demas
            phaseX[i] = Random.Range(0f, Mathf.PI * 2f);
            phaseY[i] = Random.Range(0f, Mathf.PI * 2f);
        }
        floatReady = true;
    }

    private Vector2 FloatOf(int i)
    {
        if (!floatOptions || !floatReady || i < 0 || i >= basePos.Length) return Vector2.zero;
        float t = Time.time * floatSpeed;
        return new Vector2(Mathf.Sin(t * 0.8f + phaseX[i]) * floatAmplitudeX, Mathf.Sin(t + phaseY[i]) * floatAmplitudeY);
    }

    private void ApplyFloat()
    {
        if (!floatReady) return;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null) continue;
            options[i].rectTransform.anchoredPosition = basePos[i] + FloatOf(i);
        }
    }

    void UpdateSelection()
    {
        for (int i = 0; i < options.Length; i++)
        {
            options[i].color = (i == currentIndex) ? selectedColor : normalColor;
        }

        TMP_Text targetText = options[currentIndex];
        RectTransform targetRect = targetText.rectTransform;
        // textBounds refleja el ancho REAL de las letras renderizadas
        // (no el de la caja/contenedor invisible), asi que el corazon
        // queda pegado al texto de verdad sin importar su alineacion.
        targetText.ForceMeshUpdate();
        Bounds bounds = targetText.textBounds;
        float leftEdgeLocal = bounds.min.x * targetRect.localScale.x;
        // Se usa la posicion BASE (sin el flotar), porque el flotar se le suma aparte al corazon
        Vector2 anchor = floatReady ? basePos[currentIndex] : targetRect.anchoredPosition;
        float leftEdgeX = anchor.x + leftEdgeLocal;
        Vector2 newTarget = new Vector2(leftEdgeX + heartOffsetX, anchor.y + heartOffsetY);
        if (float.IsNaN(newTarget.x) || float.IsNaN(newTarget.y)) return;
        heartTargetPos = newTarget;
    }
}