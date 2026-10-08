// Herramientas → Preparar demo de enemigos:
//   1. Pone ComportamientoDemo a los prefabs base (Assets/enemigosProcedural/prefabsBase/<tipo>Base.prefab)
//   2. Los enlaza como prefabBase de su tipo en el catálogo
//   3. Crea en la escena abierta un objeto "FabricaEnemigos" con la fábrica + PruebaFabrica
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrepararDemo
{
    const string RutaCatalogo = "Assets/enemigosProcedural/Animaciones/CatalogoEnemigos.asset";
    const string CarpetaPrefabs = "Assets/enemigosProcedural/prefabsBase";

    [MenuItem("Herramientas/Preparar demo de enemigos")]
    public static void Preparar()
    {
        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoEnemigos>(RutaCatalogo);
        if (catalogo == null)
        {
            Debug.LogError("[PrepararDemo] No existe " + RutaCatalogo + ". Corre primero 'Actualizar catálogo de enemigos'.");
            return;
        }

        foreach (var tipo in catalogo.tipos)
        {
            string ruta = $"{CarpetaPrefabs}/{tipo.nombre}Base.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
            {
                Debug.LogWarning($"[PrepararDemo] No encontré {ruta}; el tipo '{tipo.nombre}' se queda sin prefab base.");
                continue;
            }

            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            if (raiz.GetComponent<ComportamientoDemo>() == null)
                raiz.AddComponent<ComportamientoDemo>();   // agrega también el Animator (RequireComponent)
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            PrefabUtility.UnloadPrefabContents(raiz);

            tipo.prefabBase = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        }
        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();

        var fabrica = Object.FindFirstObjectByType<FabricaEnemigos>();
        if (fabrica == null)
        {
            var go = new GameObject("FabricaEnemigos");
            Undo.RegisterCreatedObjectUndo(go, "Crear FabricaEnemigos");
            fabrica = go.AddComponent<FabricaEnemigos>();
        }
        fabrica.catalogo = catalogo;
        if (fabrica.GetComponent<PruebaFabrica>() == null)
            fabrica.gameObject.AddComponent<PruebaFabrica>();
        EditorUtility.SetDirty(fabrica);
        EditorSceneManager.MarkSceneDirty(fabrica.gameObject.scene);
        Selection.activeGameObject = fabrica.gameObject;

        Debug.Log("[PrepararDemo] Listo: prefabs con ComportamientoDemo, enlazados al catálogo, y 'FabricaEnemigos' en la escena. Dale Play.");
    }
}
