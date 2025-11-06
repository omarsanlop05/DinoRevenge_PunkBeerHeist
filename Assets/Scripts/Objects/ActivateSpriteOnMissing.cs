using UnityEngine;

public class ActivateSpriteOnMissing : MonoBehaviour
{
    [Header("Target to Monitor")]
    public GameObject targetObject; // El GameObject que estamos vigilando

    [Header("Sprite to Activate")]
    public SpriteRenderer spriteToActivate; // El SpriteRenderer que se activará

    private bool hasActivated = false;

    void Update()
    {
        // Si el objeto objetivo ya no existe y aún no hemos activado el sprite
        if (!hasActivated && (targetObject == null || !targetObject.activeInHierarchy))
        {
            spriteToActivate.enabled = true;
            hasActivated = true;
        }
    }
}