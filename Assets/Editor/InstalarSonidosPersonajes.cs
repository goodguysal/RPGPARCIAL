using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Asigna los clips de audio del proyecto a los componentes del combate.</summary>
public static class InstalarSonidosPersonajes
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string ArchivoSolicitud = "InstalarSonidosPersonajes.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudPendiente()
    {
        EditorApplication.delayCall += () =>
        {
            string marcador = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", ArchivoSolicitud);
            if (!File.Exists(marcador)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
                EditorApplication.playModeStateChanged += ReintentarAlSalirDePlay;
                return;
            }

            try
            {
                Aplicar();
                File.Delete(marcador);
                Debug.Log("Sonidos de personajes guardados en Combate.unity.");
            }
            catch (Exception excepcion) { Debug.LogException(excepcion); }
        };
    }

    private static void ReintentarAlSalirDePlay(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
        EditorApplication.delayCall += EjecutarSolicitudPendiente;
    }

    [MenuItem("Tools/RPG/Asignar sonidos del combate")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de asignar los sonidos.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            AudioClip golpeDoctor = Cargar("Assets/Personaje/766480__rtyrtyrtyrtyrtyrtyrtyrty__knife-slash-spike-trap-sharp-sound-4.mp3");
            AudioClip magiaDoctor = Cargar("Assets/Personaje/ATK magico.wav");
            AudioClip golpeCura = Cargar("Assets/CURA/370204__nekoninja__samurai-slash.wav");
            AudioClip magiaCura = Cargar("Assets/CURA/Curar.wav");
            AudioClip impactoComun = Cargar("Assets/CURA/660770__madpancake__hit-impact.ogg");
            AudioClip golpeEsqueleto = Cargar("Assets/ESQUELETO/35213__abyssmal__slashkut.wav");
            AudioClip muerteEsqueleto = Cargar("Assets/ESQUELETO/784768__akkingstudio__deathcrunch-gore-sfx-blood-and-bone.wav");
            AudioClip sonidoVenda = Cargar("Assets/Otros/bandage.wav");
            AudioClip sonidoTonico = Cargar("Assets/Otros/Tonico.wav");

            AudioClip golpeGolem = Cargar("Assets/Enemigo/847272__highwaistedpants__wet-clay-thud.wav");
            AudioClip impactoGolem = Cargar("Assets/Enemigo/829503__qubodup__short-dog-grunt.wav");
            AudioClip muerteGolem = Cargar("Assets/Enemigo/Grunt muerte.wav");

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (PersonajeCombate personaje in raiz.GetComponentsInChildren<PersonajeCombate>(true))
                {
                    SerializedObject datos = new SerializedObject(personaje);
                    if (personaje.EsDoctor)
                        Asignar(datos, "sonidoGolpe", golpeDoctor);
                    else if (personaje.EsCura)
                        Asignar(datos, "sonidoGolpe", golpeCura);
                    else
                        Asignar(datos, "sonidoGolpe", golpeEsqueleto);

                    Asignar(datos, "sonidoRecibirGolpe", impactoComun);
                    Asignar(datos, "sonidoAtaquePM", personaje.EsDoctor ? magiaDoctor : personaje.EsCura ? magiaCura : golpeEsqueleto);
                    Asignar(datos, "sonidoMorir", personaje.EsEsqueleto ? muerteEsqueleto : impactoComun);
                    Asignar(datos, "sonidoVenda", sonidoVenda);
                    Asignar(datos, "sonidoTonico", sonidoTonico);
                    datos.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(personaje);
                }

                foreach (EnemigoCombate enemigo in raiz.GetComponentsInChildren<EnemigoCombate>(true))
                {
                    SerializedObject datos = new SerializedObject(enemigo);
                    Asignar(datos, "sonidoAtaque", golpeGolem);
                    Asignar(datos, "sonidoRecibirGolpe", impactoGolem);
                    Asignar(datos, "sonidoMorir", muerteGolem);
                    datos.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(enemigo);
                }
            }

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static AudioClip Cargar(string ruta)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
        if (clip == null) throw new FileNotFoundException("No se encontró el AudioClip: " + ruta);
        return clip;
    }

    private static void Asignar(SerializedObject datos, string propiedad, AudioClip clip)
    {
        SerializedProperty campo = datos.FindProperty(propiedad);
        if (campo == null) throw new MissingFieldException("Falta el campo serializado " + propiedad);
        campo.objectReferenceValue = clip;
    }
}
