// Crea enemigos: recibe los tipos permitidos y el nivel de casco, zapatos y aura (1-3),
// elige un personaje al azar entre esos tipos y regresa el GameObject con su animación.
//
//   GameObject e = fabrica.Crear(new[] { "volador", "contacto" }, casco: 1, zapatos: 2, aura: 3);
//   GameObject e = fabrica.Crear(new[] { "proyectil" }, 1, 2, 3, out GameObject prefabProyectil);
using System.Collections.Generic;
using UnityEngine;

public class FabricaEnemigos : MonoBehaviour
{
    public CatalogoEnemigos catalogo;

    public GameObject Crear(string[] tipos, int casco, int zapatos, int aura,
        Vector3 posicion = default, Transform padre = null)
    {
        return Crear(catalogo, tipos, casco, zapatos, aura, out _, posicion, padre);
    }

    // Igual, pero además regresa el PREFAB del proyectil del enemigo (null si no es de proyectil).
    // Se instancia cuando el enemigo dispare:  Instantiate(proyectil, posicion, Quaternion.identity);
    public GameObject Crear(string[] tipos, int casco, int zapatos, int aura, out GameObject proyectil,
        Vector3 posicion = default, Transform padre = null)
    {
        return Crear(catalogo, tipos, casco, zapatos, aura, out proyectil, posicion, padre);
    }

    public static GameObject Crear(CatalogoEnemigos catalogo, string[] tipos, int casco, int zapatos, int aura,
        Vector3 posicion = default, Transform padre = null)
    {
        return Crear(catalogo, tipos, casco, zapatos, aura, out _, posicion, padre);
    }

    public static GameObject Crear(CatalogoEnemigos catalogo, string[] tipos, int casco, int zapatos, int aura,
        out GameObject proyectil, Vector3 posicion = default, Transform padre = null)
    {
        proyectil = null;
        if (catalogo == null)
        {
            Debug.LogError("[FabricaEnemigos] No hay catálogo asignado.");
            return null;
        }
        if (!EnRango(casco) || !EnRango(zapatos) || !EnRango(aura))
        {
            Debug.LogError($"[FabricaEnemigos] casco, zapatos y aura deben ir de 1 a {CatalogoEnemigos.Niveles} " +
                           $"(llegó casco={casco}, zapatos={zapatos}, aura={aura}).");
            return null;
        }

        // todos los personajes de los tipos pedidos, cada uno con la misma probabilidad
        var candidatos = new List<(CatalogoEnemigos.Tipo tipo, CatalogoEnemigos.Personaje personaje)>();
        foreach (var nombreTipo in tipos ?? new string[0])
        {
            var tipo = catalogo.BuscarTipo(nombreTipo);
            if (tipo == null)
            {
                Debug.LogWarning($"[FabricaEnemigos] No existe el tipo '{nombreTipo}'.");
                continue;
            }
            foreach (var p in tipo.personajes)
                candidatos.Add((tipo, p));
        }
        if (candidatos.Count == 0)
        {
            Debug.LogError("[FabricaEnemigos] No hay personajes para los tipos: " + string.Join(", ", tipos ?? new string[0]));
            return null;
        }

        var (tipoElegido, elegido) = candidatos[Random.Range(0, candidatos.Count)];
        var controller = elegido.Variacion(casco, zapatos, aura);

        var go = tipoElegido.prefabBase != null
            ? Instantiate(tipoElegido.prefabBase, posicion, Quaternion.identity, padre)
            : new GameObject();
        if (tipoElegido.prefabBase == null)
        {
            go.transform.SetParent(padre, false);
            go.transform.position = posicion;
        }
        go.name = $"{elegido.nombre}_a{aura}_z{zapatos}_c{casco}";

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = elegido.spriteInicial;

        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        proyectil = elegido.proyectil;
        return go;
    }

    static bool EnRango(int v) => v >= 1 && v <= CatalogoEnemigos.Niveles;
}
