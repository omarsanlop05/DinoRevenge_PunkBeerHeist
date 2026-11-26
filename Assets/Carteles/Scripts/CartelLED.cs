using UnityEngine;

/// <summary>
/// Cartel LED animado (siempre en loop).
/// - Desfase: para que varios carteles en la escena no parpadeen sincronizados.
/// - Velocidad: multiplica la velocidad de la animación.
/// - Brillo: 1 = normal, menos = más tenue (el resplandor extra lo da el Bloom).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public class CartelLED : MonoBehaviour
{
    [Tooltip("Empieza en un punto aleatorio del loop")]
    public bool desfaseAleatorio = true;

    [Range(0f, 1f), Tooltip("Punto del loop donde empieza (si no es aleatorio)")]
    public float desfase = 0f;

    [Min(0f)] public float velocidad = 1f;

    [Range(0f, 1f), Tooltip("1 = normal, menos = más tenue")]
    public float brillo = 1f;

    void OnEnable()
    {
        var anim = GetComponent<Animator>();
        anim.speed = velocidad;
        if (Application.isPlaying)
        {
            float t = desfaseAleatorio ? Random.value : desfase;
            anim.Play("Loop", 0, t);
        }
        AplicarBrillo();
    }

    void OnValidate()
    {
        var anim = GetComponent<Animator>();
        if (anim) anim.speed = velocidad;
        AplicarBrillo();
    }

    void AplicarBrillo()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (!sr) return;
        sr.color = new Color(brillo, brillo, brillo, 1f);
    }
}
