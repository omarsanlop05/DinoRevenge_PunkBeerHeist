using UnityEngine;

public class PlayerHealth : MonoBehaviour
{

    [Header("Vida")]
    public float vidaMaxima = 100f;
    public float vidaActual = 100f;
    
    [Header("Cervezas")]
    public int cervezas = 0;
    public int maxCervezas = 2;

    public PlayerController controller; 

    public void RecibirDaño(float daño)
    {
        if (controller.isAttacking != true)
        {
            vidaActual -= daño;
            Debug.Log(name + " recibió " + daño + " de daño. Vida restante: " + vidaActual);
            controller.animator.SetTrigger("Hurt");
            controller.isHurt = true;
            controller.Invoke(nameof(controller.FinHerido), 0.25f);
            if (vidaActual <= 0)
                Morir();
        }
       
    }
    public void TomarCerveza()
    {
        if (cervezas <= 0)
        {
            Debug.Log("🚫 No te quedan cervezas.");
            return;
        }

        if (controller == null)
        {
            Debug.Log("🚫 No se puede acceder al controlador.");
            return;
        }

        if (!controller.IsGrounded())
        {
            Debug.Log("🚫 Solo puedes tomar cerveza estando en el suelo.");
            return;
        }

        if (controller.isAttacking || controller.isDrinking)
        {
            Debug.Log("🚫 No puedes tomar cerveza mientras estás atacando o ya estás tomando.");
            return;
        }

        controller.isDrinking = true;
        vidaActual = Mathf.Min(vidaActual + 25f, vidaMaxima);
        cervezas--;
        controller.animator.SetTrigger("Beer");

        Debug.Log(name + " tomó una cerveza y recuperó 25 puntos de vida. Vida restante: " + vidaActual + ". Cervezas restantes: " + cervezas);

        controller.Invoke(nameof(controller.FinDeTomarCerveza), 0.5f); 

        if (vidaActual <= 0)
            Morir();
    }

    void Morir()
    {
        Debug.Log(name + " ha muerto");
        Destroy(gameObject);
    }
}
