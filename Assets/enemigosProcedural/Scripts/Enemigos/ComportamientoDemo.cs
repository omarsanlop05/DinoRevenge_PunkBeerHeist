// Comportamiento de prueba: solo recorre las animaciones (idle → correr → ataque) en bucle
// para poder verlas. Se reemplaza después por el comportamiento real.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ComportamientoDemo : MonoBehaviour
{
    public float segundosIdle = 1.5f;
    public float segundosCorrer = 1.5f;
    public float segundosAtaque = 1f;

    [Header("Solo enemigos de proyectil")]
    public GameObject proyectilPrefab;              // lo asigna quien crea al enemigo
    public float segundosHastaLanzar = 0.25f;       // momento del ataque en que sale el proyectil
    public Vector2 salidaProyectil = new Vector2(0.2f, 0.05f);
    public Vector2 direccionProyectil = Vector2.right;

    IEnumerator Start()
    {
        var animator = GetComponent<Animator>();
        // desfase al azar para que no se muevan todos al mismo tiempo
        yield return new WaitForSeconds(Random.Range(0f, segundosIdle));
        while (true)
        {
            animator.SetBool("corriendo", false);
            yield return new WaitForSeconds(segundosIdle);
            animator.SetBool("corriendo", true);
            yield return new WaitForSeconds(segundosCorrer);
            animator.SetBool("corriendo", false);
            animator.SetTrigger("atacar");
            if (proyectilPrefab != null)
            {
                yield return new WaitForSeconds(segundosHastaLanzar);
                Lanzar();
                yield return new WaitForSeconds(Mathf.Max(0f, segundosAtaque - segundosHastaLanzar));
            }
            else
                yield return new WaitForSeconds(segundosAtaque);
        }
    }

    void Lanzar()
    {
        var p = Instantiate(proyectilPrefab, transform.position + (Vector3)salidaProyectil, Quaternion.identity);
        var demo = p.GetComponent<ProyectilDemo>();
        if (demo != null) demo.direccion = direccionProyectil;
    }
}
