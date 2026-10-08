// Genera clips, Animator Controllers y Overrides a partir de los spritesheets exportados de Aseprite
// (PNG + JSON json-array con frameTags) que hay en Assets/enemigosProcedural/sprites.
//
//   Personajes: Assets/enemigosProcedural/Animaciones/<grupo>/<personaje>/clips/<variacion>_<tag>.anim
//               Assets/enemigosProcedural/Animaciones/<grupo>/<personaje>/<personaje>_base.controller
//               Assets/enemigosProcedural/Animaciones/<grupo>/<personaje>/overrides/<variacion>.overrideController
//   Proyectiles: Assets/enemigosProcedural/Animaciones/proyectiles/<nombre>/...  (controller propio, trigger 'impactar')
//
// Se puede volver a correr: actualiza lo que ya existe sin romper referencias.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class GenerarAnimaciones
{
    const string RaizSprites = "Assets/enemigosProcedural/sprites";
    const string RaizAnimaciones = "Assets/enemigosProcedural/Animaciones";
    const float FrameRate = 100f;   // las duraciones de Aseprite son múltiplos de 10 ms
    static readonly HashSet<string> TagsEnLoop = new HashSet<string> { "idle", "correr", "vuelo" };

    // --- JSON de Aseprite (json-array) ---
    [Serializable] class AseRect { public int x, y, w, h; }
    [Serializable] class AseFrame { public AseRect frame; public int duration; }
    [Serializable] class AseTag { public string name; public int from, to; }
    [Serializable] class AseMeta { public AseTag[] frameTags; }
    [Serializable] class AseData { public AseFrame[] frames; public AseMeta meta; }

    class Hoja
    {
        public string png, nombre, grupo, personaje;
        public AseData data;
    }

    [MenuItem("Herramientas/Generar animaciones")]
    public static void Generar()
    {
        var hojas = BuscarHojas();
        int clips = 0, controllers = 0, overrides = 0;
        try
        {
            // 1) importación: Point, sin compresión, cortada según el JSON
            for (int i = 0; i < hojas.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Generar animaciones", "Importando " + hojas[i].nombre, (float)i / hojas.Count * 0.3f);
                ConfigurarImportacion(hojas[i]);
            }

            // 2) clips, controllers y overrides por personaje
            var porPersonaje = hojas.GroupBy(h => h.grupo + "/" + h.personaje).ToList();
            for (int p = 0; p < porPersonaje.Count; p++)
            {
                var grupoHojas = porPersonaje[p].OrderBy(h => h.nombre, StringComparer.Ordinal).ToList();
                var primera = grupoHojas[0];
                string carpeta = $"{RaizAnimaciones}/{primera.grupo}/{primera.personaje}";
                bool esProyectil = primera.grupo == "proyectiles";
                string carpetaClips = esProyectil ? carpeta : carpeta + "/clips";
                AsegurarCarpeta(carpetaClips);

                // clips[variacion][tag]
                var clipsPorHoja = new Dictionary<string, Dictionary<string, AnimationClip>>();
                foreach (var h in grupoHojas)
                {
                    EditorUtility.DisplayProgressBar("Generar animaciones", "Clips de " + h.nombre,
                        0.3f + 0.7f * p / porPersonaje.Count);
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(h.png).OfType<Sprite>().ToDictionary(s => s.name);
                    var porTag = new Dictionary<string, AnimationClip>();
                    foreach (var tag in h.data.meta.frameTags)
                    {
                        porTag[tag.name] = CrearClip(h, tag, sprites, $"{carpetaClips}/{h.nombre}_{tag.name}.anim");
                        clips++;
                    }
                    clipsPorHoja[h.nombre] = porTag;
                }

                var baseClips = clipsPorHoja[primera.nombre];
                string nombreCtrl = esProyectil ? primera.personaje : primera.personaje + "_base";
                var ctrl = CrearController($"{carpeta}/{nombreCtrl}.controller", baseClips, esProyectil);
                controllers++;

                if (!esProyectil)
                {
                    AsegurarCarpeta(carpeta + "/overrides");
                    foreach (var h in grupoHojas)
                    {
                        CrearOverride($"{carpeta}/overrides/{h.nombre}.overrideController", ctrl, baseClips, clipsPorHoja[h.nombre]);
                        overrides++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Debug.Log($"[GenerarAnimaciones] {hojas.Count} hojas → {clips} clips, {controllers} controllers, {overrides} overrides.");
        ActualizarCatalogo();
    }

    // grupo de carpeta → nombre del tipo en el catálogo
    static readonly (string grupo, string tipo)[] GruposTipos =
    {
        ("enemigos_contacto", "contacto"),
        ("enemigos_proyectil", "proyectil"),
        ("enemigos_voladores", "volador"),
    };

    [MenuItem("Herramientas/Actualizar catálogo de enemigos")]
    public static void ActualizarCatalogo()
    {
        string ruta = RaizAnimaciones + "/CatalogoEnemigos.asset";
        var catalogo = AssetDatabase.LoadAssetAtPath<CatalogoEnemigos>(ruta);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<CatalogoEnemigos>();
            AssetDatabase.CreateAsset(catalogo, ruta);
        }

        var prefabsProyectiles = CrearPrefabsProyectiles();

        int n = CatalogoEnemigos.Niveles, total = 0;
        foreach (var (grupo, nombreTipo) in GruposTipos)
        {
            var tipo = catalogo.BuscarTipo(nombreTipo);   // si ya existe se conserva su prefabBase
            if (tipo == null)
            {
                tipo = new CatalogoEnemigos.Tipo { nombre = nombreTipo };
                catalogo.tipos.Add(tipo);
            }
            tipo.personajes.Clear();

            string carpetaGrupo = $"{RaizAnimaciones}/{grupo}";
            if (!AssetDatabase.IsValidFolder(carpetaGrupo)) continue;
            foreach (var carpeta in AssetDatabase.GetSubFolders(carpetaGrupo).OrderBy(c => c, StringComparer.Ordinal))
            {
                string nombre = Path.GetFileName(carpeta);
                var p = new CatalogoEnemigos.Personaje { nombre = nombre };
                bool completo = true;
                for (int a = 1; a <= n; a++)
                for (int z = 1; z <= n; z++)
                for (int c = 1; c <= n; c++)
                {
                    var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
                        $"{carpeta}/overrides/{nombre}_a{a}_z{z}_c{c}.overrideController");
                    if (oc == null) completo = false;
                    p.variaciones[CatalogoEnemigos.Personaje.Indice(c, z, a)] = oc;
                }
                if (!completo)
                {
                    Debug.LogWarning($"[GenerarAnimaciones] {nombre}: faltan overrides, no se agrega al catálogo.");
                    continue;
                }
                p.spriteInicial = AssetDatabase.LoadAllAssetsAtPath($"{RaizSprites}/{grupo}/{nombre}/{nombre}_a1_z1_c1.png")
                    .OfType<Sprite>().FirstOrDefault(s => s.name == $"{nombre}_a1_z1_c1_0");
                if (ProyectilDe.TryGetValue(nombre, out var nombreProyectil))
                {
                    prefabsProyectiles.TryGetValue(nombreProyectil, out p.proyectil);
                    if (p.proyectil == null)
                        Debug.LogWarning($"[GenerarAnimaciones] {nombre}: no encontré el prefab del proyectil '{nombreProyectil}'.");
                }
                tipo.personajes.Add(p);
                total++;
            }
        }

        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GenerarAnimaciones] Catálogo actualizado: {total} personajes en {ruta}.");
    }

    // enemigo de proyectil → proyectil que lanza (igual que en make_proyectiles_sueltos.py)
    static readonly Dictionary<string, string> ProyectilDe = new Dictionary<string, string>
    {
        { "rataRockera", "molotov" },
        { "bartender", "martini" },
        { "skaterSkeleton", "lataAerosol" },
        { "calacaGlam", "murcielago" },
        { "rataPiro", "cohete" },
        { "mesera", "tarroCerveza" },
    };

    const string RutaBaseProyectil = "Assets/enemigosProcedural/prefabsBase/proyectilObjetoBase.prefab";
    const string CarpetaPrefabsProyectiles = "Assets/enemigosProcedural/prefabsProyectiles";

    // Un Prefab Variant por proyectil, hijo de proyectilObjetoBase (ahí va el comportamiento,
    // y lo heredan todos). Regresa nombre → prefab.
    static Dictionary<string, GameObject> CrearPrefabsProyectiles()
    {
        var resultado = new Dictionary<string, GameObject>();
        string carpetaAnims = RaizAnimaciones + "/proyectiles";
        if (!AssetDatabase.IsValidFolder(carpetaAnims)) return resultado;

        var prefabBase = AssetDatabase.LoadAssetAtPath<GameObject>(RutaBaseProyectil);
        if (prefabBase == null)
        {
            AsegurarCarpeta(Path.GetDirectoryName(RutaBaseProyectil).Replace('\\', '/'));
            var go = new GameObject(Path.GetFileNameWithoutExtension(RutaBaseProyectil));
            go.AddComponent<SpriteRenderer>().sortingOrder = 1;   // delante de los enemigos
            go.AddComponent<ProyectilDemo>();                     // agrega también el Animator
            prefabBase = PrefabUtility.SaveAsPrefabAsset(go, RutaBaseProyectil);
            UnityEngine.Object.DestroyImmediate(go);
        }
        AsegurarCarpeta(CarpetaPrefabsProyectiles);

        foreach (var carpeta in AssetDatabase.GetSubFolders(carpetaAnims))
        {
            string nombre = Path.GetFileName(carpeta);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{carpeta}/{nombre}.controller");
            var sprite = AssetDatabase.LoadAllAssetsAtPath($"{RaizSprites}/proyectiles/{nombre}.png")
                .OfType<Sprite>().FirstOrDefault(s => s.name == nombre + "_0");
            if (ctrl == null || sprite == null)
            {
                Debug.LogWarning($"[GenerarAnimaciones] Proyectil {nombre}: falta su controller o su sprite.");
                continue;
            }

            string ruta = $"{CarpetaPrefabsProyectiles}/{nombre}.prefab";
            bool existe = AssetDatabase.LoadAssetAtPath<GameObject>(ruta) != null;
            var raiz = existe ? PrefabUtility.LoadPrefabContents(ruta)
                              : (GameObject)PrefabUtility.InstantiatePrefab(prefabBase);
            raiz.name = nombre;
            var sr = raiz.GetComponent<SpriteRenderer>();
            if (sr == null) sr = raiz.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            var animator = raiz.GetComponent<Animator>();
            if (animator == null) animator = raiz.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;

            resultado[nombre] = PrefabUtility.SaveAsPrefabAsset(raiz, ruta);   // instancia del base → Prefab Variant
            if (existe) PrefabUtility.UnloadPrefabContents(raiz);
            else UnityEngine.Object.DestroyImmediate(raiz);
        }
        return resultado;
    }

    static List<Hoja> BuscarHojas()
    {
        var hojas = new List<Hoja>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RaizSprites }))
        {
            string png = AssetDatabase.GUIDToAssetPath(guid);
            string json = Path.ChangeExtension(png, ".json");
            if (!File.Exists(json)) continue;

            // <RaizSprites>/<grupo>/<personaje>/<variacion>.png   o   <RaizSprites>/proyectiles/<nombre>.png
            var partes = png.Substring(RaizSprites.Length + 1).Split('/');
            string nombre = Path.GetFileNameWithoutExtension(png);
            hojas.Add(new Hoja
            {
                png = png,
                nombre = nombre,
                grupo = partes[0],
                personaje = partes.Length >= 3 ? partes[1] : nombre,
                data = JsonUtility.FromJson<AseData>(File.ReadAllText(json)),
            });
        }
        return hojas;
    }

    static void ConfigurarImportacion(Hoja h)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(h.png);
        bool cambio = false;
        if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; cambio = true; }
        if (imp.spriteImportMode != SpriteImportMode.Multiple) { imp.spriteImportMode = SpriteImportMode.Multiple; cambio = true; }
        if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; cambio = true; }
        if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; cambio = true; }
        if (imp.mipmapEnabled) { imp.mipmapEnabled = false; cambio = true; }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
        dp.InitSpriteEditorDataProvider();

        // si ya está cortada con los nombres esperados, se respeta el corte (y su pivote)
        var actuales = dp.GetSpriteRects();
        var esperados = Enumerable.Range(0, h.data.frames.Length).Select(i => $"{h.nombre}_{i}").ToList();
        bool yaCortada = actuales.Length == esperados.Count &&
                         new HashSet<string>(actuales.Select(r => r.name)).SetEquals(esperados);
        if (!yaCortada)
        {
            int altoTextura = h.data.frames.Max(f => f.frame.y + f.frame.h);
            var rects = h.data.frames.Select((f, i) => new SpriteRect
            {
                name = esperados[i],
                spriteID = GUID.Generate(),
                rect = new Rect(f.frame.x, altoTextura - f.frame.y - f.frame.h, f.frame.w, f.frame.h),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            }).ToArray();
            dp.SetSpriteRects(rects);
            var nombresIds = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nombresIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            cambio = true;
        }

        if (cambio) imp.SaveAndReimport();
    }

    static AnimationClip CrearClip(Hoja h, AseTag tag, Dictionary<string, Sprite> sprites, string ruta)
    {
        var keys = new List<ObjectReferenceKeyframe>();
        float t = 0f;
        Sprite ultimo = null;
        for (int i = tag.from; i <= tag.to; i++)
        {
            ultimo = sprites[$"{h.nombre}_{i}"];
            keys.Add(new ObjectReferenceKeyframe { time = t, value = ultimo });
            t += h.data.frames[i].duration / 1000f;
        }
        // key final para que el último frame dure lo que le toca
        keys.Add(new ObjectReferenceKeyframe { time = t, value = ultimo });

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        bool nuevo = clip == null;
        if (nuevo) clip = new AnimationClip();
        else clip.ClearCurves();
        clip.frameRate = FrameRate;

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = TagsEnLoop.Contains(tag.name);
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (nuevo) AssetDatabase.CreateAsset(clip, ruta);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimatorController CrearController(string ruta, Dictionary<string, AnimationClip> clips, bool esProyectil)
    {
        // si ya existe se reutiliza (los overrides apuntan a él); solo se actualizan los clips de sus estados
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
        if (ctrl != null)
        {
            foreach (var st in ctrl.layers[0].stateMachine.states)
                if (clips.TryGetValue(st.state.name, out var c)) st.state.motion = c;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        ctrl = AnimatorController.CreateAnimatorControllerAtPath(ruta);
        var sm = ctrl.layers[0].stateMachine;

        if (esProyectil)
        {
            ctrl.AddParameter("impactar", AnimatorControllerParameterType.Trigger);
            var vuelo = sm.AddState("vuelo");
            var impacto = sm.AddState("impacto");
            vuelo.motion = clips["vuelo"];
            impacto.motion = clips["impacto"];
            sm.defaultState = vuelo;
            Instantanea(vuelo.AddTransition(impacto)).AddCondition(AnimatorConditionMode.If, 0, "impactar");
            return ctrl;
        }

        ctrl.AddParameter("corriendo", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("atacar", AnimatorControllerParameterType.Trigger);
        var idle = sm.AddState("idle");
        var correr = sm.AddState("correr");
        var ataque = sm.AddState("ataque");
        idle.motion = clips["idle"];
        correr.motion = clips["correr"];
        ataque.motion = clips["ataque"];
        sm.defaultState = idle;

        Instantanea(idle.AddTransition(correr)).AddCondition(AnimatorConditionMode.If, 0, "corriendo");
        Instantanea(correr.AddTransition(idle)).AddCondition(AnimatorConditionMode.IfNot, 0, "corriendo");

        var aAtaque = Instantanea(sm.AddAnyStateTransition(ataque));
        aAtaque.canTransitionToSelf = false;
        aAtaque.AddCondition(AnimatorConditionMode.If, 0, "atacar");

        var finAtaque = ataque.AddTransition(idle);
        finAtaque.hasExitTime = true;
        finAtaque.exitTime = 1f;
        finAtaque.duration = 0f;
        return ctrl;
    }

    static T Instantanea<T>(T t) where T : AnimatorStateTransition
    {
        t.hasExitTime = false;
        t.duration = 0f;
        return t;
    }

    static void CrearOverride(string ruta, AnimatorController ctrl,
        Dictionary<string, AnimationClip> baseClips, Dictionary<string, AnimationClip> clips)
    {
        var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ruta);
        bool nuevo = oc == null;
        if (nuevo) oc = new AnimatorOverrideController(ctrl);
        else oc.runtimeAnimatorController = ctrl;

        var lista = baseClips.Where(kv => clips.ContainsKey(kv.Key))
            .Select(kv => new KeyValuePair<AnimationClip, AnimationClip>(kv.Value, clips[kv.Key]))
            .ToList();
        oc.ApplyOverrides(lista);

        if (nuevo) AssetDatabase.CreateAsset(oc, ruta);
        else EditorUtility.SetDirty(oc);
    }

    static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
}
