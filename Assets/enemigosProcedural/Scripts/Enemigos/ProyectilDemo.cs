// Comportamiento de prueba del proyectil: vuela en línea recta, al rato reproduce 'impacto'
// y se destruye cuando termina. Se reemplaza después por el comportamiento real.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ProyectilDemo : MonoBehaviour
{
    public Vector2 direccion = Vector2.right;
    public float velocidad = 3f;
    public float segundosVuelo = 0.8f;

    bool volando = true;

    IEnumerator Start()
    {
        var animator = GetComponent<Animator>();
        yield return new WaitForSeconds(segundosVuelo);

        volando = false;
        animator.SetTrigger("impactar");
        yield return null;   // esperar a que entre al estado 'impacto'
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
        Destroy(gameObject);
    }

    void Update()
    {
        if (volando)
            transform.position += (Vector3)(direccion.normalized * velocidad * Time.deltaTime);
    }
}
