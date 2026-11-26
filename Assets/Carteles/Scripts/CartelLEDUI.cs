using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cartel LED para UI (Canvas): la misma animación que CartelLED, pero cambiando el sprite de un Image,
/// porque el Animator de los prefabs anima un SpriteRenderer y eso no se ve en un Canvas Overlay.
/// Los datos salen de Frames/&lt;nombre&gt;/secuencia.json (frames + duracionesMs).
///
/// Entrada como en la pantalla de título del trailer (E08):
///   - Parpadeo: se enciende parpadeando como un letrero (carteles de los lados).
///   - Golpe: aparece grande y transparente y cae a su tamaño, con un flash blanco (título).
/// Usa tiempo sin escala, así funciona aunque el juego esté en pausa (Time.timeScale = 0).
/// </summary>
[RequireComponent(typeof(Image))]
public class CartelLEDUI : MonoBehaviour
{
    public enum Entrada { Ninguna, Parpadeo, Golpe }

    [Header("Animación (de secuencia.json)")]
    public Sprite[] frames;
    [Tooltip("Índice del frame que se muestra en cada paso")]
    public int[] secuencia;
    [Tooltip("Duración de cada paso en milisegundos")]
    public int[] duracionesMs;
    [Min(0f)] public float velocidad = 1f;
    [Tooltip("Empieza en un punto aleatorio del loop")]
    public bool desfaseAleatorio = true;

    [Header("Entrada")]
    public Entrada entrada = Entrada.Parpadeo;
    [Tooltip("Segundos antes de aparecer")]
    public float retraso = 0f;
    public float duracionEntrada = 0.35f;
    [Tooltip("Solo Golpe: tamaño con el que empieza")]
    public float escalaInicial = 1.35f;
    [Tooltip("Solo Golpe: Image a pantalla completa que hace el flash (opcional)")]
    public Image flash;

    [Range(0f, 1f), Tooltip("1 = normal, menos = más tenue")]
    public float brillo = 1f;

    private Image img;
    private int paso;
    private float tiempoEnPaso;
    private float alfa = 1f;

    private void Awake()
    {
        img = GetComponent<Image>();
        img.preserveAspect = true;
    }

    private void OnEnable()
    {
        paso = 0;
        tiempoEnPaso = 0f;
        if (Pasos > 0 && desfaseAleatorio)
        {
            paso = Random.Range(0, Pasos);
            tiempoEnPaso = Random.value * Duracion(paso);
        }
        MostrarPaso();
        StartCoroutine(Entrar());
    }

    private int Pasos => secuencia != null && duracionesMs != null ? Mathf.Min(secuencia.Length, duracionesMs.Length) : 0;

    private float Duracion(int i) => Mathf.Max(0.01f, duracionesMs[i] / 1000f);

    private void Update()
    {
        if (Pasos == 0 || velocidad <= 0f) return;

        tiempoEnPaso += Time.unscaledDeltaTime * velocidad;
        bool cambio = false;
        while (tiempoEnPaso >= Duracion(paso))
        {
            tiempoEnPaso -= Duracion(paso);
            paso = (paso + 1) % Pasos;
            cambio = true;
        }
        if (cambio) MostrarPaso();
    }

    private void MostrarPaso()
    {
        if (Pasos == 0 || frames == null || frames.Length == 0) return;
        int f = Mathf.Clamp(secuencia[paso], 0, frames.Length - 1);
        if (frames[f] != null) img.sprite = frames[f];
    }

    private void AplicarColor() => img.color = new Color(brillo, brillo, brillo, alfa);

    private IEnumerator Entrar()
    {
        var escalaFinal = transform.localScale;
        if (entrada == Entrada.Ninguna) { alfa = 1f; AplicarColor(); yield break; }

        alfa = 0f;
        AplicarColor();
        if (retraso > 0f) yield return new WaitForSecondsRealtime(retraso);

        if (entrada == Entrada.Parpadeo)
        {
            float[] patron = { 1, 0, 0.6f, 0, 1, 0.2f, 1 };
            float dt = duracionEntrada / patron.Length;
            foreach (float a in patron)
            {
                alfa = a;
                AplicarColor();
                yield return new WaitForSecondsRealtime(dt);
            }
        }
        else // Golpe
        {
            float t = 0f;
            while (t < duracionEntrada)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duracionEntrada);
                float salida = 1f - (1f - k) * (1f - k) * (1f - k);   // sale rápido y frena
                transform.localScale = escalaFinal * Mathf.Lerp(escalaInicial, 1f, salida);
                alfa = Mathf.Clamp01(t / (duracionEntrada * 0.8f));
                AplicarColor();
                yield return null;
            }
            transform.localScale = escalaFinal;
            if (flash != null) StartCoroutine(Flash());
        }

        alfa = 1f;
        AplicarColor();
    }

    private IEnumerator Flash()
    {
        const float dur = 0.5f;
        flash.gameObject.SetActive(true);
        flash.raycastTarget = false;
        for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
        {
            var c = flash.color; c.a = 0.85f * (1f - t / dur); flash.color = c;
            yield return null;
        }
        flash.gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        if (img == null) img = GetComponent<Image>();
        if (img != null && !Application.isPlaying && frames != null && frames.Length > 0 && secuencia != null && secuencia.Length > 0)
        {
            int f = Mathf.Clamp(secuencia[secuencia.Length - 1], 0, frames.Length - 1);
            if (frames[f] != null) img.sprite = frames[f];
        }
    }
}
