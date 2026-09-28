using UnityEngine;

/// <summary>
/// Hace que el personaje "respire" (se estira y encoge un poquito) y se balancee apenas.
/// Lo agrega solo el DialogueController al Guide, no hay que ponerlo a mano.
/// Usa escala y rotacion (no la posicion) para no chocar con el deslizamiento de entrada/salida.
/// </summary>
public class GuideIdle : MonoBehaviour
{
    [Tooltip("Cuanto se estira al respirar (0.03 = 3%).")]
    public float breathe = 0.03f;
    public float breatheSpeed = 2f;
    [Tooltip("Grados que se balancea de lado a lado.")]
    public float sway = 2f;
    public float swaySpeed = 1.3f;

    private RectTransform rt;
    private Vector3 baseScale;

    void Awake()
    {
        rt = (RectTransform)transform;
        baseScale = rt.localScale;
    }

    void Update()
    {
        float b = Mathf.Sin(Time.time * breatheSpeed) * breathe;
        rt.localScale = new Vector3(baseScale.x * (1f - b * 0.5f), baseScale.y * (1f + b), baseScale.z);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * swaySpeed) * sway);
    }
}
