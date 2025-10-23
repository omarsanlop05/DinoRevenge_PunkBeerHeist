using System.Collections.Generic;
using UnityEngine;

public class AttackPointBehaviour : MonoBehaviour
{
    public float da�oGolpe = 5f;
    private List<Collider2D> objetivosEnRango = new List<Collider2D>();
    private bool attackActive = false;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (attackActive && other.CompareTag("Enemy"))
        {
            if (!objetivosEnRango.Contains(other))
            {
                objetivosEnRango.Add(other);
                AplicarDa�o(other);
            }
        }

        if (attackActive && other.CompareTag("Breakable"))
        {
            if (!objetivosEnRango.Contains(other))
            {
                objetivosEnRango.Add(other);
                RomperObjeto(other);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            objetivosEnRango.Remove(other);
            Debug.Log("Enemigo sali� de rango: " + other.name);
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

    void AplicarDa�o(Collider2D enemy)
    {
        // Si tus enemigos tienen un script tipo "EnemyHealth" o similar:
        EnemyHealth vida = enemy.GetComponent<EnemyHealth>();
        if (vida != null)
        {
            vida.RecibirDa�o(da�oGolpe);
        }
        Debug.Log("Golpeado: " + enemy.name);
    }

    void RomperObjeto(Collider2D objeto)
    {
        Destroy(objeto.gameObject);
    }
}
