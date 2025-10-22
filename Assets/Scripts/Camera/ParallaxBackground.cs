using UnityEngine;
using UnityEngine.Rendering;

public class ParallaxBackground : MonoBehaviour
{

    private float StartPosition, length;
    //public GameObject cam;
    private Transform camTransform;
    public float parallaxEffect;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        camTransform = Camera.main.transform;
        StartPosition = transform.position.x;
        length = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float distance = camTransform.transform.position.x * parallaxEffect;
        float movement = camTransform.transform.position.x * (1 - parallaxEffect);

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
