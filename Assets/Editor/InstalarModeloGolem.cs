using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Agrega el prefab URP del gólem al enemigo de la escena de combate.</summary>
public static class InstalarModeloGolem
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaPrefab = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Prefabs/URP/URP - Giant01 - Golem.prefab";
    private const string RutaModeloAvatar = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Models/GiantModel01 - Golem.fbx";
    private const string RutaAnimacionIdle = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Animations/Giant@Idle01.fbx";
    private const string RutaAnimacionAtaque = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Animations/Combat/Giant@UnarmedAttack01.fbx";
    private const string RutaAnimacionGolpe = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Animations/Combat/Giant@Damage01.fbx";
    private const string RutaControlador = "Assets/Kevin Iglesias/Characters/Humanoid Giant/Demo Scene/Animator Controllers/GOLEM_Combat.controller";
    private const string NombreSolicitud = "InstalarModeloGolem.once";
    private const float AlturaModeloMundo = 3.2f;

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitudUnica()
    {
        EditorApplication.delayCall += () =>
        {
            string solicitud = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", NombreSolicitud);
            if (!File.Exists(solicitud)) return;

            try
            {
                InstalarEnEscena();
                File.Delete(solicitud);
                AssetDatabase.Refresh();
                Debug.Log("Modelo GOLEM asignado al enemigo en Combate.unity.");
            }
            catch (Exception excepcion)
            {
                Debug.LogException(excepcion);
            }
        };
    }

    [MenuItem("Tools/RPG/Asignar modelo GOLEM al enemigo")]
    public static void InstalarEnEscena()
    {
        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        if (!escena.IsValid() || !escena.isLoaded)
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        GameObject enemigo = BuscarEnEscena(escena, "Enemigo");
        GameObject jugador = BuscarEnEscena(escena, "DOCTOR");
        GameObject suelo = BuscarEnEscena(escena, "Cube");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (enemigo == null) throw new InvalidOperationException("No se encontró Enemigo en Combate.unity.");
        if (jugador == null) throw new InvalidOperationException("No se encontró DOCTOR en Combate.unity.");
        if (prefab == null) throw new InvalidOperationException("No se encontró el prefab URP del GOLEM: " + RutaPrefab);

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RutaModeloAvatar).OfType<Avatar>().FirstOrDefault(a => a.isHuman);
        if (avatar == null) throw new InvalidOperationException("No se encontró el Avatar Humanoid del GOLEM en " + RutaModeloAvatar);
        AnimationClip idleClip = PrepararClip(RutaAnimacionIdle, "Giant@Idle01", avatar);
        AnimationClip ataqueClip = PrepararClip(RutaAnimacionAtaque, "Giant@UnarmedAttack01", avatar);
        AnimationClip golpeClip = PrepararClip(RutaAnimacionGolpe, "Giant@Damage01", avatar);
        AnimatorController controlador = CrearControlador(idleClip, ataqueClip, golpeClip);

        Transform anterior = enemigo.transform.Find("GolemModel");
        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);

        GameObject modelo = PrefabUtility.InstantiatePrefab(prefab, escena) as GameObject;
        if (modelo == null) throw new InvalidOperationException("Unity no pudo crear una instancia del prefab GOLEM.");
        Undo.RegisterCreatedObjectUndo(modelo, "Asignar modelo GOLEM al enemigo");
        modelo.name = "GolemModel";

        Renderer[] renderers = modelo.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("El prefab GOLEM no contiene renderers.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y <= 0.001f) throw new InvalidOperationException("No se pudo calcular la altura del modelo GOLEM.");

        float escalaUniforme = AlturaModeloMundo / bounds.size.y;
        float alturaSuelo = 0f;
        Renderer rendererSuelo = suelo != null ? suelo.GetComponent<Renderer>() : null;
        if (rendererSuelo != null) alturaSuelo = rendererSuelo.bounds.max.y;

        modelo.transform.position = new Vector3(
            enemigo.transform.position.x,
            alturaSuelo - bounds.min.y * escalaUniforme,
            enemigo.transform.position.z);
        Vector3 direccion = jugador.transform.position - modelo.transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.001f)
            modelo.transform.rotation = Quaternion.LookRotation(direccion.normalized, Vector3.up);

        modelo.transform.SetParent(enemigo.transform, true);
        Vector3 escalaPadre = enemigo.transform.lossyScale;
        modelo.transform.localScale = new Vector3(
            escalaUniforme / Mathf.Max(0.001f, escalaPadre.x),
            escalaUniforme / Mathf.Max(0.001f, escalaPadre.y),
            escalaUniforme / Mathf.Max(0.001f, escalaPadre.z));

        Animator animator = modelo.GetComponentInChildren<Animator>(true);
        if (animator == null) throw new InvalidOperationException("El prefab GOLEM no contiene un Animator.");
        Undo.RecordObject(animator, "Asignar Animator Controller al GOLEM");
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controlador;
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);

        Renderer placeholder = enemigo.GetComponent<MeshRenderer>();
        if (placeholder != null)
        {
            Undo.RecordObject(placeholder, "Ocultar cubo de prueba del enemigo");
            placeholder.enabled = false;
        }

        EnemigoCombate datos = enemigo.GetComponent<EnemigoCombate>();
        if (datos != null)
        {
            SerializedObject serializado = new SerializedObject(datos);
            serializado.FindProperty("nombre").stringValue = "GOLEM";
            serializado.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(enemigo);
        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena))
            throw new InvalidOperationException("Unity no pudo guardar Assets/Scenes/Combate.unity.");
    }

    private static GameObject BuscarEnEscena(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            Transform[] transforms = raiz.GetComponentsInChildren<Transform>(true);
            foreach (Transform elemento in transforms)
                if (elemento.name == nombre) return elemento.gameObject;
        }
        return null;
    }

    private static AnimationClip PrepararClip(string ruta, string nombre, Avatar avatar)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se pudo leer el importador de animación: " + ruta);

        if (importer.animationType != ModelImporterAnimationType.Human ||
            importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther ||
            importer.sourceAvatar != avatar)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.SaveAndReimport();
        }

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == nombre);
        if (clip == null) throw new InvalidOperationException("No se encontró el clip " + nombre + " en " + ruta);
        return clip;
    }

    private static AnimatorController CrearControlador(AnimationClip idleClip, AnimationClip ataqueClip, AnimationClip golpeClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        if (controller == null) throw new InvalidOperationException("No se pudo crear el Animator Controller del GOLEM.");

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

        idle.motion = idleClip;
        ataque.motion = ataqueClip;
        golpe.motion = golpeClip;
        maquina.defaultState = idle;

        AgregarTriggerSiFalta(controller, "Attack");
        AgregarTriggerSiFalta(controller, "Hit");
        AsegurarTransicionAnyState(maquina, ataque, "Attack");
        AsegurarTransicionAnyState(maquina, golpe, "Hit");
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
        if (controller.parameters.Any(p => p.name == nombre)) return;
        controller.AddParameter(nombre, AnimatorControllerParameterType.Trigger);
    }

    private static void AsegurarTransicionAnyState(AnimatorStateMachine maquina, AnimatorState destino, string trigger)
    {
        foreach (AnimatorStateTransition existente in maquina.anyStateTransitions)
            if (existente.destinationState == destino && existente.conditions.Any(c => c.parameter == trigger)) return;

        AnimatorStateTransition transicion = maquina.AddAnyStateTransition(destino);
        transicion.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        transicion.hasExitTime = false;
        transicion.duration = 0.05f;
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
}
