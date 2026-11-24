using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Prepara las animaciones y el Animator Controller del personaje DOCTOR.</summary>
public static class InstalarAnimacionesDoctor
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaAvatar = "Assets/Polytope Studio/Lowpoly_Characters/Sources/Meshes/PT_Plague_Doctor.fbx";
    private const string RutaIdle = "Assets/Personaje/Ready Idle.fbx";
    private const string RutaAtaque = "Assets/Personaje/Boxing.fbx";
    private const string RutaGolpe = "Assets/Personaje/Hit To Body.fbx";
    private const string RutaMuerte = "Assets/Personaje/Standing React Death Backward.fbx";
    private const string RutaControlador = "Assets/Personaje/DOCTOR_Combat.controller";
    private const string NombreSolicitud = "InstalarAnimacionesDoctor.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudUnica()
    {
        EditorApplication.delayCall += EjecutarSolicitudSegura;
    }

    private static void EjecutarSolicitudSegura()
    {
        string solicitud = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", NombreSolicitud);
        if (!File.Exists(solicitud)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ReanudarAlVolverAEdicion;
            EditorApplication.playModeStateChanged += ReanudarAlVolverAEdicion;
            return;
        }
        try
        {
            InstalarEnEscena();
            File.Delete(solicitud);
            AssetDatabase.Refresh();
            Debug.Log("Animator Controller y animaciones asignados a DOCTOR.");
        }
        catch (Exception excepcion) { Debug.LogException(excepcion); }
    }

    private static void ReanudarAlVolverAEdicion(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReanudarAlVolverAEdicion;
        EditorApplication.delayCall += EjecutarSolicitudSegura;
    }

    [MenuItem("Tools/RPG/Asignar animaciones a DOCTOR")]
    public static void InstalarEnEscena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de instalar animaciones de DOCTOR en la escena.");
        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        if (!escena.IsValid() || !escena.isLoaded)
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        GameObject doctor = BuscarEnEscena(escena, "DOCTOR");
        if (doctor == null) throw new InvalidOperationException("No se encontró DOCTOR en Assets/Scenes/Combate.unity.");

        Animator animator = doctor.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidOperationException("DOCTOR no tiene un Animator en su modelo hijo.");
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RutaAvatar).OfType<Avatar>().FirstOrDefault(a => a.isHuman);
        if (avatar == null)
            throw new InvalidOperationException("No se encontró el Avatar Humanoid del modelo DOCTOR en " + RutaAvatar);
        if (animator.avatar == null || !animator.avatar.isHuman)
            throw new InvalidOperationException("El Animator de DOCTOR no tiene un Avatar Humanoid válido.");

        AnimationClip idle = PrepararClip(RutaIdle, true, false);
        AnimationClip ataque = PrepararClip(RutaAtaque, false, false);
        AnimationClip golpe = PrepararClip(RutaGolpe, false, false);
        AnimationClip muerte = PrepararClip(RutaMuerte, false, true);
        AnimatorController controller = CrearControlador(idle, ataque, golpe, muerte);

        Undo.RecordObject(animator, "Asignar Animator Controller a DOCTOR");
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);

        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena))
            throw new InvalidOperationException("Unity no pudo guardar Assets/Scenes/Combate.unity.");
    }

    [MenuItem("Tools/RPG/Asignar animaciones a DOCTOR", true)]
    private static bool PuedeInstalar() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static AnimationClip PrepararClip(string ruta, bool loop, bool fijarAlturaRaiz)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se pudo leer el importador de animación: " + ruta);

        bool cambios = importer.animationType != ModelImporterAnimationType.Human ||
                       importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                       !importer.autoGenerateAvatarMappingIfUnspecified;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.autoGenerateAvatarMappingIfUnspecified = true;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (clip == null) continue;
                clip.loopTime = loop;
                clip.lockRootHeightY = fijarAlturaRaiz;
            }
            importer.clipAnimations = clips;
            cambios = true;
        }

        if (cambios) importer.SaveAndReimport();

        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (resultado == null) throw new InvalidOperationException("No se encontró un AnimationClip importado en " + ruta);
        return resultado;
    }

    private static AnimatorController CrearControlador(AnimationClip idleClip, AnimationClip ataqueClip,
        AnimationClip golpeClip, AnimationClip muerteClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        if (controller == null) throw new InvalidOperationException("No se pudo crear DOCTOR_Combat.controller.");

        AnimatorStateMachine maquina = controller.layers[0].stateMachine;
        AnimatorState idle = ObtenerEstado(maquina, "Idle");
        if (idle == null && maquina.states.Length > 0)
        {
            idle = maquina.states[0].state;
            idle.name = "Idle";
        }
        if (idle == null) idle = maquina.AddState("Idle");
        AnimatorState ataque = ObtenerEstado(maquina, "Attack") ?? maquina.AddState("Attack");
        AnimatorState golpe = ObtenerEstado(maquina, "Hit") ?? maquina.AddState("Hit");
        AnimatorState muerte = ObtenerEstado(maquina, "Death") ?? maquina.AddState("Death");

        idle.motion = idleClip;
        ataque.motion = ataqueClip;
        golpe.motion = golpeClip;
        muerte.motion = muerteClip;
        maquina.defaultState = idle;

        AgregarTriggerSiFalta(controller, "Attack");
        AgregarTriggerSiFalta(controller, "Hit");
        AgregarTriggerSiFalta(controller, "Death");
        AsegurarTransicionAnyState(maquina, ataque, "Attack");
        AsegurarTransicionAnyState(maquina, golpe, "Hit");
        AsegurarTransicionAnyState(maquina, muerte, "Death");
        AsegurarRetorno(idle, ataque);
        AsegurarRetorno(idle, golpe);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState ObtenerEstado(AnimatorStateMachine maquina, string nombre)
    {
        foreach (ChildAnimatorState hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == nombre) return hijo.state;
        return null;
    }

    private static void AgregarTriggerSiFalta(AnimatorController controller, string nombre)
    {
        if (controller.parameters.Any(parametro => parametro.name == nombre)) return;
        controller.AddParameter(nombre, AnimatorControllerParameterType.Trigger);
    }

    private static void AsegurarTransicionAnyState(AnimatorStateMachine maquina, AnimatorState destino, string trigger)
    {
        foreach (AnimatorStateTransition existente in maquina.anyStateTransitions)
            if (existente.destinationState == destino && existente.conditions.Any(condicion => condicion.parameter == trigger)) return;

        AnimatorStateTransition transicion = maquina.AddAnyStateTransition(destino);
        transicion.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        transicion.hasExitTime = false;
        transicion.duration = 0.08f;
        transicion.canTransitionToSelf = false;
    }

    private static void AsegurarRetorno(AnimatorState idle, AnimatorState accion)
    {
        foreach (AnimatorStateTransition existente in accion.transitions)
            if (existente.destinationState == idle && existente.hasExitTime) return;

        AnimatorStateTransition transicion = accion.AddTransition(idle);
        transicion.hasExitTime = true;
        transicion.exitTime = 0.95f;
        transicion.duration = 0.1f;
    }

    private static GameObject BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Transform elemento in raiz.GetComponentsInChildren<Transform>(true))
                if (elemento.name == nombre) return elemento.gameObject;
        }
        return null;
    }
}
