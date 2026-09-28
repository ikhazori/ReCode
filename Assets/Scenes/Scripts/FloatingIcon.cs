using UnityEngine;

/// <summary>
/// Le da vida a un icono: flota suavemente arriba/abajo y a los lados dentro de su lugar
/// (cada icono con su propio ritmo) y aparece con un pequeño "pop".
/// Se agrega solo desde CommandQueueUI, no hay que ponerlo a mano.
/// </summary>
public class FloatingIcon : MonoBehaviour
{
    [Tooltip("Cuanto se mueve de lado a lado (unidades del Canvas).")]
    public float amplitudeX = 1.5f;
    [Tooltip("Cuanto se mueve de arriba a abajo (unidades del Canvas).")]
    public float amplitudeY = 2.5f;
    [Tooltip("Velocidad del movimiento. Mas bajo = mas tranquilo.")]
    public float speed = 2f;
    [Tooltip("Duracion del 'pop' de aparicion (segundos). 0 = sin pop.")]
    public float popDuration = 0.15f;

    private RectTransform rt;
    private float phaseX;
    private float phaseY;
    private float age;

    void Awake()
    {
        rt = (RectTransform)transform;
        // Fases al azar para que cada icono flote distinto a los demas
        phaseX = Random.Range(0f, Mathf.PI * 2f);
        phaseY = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Time.time * speed;

        rt.anchoredPosition = new Vector2(
            Mathf.Sin(t * 0.8f + phaseX) * amplitudeX,
            Mathf.Sin(t + phaseY) * amplitudeY
        );

        float pop = popDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / popDuration));
        rt.localScale = new Vector3(pop, pop, 1f);
    }
}
