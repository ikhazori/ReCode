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

    private int currentIndex = 0;
    private Vector2 heartTargetPos;

    void Start()
    {
        UpdateSelection();
        if (heartIcon != null)
        {
            // Al empezar, que aparezca ya en su posicion (sin deslizarse desde 0,0)
            heartIcon.anchoredPosition = heartTargetPos;
        }
    }

    void Update()
    {
        if (options == null || options.Length == 0) return;

        // Mientras el cuadro esta animando o mostrando el mensaje, no leemos
        // el input del menu (pero el corazon sigue deslizandose abajo).
        bool inputBlocked = battleBox != null && battleBox.IsBusy;

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

        // Desliza el corazon suavemente hacia su posicion objetivo cada frame
        if (heartIcon != null)
        {
            heartIcon.anchoredPosition = Vector2.Lerp(
                heartIcon.anchoredPosition,
                heartTargetPos,
                Time.deltaTime * heartMoveSpeed
            );
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
        Bounds bounds = targetText.textBounds;
        float leftEdgeLocal = bounds.min.x * targetRect.localScale.x;
        float leftEdgeX = targetRect.anchoredPosition.x + leftEdgeLocal;
        heartTargetPos = new Vector2(leftEdgeX + heartOffsetX, targetRect.anchoredPosition.y + heartOffsetY);
    }
}