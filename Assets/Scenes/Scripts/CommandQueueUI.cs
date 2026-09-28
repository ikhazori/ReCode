using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Direcciones posibles de una instruccion de movimiento.
/// </summary>
public enum Direction { Up, Down, Left, Right }

/// <summary>
/// Escucha el evento OnConfirm de un MenuNavigator (el menu de flechas) y va
/// agregando un icono por cada direccion elegida a una fila en pantalla,
/// guardando ademas la secuencia en una lista para poder ejecutarla despues.
/// Cada icono se crea por codigo (un "slot" que acomoda el Layout Group y, dentro,
/// el icono que flota), asi que ya no hace falta ningun prefab.
/// </summary>
public class CommandQueueUI : MonoBehaviour
{
    [Header("Mapeo: el indice de la opcion en el menu -> que direccion representa")]
    [Tooltip("Debe tener el mismo largo y orden que las 'options' del MenuNavigator. Ej: si tus opciones son [Arriba, Abajo, Izquierda, Derecha], este array debe ser [Up, Down, Left, Right].")]
    public Direction[] directionMap;

    [Header("Iconos (mismo orden que directionMap)")]
    public Sprite[] directionIcons;

    [Header("Donde van apareciendo los iconos")]
    [Tooltip("El objeto 'InstructionRow'. Cada icono nuevo se agrega aqui como hijo.")]
    public RectTransform iconContainer;
    [Tooltip("Tamaño de cada icono (unidades del Canvas).")]
    public Vector2 iconSize = new Vector2(28f, 28f);

    [Header("Limite (0 = sin limite)")]
    public int maxInstructions = 0;

    /// <summary>La secuencia de direcciones armada hasta ahora, en orden.</summary>
    public List<Direction> Instructions { get; private set; } = new List<Direction>();

    [Header("Se dispara cuando el jugador presiona la tecla de correr (R)")]
    public UnityEvent OnRunRequested;

    /// <summary>Mientras es true (el personaje se esta moviendo) no se pueden agregar instrucciones ni volver a correr.</summary>
    public bool Locked { get; set; }

    void Update()
    {
        if (Locked) return;

        if (Input.GetKeyDown(KeyCode.R) && Instructions.Count > 0)
        {
            OnRunRequested?.Invoke();
        }
    }

    /// <summary>Conecta este metodo al evento OnConfirm del MenuNavigator de direcciones.</summary>
    public void AddDirection(int optionIndex)
    {
        if (Locked) return;
        if (directionMap == null || optionIndex < 0 || optionIndex >= directionMap.Length) return;
        if (maxInstructions > 0 && Instructions.Count >= maxInstructions) return;

        Instructions.Add(directionMap[optionIndex]);

        if (iconContainer != null) CreateIcon(optionIndex);
    }

    /// <summary>Borra toda la secuencia armada y los iconos (usalo al reiniciar el nivel).</summary>
    public void ClearInstructions()
    {
        Instructions.Clear();
        if (iconContainer != null)
        {
            for (int i = iconContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(iconContainer.GetChild(i).gameObject);
            }
        }
    }

    private void CreateIcon(int optionIndex)
    {
        // Slot: este SI lo posiciona el Layout Group
        GameObject slot = new GameObject("Slot", typeof(RectTransform), typeof(LayoutElement));
        slot.transform.SetParent(iconContainer, false);
        ((RectTransform)slot.transform).sizeDelta = iconSize;
        LayoutElement element = slot.GetComponent<LayoutElement>();
        element.preferredWidth = iconSize.x;
        element.preferredHeight = iconSize.y;

        // Icono: vive dentro del slot y es el que flota libremente
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(slot.transform, false);
        RectTransform rt = (RectTransform)icon.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = icon.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        if (directionIcons != null && optionIndex < directionIcons.Length)
        {
            img.sprite = directionIcons[optionIndex];
        }

        icon.AddComponent<FloatingIcon>();
    }
}