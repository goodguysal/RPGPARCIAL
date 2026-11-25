using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instala Throw/Searching y el audio de consumibles en los tres aliados.</summary>
public static class InstalarAnimacionesConsumibles
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaThrow = "Assets/Otros/Throw.fbx";
    private const string RutaSearching = "Assets/Otros/Searching Files High.fbx";
    private const string RutaSonido = "Assets/DOCTOR/veneno.mp3";
    private const string ArchivoSolicitud = "InstalarAnimacionesConsumibles.once";

    [InitializeOnLoadMethod]
    private static void RevisarSolicitudPendiente()
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
                Instalar();
                File.Delete(marcador);
                Debug.Log("Animaciones y sonido de consumibles guardados para DOCTOR, CURA y ESQUELETO.");
            }
            catch (Exception excepcion) { Debug.LogException(excepcion); }
        };
    }

    private static void ReintentarAlSalirDePlay(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
        EditorApplication.delayCall += RevisarSolicitudPendiente;
    }

    [MenuItem("Tools/RPG/Instalar animaciones de consumibles")]
    public static void Instalar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de instalar las animaciones de consumibles.");

        AnimationClip throwClip = PrepararClip(RutaThrow);
        AnimationClip searchingClip = PrepararClip(RutaSearching);
        AudioClip sonido = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaSonido);
        if (sonido == null) throw new FileNotFoundException("No se encontró el sonido de consumible: " + RutaSonido);

        InstalarEnControlador("Assets/DOCTOR/DOCTOR_Combat.controller", throwClip, searchingClip);
        InstalarEnControlador("Assets/CURA/CURA_Combat.controller", throwClip, searchingClip);
        InstalarEnControlador("Assets/ESQUELETO/ESQUELETO_Combat.controller", throwClip, searchingClip);

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            int encontrados = 0;
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (PersonajeCombate personaje in raiz.GetComponentsInChildren<PersonajeCombate>(true))
                {
                    Animator animator = personaje.GetComponentInChildren<Animator>(true);
                    if (animator == null || animator.runtimeAnimatorController == null)
                        throw new InvalidOperationException("" + personaje.Nombre + " no tiene Animator Controller asignado.");
                    if (!TieneParametro(animator.runtimeAnimatorController, "ItemThrow") ||
                        !TieneParametro(animator.runtimeAnimatorController, "ItemSearching"))
                        throw new InvalidOperationException("El Animator Controller de " + personaje.Nombre + " no recibió los estados de consumibles.");

                    SerializedObject datos = new SerializedObject(personaje);
                    SerializedProperty campoSonido = datos.FindProperty("sonidoConsumible");
                    if (campoSonido == null) throw new MissingFieldException("PersonajeCombate no tiene sonidoConsumible.");
                    campoSonido.objectReferenceValue = sonido;
                    datos.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(personaje);
                    encontrados++;
                }
            }

            if (encontrados < 3) throw new InvalidOperationException("Se esperaban los tres personajes aliados en Combate.unity; encontrados: " + encontrados);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    private static AnimationClip PrepararClip(string ruta)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se encontró el FBX de animación: " + ruta);

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.autoGenerateAvatarMappingIfUnspecified = true;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) throw new InvalidOperationException("El FBX no contiene clips: " + ruta);
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip == null) continue;
            clip.loopTime = false;
            clip.lockRootHeightY = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Avatar>()
            .FirstOrDefault(a => a != null && a.isValid && a.isHuman);
        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
            .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (avatar == null || resultado == null)
            throw new InvalidOperationException("Unity no pudo importar el clip Humanoid de " + ruta + ". Revisa la pestaña Rig del FBX.");
        return resultado;
    }

    private static void InstalarEnControlador(string ruta, AnimationClip throwClip, AnimationClip searchingClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
        if (controller == null) throw new FileNotFoundException("No se encontró Animator Controller: " + ruta);
        AnimatorStateMachine maquina = controller.layers[0].stateMachine;
        AnimatorState idle = ObtenerEstado(maquina, "Idle");
        if (idle == null) throw new InvalidOperationException("El Controller no tiene estado Idle: " + ruta);

        AnimatorState lanzar = ObtenerEstado(maquina, "Throw") ?? maquina.AddState("Throw");
        AnimatorState buscar = ObtenerEstado(maquina, "Searching") ?? maquina.AddState("Searching");
        lanzar.motion = throwClip;
        buscar.motion = searchingClip;
        AgregarTriggerSiFalta(controller, "ItemThrow");
        AgregarTriggerSiFalta(controller, "ItemSearching");
        AsegurarTransicionAnyState(maquina, lanzar, "ItemThrow");
        AsegurarTransicionAnyState(maquina, buscar, "ItemSearching");
        AsegurarRetorno(idle, lanzar);
        AsegurarRetorno(idle, buscar);
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorState ObtenerEstado(AnimatorStateMachine maquina, string nombre) =>
        maquina.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == nombre);

    private static bool TieneParametro(RuntimeAnimatorController controller, string nombre)
    {
        AnimatorController editable = controller as AnimatorController;
        return editable != null && editable.parameters.Any(p => p.name == nombre);
    }

    private static void AgregarTriggerSiFalta(AnimatorController controller, string nombre)
    {
        if (!controller.parameters.Any(p => p.name == nombre))
            controller.AddParameter(nombre, AnimatorControllerParameterType.Trigger);
    }

    private static void AsegurarTransicionAnyState(AnimatorStateMachine maquina, AnimatorState destino, string trigger)
    {
        if (maquina.anyStateTransitions.Any(t => t.destinationState == destino && t.conditions.Any(c => c.parameter == trigger))) return;
        AnimatorStateTransition transicion = maquina.AddAnyStateTransition(destino);
        transicion.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        transicion.hasExitTime = false;
        transicion.duration = 0.08f;
        transicion.canTransitionToSelf = false;
    }

    private static void AsegurarRetorno(AnimatorState idle, AnimatorState accion)
    {
        if (accion.transitions.Any(t => t.destinationState == idle && t.hasExitTime)) return;
        AnimatorStateTransition transicion = accion.AddTransition(idle);
        transicion.hasExitTime = true;
        transicion.exitTime = 0.95f;
        transicion.duration = 0.1f;
    }
}
