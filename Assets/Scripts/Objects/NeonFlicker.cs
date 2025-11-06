using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class NeonFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    public float minAlpha = 0.5f;
    public float maxAlpha = 2f;
    public float flickerSpeed = 15f;
    public float flickerInterval = 0.3f;

    private SpriteRenderer spriteRenderer;
    private float targetAlpha;
    private float timer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("NeonFlicker: No SpriteRenderer found!");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        targetAlpha = maxAlpha;
        timer = flickerInterval;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            targetAlpha = Random.Range(minAlpha, maxAlpha);
            timer = flickerInterval;
        }

        Color currentColor = spriteRenderer.color;
        float newAlpha = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * flickerSpeed);
        spriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);
    }
}