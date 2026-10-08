// Catálogo con las referencias a los Animator Override Controllers de cada enemigo,
// agrupados por tipo. Lo llena el menú Herramientas → Actualizar catálogo de enemigos.
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Enemigos/Catálogo de enemigos", fileName = "CatalogoEnemigos")]
public class CatalogoEnemigos : ScriptableObject
{
    public const int Niveles = 3;   // casco, zapatos y aura van de 1 a 3

    [Serializable]
    public class Personaje
    {
        public string nombre;
        // 27 variaciones, índice = (aura-1)*9 + (zapatos-1)*3 + (casco-1)
        public AnimatorOverrideController[] variaciones = new AnimatorOverrideController[Niveles * Niveles * Niveles];
        public Sprite spriteInicial;
        [Tooltip("Prefab del proyectil que lanza (solo enemigos de proyectil).")]
        public GameObject proyectil;

        public static int Indice(int casco, int zapatos, int aura) =>
            (aura - 1) * Niveles * Niveles + (zapatos - 1) * Niveles + (casco - 1);

        public AnimatorOverrideController Variacion(int casco, int zapatos, int aura) =>
            variaciones[Indice(casco, zapatos, aura)];
    }

    [Serializable]
    public class Tipo
    {
        public string nombre;                 // "contacto", "proyectil" o "volador"
        [Tooltip("Opcional: prefab con el script de comportamiento. Si está vacío se crea un GameObject nuevo.")]
        public GameObject prefabBase;
        public List<Personaje> personajes = new List<Personaje>();
    }

    public List<Tipo> tipos = new List<Tipo>();

    public Tipo BuscarTipo(string nombre) =>
        tipos.Find(t => string.Equals(t.nombre, nombre, StringComparison.OrdinalIgnoreCase));
}
