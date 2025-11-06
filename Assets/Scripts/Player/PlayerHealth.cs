using System;
using System.Collections;
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

    public void RecibirDaño(float daño, float atacanteX)
    {
        if (controller.isAttacking || controller.isInvulnerable)
            return;

        if (controller.isAttacking != true)
        {
            vidaActual -= daño;
            Debug.Log(name + " recibió " + daño + " de daño. Vida restante: " + vidaActual);
            controller.animator.SetTrigger("Hurt");
            controller.SetHurtState(0.25f, atacanteX);
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

        if (vidaActual >= vidaMaxima)
        {
            Debug.Log("🚫 Ya tienes toda la vida, no te puedes curar más");
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

        controller.drinKingState(1.0f);

        if (vidaActual <= 0)
        {
            
            Morir();
        }
            
    }

    void Morir()
    {
        controller.animator.SetTrigger("Dead");
        controller.isDead = true;
        controller.jumpQueued = false;
        controller.isJumping = false;
        controller.rb.linearVelocity = Vector2.zero;
        controller.rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        Debug.Log(name + " ha muerto");
        StartCoroutine(MostrarPantallaDeMuerte(3f));
    }
    public void MorirInst()
    {
        vidaActual = 0;
        Morir();
        StartCoroutine(MostrarPantallaDeMuerte(0.1f));
    }

    IEnumerator MostrarPantallaDeMuerte(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (GameManager.instance != null)
        {
            GameManager.instance.ShowDeathScreen();
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontró el GameManager.");
        }

        // Destruir el jugador después de mostrar la pantalla
        Destroy(gameObject);
    }

}
