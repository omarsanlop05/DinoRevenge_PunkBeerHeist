using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public float vida = 100f;

    public void RecibirDa�o(float da�o)
    {
        vida -= da�o;
        Debug.Log(name + " recibi� " + da�o + " de da�o. Vida restante: " + vida);
        if (vida <= 0)
            Morir();
    }

    void Morir()
    {
        Debug.Log(name + " ha muerto");
        Destroy(gameObject);
    }
}
