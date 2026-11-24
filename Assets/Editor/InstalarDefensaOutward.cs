using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>Asigna Outward Block a los combatientes y normaliza sus nombres y PV en la escena.</summary>
public static class InstalarDefensaOutward
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaClip = "Assets/Personaje/Outward Block.fbx";
    private const string RutaDoctor = "Assets/Personaje/DOCTOR_Combat.controller";
    private const string RutaCura = "Assets/CURA/CURA_Combat.controller";
    private const string RutaEsqueleto = "Assets/ESQUELETO/ESQUELETO_Combat.controller";
    private const string RutaGolem = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Demo Scene/Animator Controllers/GOLEM_Combat.controller";
    private const string Solicitud = "InstalarDefensaOutward.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudUnica() => EditorApplication.delayCall += EjecutarSolicitudSegura;

    private static void EjecutarSolicitudSegura()
    {
        string archivo = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", Solicitud);
        if (!File.Exists(archivo)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ReintentarAlVolverAEdicion;
            EditorApplication.playModeStateChanged += ReintentarAlVolverAEdicion;
            return;
        }
        try
        {
            Instalar();
            File.Delete(archivo);
            Debug.Log("Outward Block asignado a DOCTOR, CURA, ESQUELETO y GOLEM; nombres y vida actualizados.");
        }
        catch (Exception excepcion) { Debug.LogException(excepcion); }
    }

    private static void ReintentarAlVolverAEdicion(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlVolverAEdicion;
        EditorApplication.delayCall += EjecutarSolicitudSegura;
    }

    [MenuItem("Tools/RPG/Asignar Outward Block y actualizar nombres y vida")]
    public static void Instalar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de instalar las animaciones.");

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            AnimationClip clip = PrepararClip();
            ConfigurarPersonaje(escena, "DOCTOR", "DOCTOR", RutaDoctor, clip);
            ConfigurarPersonaje(escena, "CURA", "CURA", RutaCura, clip);
            ConfigurarPersonaje(escena, "ESQUELETO", "ESQUELETO", RutaEsqueleto, clip);

            GameObject objetoEnemigo = Buscar(escena, "Enemigo");
            if (objetoEnemigo == null) objetoEnemigo = Buscar(escena, "GOLEM");
            if (objetoEnemigo == null) throw new InvalidOperationException("No se encontró el enemigo en Combate.unity.");
            EnemigoCombate enemigo = objetoEnemigo.GetComponent<EnemigoCombate>();
            if (enemigo == null) throw new InvalidOperationException("El enemigo no tiene EnemigoCombate.");
            ConfigurarEnemigo(enemigo);
            Animator animatorEnemigo = objetoEnemigo.GetComponentInChildren<Animator>(true);
            if (animatorEnemigo == null) throw new InvalidOperationException("El GOLEM no tiene Animator.");
            AsignarDefensa(RutaGolem, clip);
            animatorEnemigo.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaGolem);
            EditorUtility.SetDirty(animatorEnemigo);

            TMP_Text etiqueta = Buscar(escena, "EnemyName")?.GetComponent<TMP_Text>();
            if (etiqueta != null) etiqueta.text = "GOLEM";
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Assets/Scenes/Combate.unity.");
            AssetDatabase.SaveAssets();
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    [MenuItem("Tools/RPG/Asignar Outward Block y actualizar nombres y vida", true)]
    private static bool PuedeInstalar() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static void ConfigurarPersonaje(Scene escena, string objeto, string nombre,
        string rutaControlador, AnimationClip clip)
    {
        GameObject raiz = Buscar(escena, objeto);
        if (raiz == null) throw new InvalidOperationException("No se encontró " + objeto + " en Combate.unity.");
        PersonajeCombate estado = raiz.GetComponent<PersonajeCombate>();
        if (estado == null) throw new InvalidOperationException(objeto + " no tiene PersonajeCombate.");
        Undo.RecordObject(estado, "Actualizar nombre de " + nombre);
        SerializedObject serializado = new SerializedObject(estado);
        serializado.FindProperty("nombre").stringValue = nombre;
        serializado.ApplyModifiedProperties();
        Animator animator = raiz.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidOperationException(objeto + " no tiene Animator.");
        AsignarDefensa(rutaControlador, clip);
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(rutaControlador);
        EditorUtility.SetDirty(animator);
    }

    private static void ConfigurarEnemigo(EnemigoCombate enemigo)
    {
        Undo.RecordObject(enemigo, "Actualizar vida y nombre del GOLEM");
        SerializedObject serializado = new SerializedObject(enemigo);
        serializado.FindProperty("nombre").stringValue = "GOLEM";
        serializado.FindProperty("vidaMaxima").intValue = 180;
        serializado.FindProperty("vidaInicial").intValue = 180;
        serializado.ApplyModifiedProperties();
        EditorUtility.SetDirty(enemigo);
    }

    private static AnimationClip PrepararClip()
    {
        ModelImporter importer = AssetImporter.GetAtPath(RutaClip) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se encontró el importador de " + RutaClip);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.autoGenerateAvatarMappingIfUnspecified = true;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) throw new InvalidOperationException("Outward Block no contiene clips de animación.");
        foreach (ModelImporterClipAnimation clip in clips)
            if (clip != null) clip.loopTime = false;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        AssetDatabase.ImportAsset(RutaClip, ImportAssetOptions.ForceSynchronousImport);
        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(RutaClip)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (resultado == null) throw new InvalidOperationException("Unity no importó el AnimationClip de Outward Block.");
        return resultado;
    }

    private static void AsignarDefensa(string ruta, AnimationClip clip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
        if (controller == null) throw new InvalidOperationException("No se encontró Animator Controller: " + ruta);
        AnimatorStateMachine maquina = controller.layers[0].stateMachine;
        AnimatorState idle = ObtenerEstado(maquina, "Idle");
        if (idle == null) throw new InvalidOperationException("El controlador no tiene estado Idle: " + ruta);
        AnimatorState defensa = ObtenerEstado(maquina, "Defend") ?? maquina.AddState("Defend");
        defensa.motion = clip;
        AsegurarTrigger(controller, "Defend");
        AsegurarTransicion(maquina, defensa, "Defend");
        AsegurarRetorno(defensa, idle);
        EditorUtility.SetDirty(controller);
    }

    private static void AsegurarTrigger(AnimatorController controller, string nombre)
    {
        AnimatorControllerParameter actual = controller.parameters.FirstOrDefault(p => p.name == nombre);
        if (actual != null)
        {
            if (actual.type != AnimatorControllerParameterType.Trigger)
                throw new InvalidOperationException("El parámetro " + nombre + " existe con un tipo que no es Trigger.");
            return;
        }
        controller.AddParameter(nombre, AnimatorControllerParameterType.Trigger);
    }

    private static void AsegurarTransicion(AnimatorStateMachine maquina, AnimatorState destino, string trigger)
    {
        foreach (AnimatorStateTransition existente in maquina.anyStateTransitions)
            if (existente.destinationState == destino && existente.conditions.Any(c => c.parameter == trigger)) return;
        AnimatorStateTransition transicion = maquina.AddAnyStateTransition(destino);
        transicion.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        transicion.hasExitTime = false;
        transicion.duration = 0.06f;
        transicion.canTransitionToSelf = false;
    }

    private static void AsegurarRetorno(AnimatorState accion, AnimatorState idle)
    {
        foreach (AnimatorStateTransition existente in accion.transitions)
            if (existente.destinationState == idle && existente.hasExitTime) return;
        AnimatorStateTransition transicion = accion.AddTransition(idle);
        transicion.hasExitTime = true;
        transicion.exitTime = 0.95f;
        transicion.duration = 0.08f;
    }

    private static AnimatorState ObtenerEstado(AnimatorStateMachine maquina, string nombre)
    {
        foreach (ChildAnimatorState hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == nombre) return hijo.state;
        return null;
    }

    private static GameObject Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform hijo in raiz.GetComponentsInChildren<Transform>(true))
                if (hijo.name == nombre) return hijo.gameObject;
        return null;
    }
}
