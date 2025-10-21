using UnityEngine;

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
        Destroy(gameObject);
    }
}
