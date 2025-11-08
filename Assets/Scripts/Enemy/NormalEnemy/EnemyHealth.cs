using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public float vida = 100f;

    public void RecibirDaño(float daño)
    {
        vida -= daño;
        Debug.Log(name + " recibió " + daño + " de daño. Vida restante: " + vida);
        if (vida <= 0)
            Morir();
    }

    void Morir()
    {
        Debug.Log(name + " ha muerto");
        if (gameObject.CompareTag("Boss"))
        {
            StartCoroutine(MostrarPantallaDeVictoria(2f));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator MostrarPantallaDeVictoria(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (GameManager.instance != null)
        {
            GameManager.instance.ShowVictoryScreen();
        }
        else
        {
            Debug.LogWarning("No se encontro el GameManager.");
        }

        Destroy(gameObject);
    }
}
