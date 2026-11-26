using UnityEngine;

// Flicker for a whole neon sign (all the SpriteRenderers under it go together, so a sign made of the circle and
// its letters blinks as one). Mostly on, with a short dim now and then, like a real neon tube.
public class NeonSignFlicker : MonoBehaviour
{
    [Range(0f, 1f)] public float minAlpha = 0.55f;
    [Range(0f, 1f)] public float maxAlpha = 1f;
    [Tooltip("Chance of a dim blink each time a new brightness is picked")]
    [Range(0f, 1f)] public float dimChance = 0.15f;
    public float speed = 14f;
    [Tooltip("Seconds between brightness changes (random between x and y)")]
    public Vector2 interval = new Vector2(0.08f, 0.7f);

    private SpriteRenderer[] renderers;
    private float alpha = 1f, target = 1f, timer;

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            bool dim = Random.value < dimChance;
            target = dim ? Random.Range(minAlpha, Mathf.Lerp(minAlpha, maxAlpha, 0.5f))
                         : Random.Range(Mathf.Lerp(minAlpha, maxAlpha, 0.85f), maxAlpha);
            timer = Random.Range(interval.x, interval.y);
        }

        alpha = Mathf.Lerp(alpha, target, Time.deltaTime * speed);
        foreach (SpriteRenderer r in renderers)
        {
            if (r == null) continue;
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}
