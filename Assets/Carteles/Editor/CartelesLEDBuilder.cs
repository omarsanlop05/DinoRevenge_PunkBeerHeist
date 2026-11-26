using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Convierte Assets/Carteles/Frames/&lt;nombre&gt;/ (PNGs + secuencia.json,
/// generados por herramientas/exportar_unity.py) en:
///   Animaciones/&lt;nombre&gt;.anim + .controller   (loop)
///   Prefabs/&lt;nombre&gt;.prefab                   (SpriteRenderer + Animator + CartelLED)
///
/// Corre solo al abrir el proyecto si falta algún prefab, o a mano en
/// el menú  Herramientas > Reconstruir carteles LED.
/// </summary>
[InitializeOnLoad]
public static class CartelesLEDBuilder
{
    const string Raiz = "Assets/Carteles";
    const string DirFrames = Raiz + "/Frames";
    const string DirAnim = Raiz + "/Animaciones";
    const string DirPrefabs = Raiz + "/Prefabs";

    // 768 px de alto / 96 = 8 unidades de Unity (el dino mide ~2)
    const float PixelesPorUnidad = 96f;
    const string CapaOrden = "Decoraciones";   // sorting layer del juego (si existe)

    [System.Serializable]
    class Secuencia
    {
        public string nombre;
        public int[] frames;
        public int[] duracionesMs;
    }

    static CartelesLEDBuilder()
    {
        EditorApplication.delayCall += ConstruirSiFalta;
    }

    public static void ConstruirSiFalta()
    {
        if (!AssetDatabase.IsValidFolder(DirFrames)) return;
        bool falta = Directory.GetDirectories(DirFrames)
            .Any(d => !File.Exists($"{DirPrefabs}/{Path.GetFileName(d)}.prefab"));
        if (falta) Construir();
    }

    [MenuItem("Herramientas/Reconstruir carteles LED")]
    public static void Construir()
    {
        Carpeta(DirAnim);
        Carpeta(DirPrefabs);

        var material = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

        int hechos = 0;
        foreach (var dir in Directory.GetDirectories(DirFrames).OrderBy(d => d))
        {
            string carpeta = dir.Replace('\\', '/');
            string json = carpeta + "/secuencia.json";
            if (!File.Exists(json)) continue;
            var seq = JsonUtility.FromJson<Secuencia>(File.ReadAllText(json));
            string nombre = Path.GetFileName(carpeta);

            // 1) importar PNGs como sprites
            var pngs = Directory.GetFiles(carpeta, "f*.png").OrderBy(p => p).ToArray();
            var sprites = new Sprite[pngs.Length];
            for (int i = 0; i < pngs.Length; i++)
            {
                string ruta = pngs[i].Replace('\\', '/');
                var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
                if (imp == null) { AssetDatabase.ImportAsset(ruta); imp = (TextureImporter)AssetImporter.GetAtPath(ruta); }
                bool cambiar = imp.textureType != TextureImporterType.Sprite
                               || imp.spritePixelsPerUnit != PixelesPorUnidad
                               || imp.textureCompression != TextureImporterCompression.Uncompressed;
                if (cambiar)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spritePixelsPerUnit = PixelesPorUnidad;
                    imp.mipmapEnabled = false;
                    imp.alphaIsTransparency = true;
                    imp.filterMode = FilterMode.Bilinear;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.maxTextureSize = 2048;
                    var s = new TextureImporterSettings();
                    imp.ReadTextureSettings(s);
                    s.spriteAlignment = (int)SpriteAlignment.Center;
                    imp.SetTextureSettings(s);
                    imp.SaveAndReimport();
                }
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            }

            // 2) clip en loop con las duraciones de Aseprite
            var clip = new AnimationClip { name = nombre, frameRate = 60 };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[seq.frames.Length + 1];
            float t = 0f;
            for (int i = 0; i < seq.frames.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = t, value = sprites[seq.frames[i]] };
                t += seq.duracionesMs[i] / 1000f;
            }
            // llave final = mantiene el último frame toda su duración
            keys[keys.Length - 1] = new ObjectReferenceKeyframe { time = t, value = sprites[seq.frames[seq.frames.Length - 1]] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
            ajustes.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, ajustes);

            string rutaClip = $"{DirAnim}/{nombre}.anim";
            AssetDatabase.DeleteAsset(rutaClip);
            AssetDatabase.CreateAsset(clip, rutaClip);

            // 3) animator con un solo estado "Loop"
            string rutaCtrl = $"{DirAnim}/{nombre}.controller";
            AssetDatabase.DeleteAsset(rutaCtrl);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(rutaCtrl);
            var estado = ctrl.layers[0].stateMachine.AddState("Loop");
            estado.motion = clip;
            ctrl.layers[0].stateMachine.defaultState = estado;

            // 4) prefab
            var go = new GameObject("LED_" + nombre);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[seq.frames[seq.frames.Length - 1]];
            if (material) sr.sharedMaterial = material;
            if (SortingLayer.layers.Any(l => l.name == CapaOrden)) sr.sortingLayerName = CapaOrden;
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            go.AddComponent<CartelLED>();
            PrefabUtility.SaveAsPrefabAsset(go, $"{DirPrefabs}/{nombre}.prefab");
            Object.DestroyImmediate(go);
            hechos++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Carteles] Carteles LED listos: {hechos} prefabs en {DirPrefabs}");
    }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }
}
