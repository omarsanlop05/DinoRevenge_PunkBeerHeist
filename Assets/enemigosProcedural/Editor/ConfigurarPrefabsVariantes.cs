// Herramientas → Configurar prefabs de enemigos
// Deja los prefabs base de la fábrica listos para el juego (cámbienles valores en el Inspector cuando quieran):
//   contactoBase        -> Rigidbody2D, CapsuleCollider2D, EnemyHealth, ProceduralEnemyInfo, ContactVariant
//   proyectilBase       -> Rigidbody2D, CapsuleCollider2D, EnemyHealth, ProceduralEnemyInfo, VariantEnemyAttack + VariantEnemyPatrol
//   voladorBase         -> Rigidbody2D (sin gravedad), CapsuleCollider2D, EnemyHealth, ProceduralEnemyInfo, FlyerVariant
//   proyectilObjetoBase -> Rigidbody2D (sin gravedad), CircleCollider2D trigger, VariantProjectile
//                          (los 6 proyectiles son Prefab Variants de este, así que lo heredan)
// Quita ComportamientoDemo / ProyectilDemo. Se puede correr varias veces: no duplica componentes.
// OJO: "Preparar demo de enemigos" vuelve a poner ComportamientoDemo; si lo usan, corran este menú otra vez.
using UnityEditor;
using UnityEngine;

public static class ConfigurarPrefabsVariantes
{
    const string CarpetaPrefabs = "Assets/enemigosProcedural/prefabsBase";

    // Los sprites miden 0.64 unidades, pero el personaje ocupa ~0.5 (la altura del collider).
    // El jugador mide 2 tiles; con 5.5 el enemigo mide ~2.75: más alto que el jugador.
    const float Escala = 5.5f;
    // Los proyectiles (7-14 px) usan la misma escala que los enemigos para que el pixel art se vea del mismo tamaño
    const float EscalaProyectil = 5.5f;
    const float VidaBase = 30f;
    // 'Default' se dibuja debajo del fondo; el jugador usa 'Personaje'
    const string CapaOrden = "Personaje";

    [MenuItem("Herramientas/Configurar prefabs de enemigos")]
    public static void Configurar()
    {
        int ok = 0;
        ok += Editar("contactoBase", raiz =>
        {
            PrepararEnemigo(raiz, gravedad: 3f);
            Asegurar<ContactVariant>(raiz);
        });
        ok += Editar("proyectilBase", raiz =>
        {
            PrepararEnemigo(raiz, gravedad: 3f);
            Asegurar<VariantEnemyAttack>(raiz);
            Asegurar<VariantEnemyPatrol>(raiz);
        });
        ok += Editar("voladorBase", raiz =>
        {
            PrepararEnemigo(raiz, gravedad: 0f);
            Asegurar<FlyerVariant>(raiz);
        });
        ok += Editar("proyectilObjetoBase", raiz =>
        {
            Quitar<ProyectilDemo>(raiz);
            var rb = Asegurar<Rigidbody2D>(raiz);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = Asegurar<CircleCollider2D>(raiz);
            col.isTrigger = true;
            col.radius = 0.05f;   // ~10 px de diámetro: el tamaño del objeto dibujado

            Asegurar<VariantProjectile>(raiz);
            Asegurar<SpriteRenderer>(raiz).sortingLayerName = CapaOrden;
            raiz.transform.localScale = new Vector3(EscalaProyectil, EscalaProyectil, 1f);
        });

        AssetDatabase.SaveAssets();
        Debug.Log($"[ConfigurarPrefabsVariantes] Listo: {ok} de 4 prefabs configurados.");
    }

    static int Editar(string nombre, System.Action<GameObject> cambios)
    {
        string ruta = $"{CarpetaPrefabs}/{nombre}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
        {
            Debug.LogWarning($"[ConfigurarPrefabsVariantes] No encontré {ruta}.");
            return 0;
        }

        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            cambios(raiz);
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
        return 1;
    }

    static void PrepararEnemigo(GameObject raiz, float gravedad)
    {
        Quitar<ComportamientoDemo>(raiz);
        raiz.tag = "Enemy";
        raiz.transform.localScale = new Vector3(Escala, Escala, 1f);

        Asegurar<SpriteRenderer>(raiz).sortingLayerName = CapaOrden;
        Asegurar<Animator>(raiz);

        var rb = Asegurar<Rigidbody2D>(raiz);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravedad;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Los frames están recortados (p. ej. 37x58 px) y el pivote está al centro del recorte, no del cuadro de 64.
        // Medido en todos los frames: los pies quedan en y = -0.28 local, así que el collider termina ahí
        // (offset -0.03 - 0.5 / 2 = -0.28). Si lo bajan más, el enemigo se ve flotando.
        var col = Asegurar<CapsuleCollider2D>(raiz);
        col.direction = CapsuleDirection2D.Vertical;
        col.size = new Vector2(0.3f, 0.5f);
        col.offset = new Vector2(0f, -0.03f);

        var vida = Asegurar<EnemyHealth>(raiz);
        vida.vida = VidaBase;

        Asegurar<ProceduralEnemyInfo>(raiz);
    }

    static T Asegurar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Quitar<T>(GameObject go) where T : Component
    {
        foreach (var c in go.GetComponents<T>())
            Object.DestroyImmediate(c, true);
    }
}
