using System.Collections.Generic;
using UnityEngine;

public class AttackPointBehaviour : MonoBehaviour
{
    public float dañoGolpe = 5.0f;

    // Usamos HashSet en lugar de List para evitar duplicados
    private HashSet<Collider2D> objetivosEnRango = new HashSet<Collider2D>();
    private bool attackActive = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!attackActive) return;

        if (other.CompareTag("Enemy") && objetivosEnRango.Add(other))
        {
            AplicarDaño(other);
        }

        if (other.CompareTag("Breakable") && objetivosEnRango.Add(other))
        {
            WallTrigger trigger = other.GetComponent<WallTrigger>();
            if (trigger != null)
            {
                trigger.BreakWall();
            }
            else
            {
                Debug.LogWarning("No se encontró el componente WallTrigger en " + gameObject.name);
            }

            RomperObjeto(other);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Breakable"))
        {
            objetivosEnRango.Remove(other);
            Debug.Log("Salió de rango: " + other.name);
        }
    }

    public void StartAttack()
    {
        attackActive = true;
        objetivosEnRango.Clear();
    }

    public void EndAttack()
    {
        attackActive = false;
        objetivosEnRango.Clear();
    }

    void AplicarDaño(Collider2D enemy)
    {
        EnemyHealth vida = enemy.GetComponent<EnemyHealth>();
        if (vida != null)
        {
            vida.RecibirDaño(dañoGolpe);
        }
        Debug.Log("Golpeado: " + enemy.name);
    }

    void RomperObjeto(Collider2D objeto)
    {
        Destroy(objeto.gameObject);
    }
}