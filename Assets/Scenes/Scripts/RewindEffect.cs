using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Desatura toda la pantalla (efecto "blanco y negro") usando el post-procesado de
/// URP. Se crea un Volume por codigo la primera vez que se usa, asi que no hay que
/// armar nada a mano en la escena.
///
/// IMPORTANTE - para que se vea el efecto:
/// 1) En la Main Camera, en el componente "Universal Additional Camera Data" (o en
///    el propio Camera si tu version lo muestra ahi), la casilla "Post Processing"
///    debe estar activada.
/// 2) El "Universal Renderer Data" que usa tu URP Asset debe tener el
///    post-procesado activo (viene activado por defecto en los proyectos con URP).
/// El Canvas principal (dialogo, panel de instrucciones) esta en blanco y negro de
/// por si, asi que aunque sea Screen Space - Overlay y no reciba el efecto, no se
/// nota. El fondo (AnimatedBackground) SI se ve afectado porque su Canvas es
/// Screen Space - Camera, dibujado por la misma camara.
/// </summary>
public static class RewindEffect
{
    private static Volume volume;
    private static ColorAdjustments colorAdjustments;

    private static void EnsureVolume()
    {
        if (volume != null) return;

        GameObject go = new GameObject("RewindVolume");
        Object.DontDestroyOnLoad(go);

        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = -100f; // gris total cuando weight llega a 1
        volume.profile = profile;
    }

    /// <summary>Sube o baja la desaturacion (0 = colores normales, 1 = blanco y negro) en 'duration' segundos.</summary>
    public static IEnumerator Fade(float targetWeight, float duration)
    {
        EnsureVolume();
        float start = volume.weight;
        float t = 0f;

        if (duration <= 0f)
        {
            volume.weight = targetWeight;
            yield break;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            volume.weight = Mathf.Lerp(start, targetWeight, Mathf.Clamp01(t / duration));
            yield return null;
        }
        volume.weight = targetWeight;
    }

    /// <summary>Corta el blanco y negro de golpe, sin fundido. Usalo al saltar de nivel desde el menu de pausa.</summary>
    public static void ForceReset()
    {
        EnsureVolume();
        volume.weight = 0f;
    }
}