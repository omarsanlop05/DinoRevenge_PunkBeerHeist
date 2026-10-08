// Prueba de la fábrica: crea una fila de enemigos por tipo, cada uno con una variación al azar.
using UnityEngine;

[RequireComponent(typeof(FabricaEnemigos))]
public class PruebaFabrica : MonoBehaviour
{
    public string[] tipos = { "contacto", "proyectil", "volador" };
    public int porFila = 6;
    public float separacion = 1f;

    void Start()
    {
        var f = GetComponent<FabricaEnemigos>();
        for (int fila = 0; fila < tipos.Length; fila++)
        {
            float y = ((tipos.Length - 1) / 2f - fila) * separacion * 1.5f;
            for (int i = 0; i < porFila; i++)
            {
                float x = (i - (porFila - 1) / 2f) * separacion;
                var enemigo = f.Crear(new[] { tipos[fila] },
                    Random.Range(1, 4), Random.Range(1, 4), Random.Range(1, 4),
                    out GameObject prefabProyectil, new Vector3(x, y, 0), transform);
                var demo = enemigo != null ? enemigo.GetComponent<ComportamientoDemo>() : null;
                if (demo != null) demo.proyectilPrefab = prefabProyectil;
            }
        }
    }
}
