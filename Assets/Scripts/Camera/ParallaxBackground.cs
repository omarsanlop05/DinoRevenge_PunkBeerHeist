using UnityEngine;
using UnityEngine.Rendering;

public class ParallaxBackground : MonoBehaviour
{

    private float StartPosition, length;
    public GameObject cam;
    public float parallaxEffect;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartPosition = transform.position.x;
        length = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float distance = cam.transform.position.x * parallaxEffect;
        float movement = cam.transform.position.x * (1 - parallaxEffect);

        transform.position = new Vector3(StartPosition + distance, transform.position.y, transform.position.z);

        if (movement > StartPosition + length)
        {
            StartPosition += length;
        }
        else if (movement < StartPosition - length)
        {
            StartPosition -= length;
        }
    }
}
