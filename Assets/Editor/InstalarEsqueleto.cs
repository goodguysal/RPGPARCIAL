using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Instala el modelo, props, animaciones y ficha de ESQUELETO en el combate.</summary>
public static class InstalarEsqueleto
{
    private const string EscenaPath = "Assets/Scenes/Combate.unity";
    private const string PrefabPath = "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Skeleton/PT_Skeleton_Male_Modular.prefab";
    private const string RutaAnimaciones = "Assets/ESQUELETO/";
    private const string RutaArmas = "Assets/Polytope Studio/Lowpoly_Weapons/Prefabs/";
    private const string ControladorPath = "Assets/ESQUELETO/ESQUELETO_Combat.controller";
    private const string Solicitud = "InstalarEsqueleto.once";

    [InitializeOnLoadMethod]
    private static void EjecutarSolicitud()
    {
        EditorApplication.delayCall += EjecutarSiSolicitado;
    }

    private static void EjecutarSiSolicitado()
    {
        string archivo = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "restore", Solicitud);
        if (!File.Exists(archivo)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
            EditorApplication.playModeStateChanged += ReintentarAlSalirDePlay;
            return;
        }
        try
        {
            Instalar();
            File.Delete(archivo);
            Debug.Log("ESQUELETO instalado con modelo, espada, escudo, animaciones y contraataque.");
        }
        catch (Exception ex) { Debug.LogException(ex); }
    }

    private static void ReintentarAlSalirDePlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReintentarAlSalirDePlay;
        EditorApplication.delayCall += EjecutarSiSolicitado;
    }

    [MenuItem("Tools/RPG/Instalar animaciones y equipo de ESQUELETO")]
    public static void Instalar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de instalar ESQUELETO.");
        Scene escena = SceneManager.GetSceneByPath(EscenaPath);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(EscenaPath, OpenSceneMode.Additive);
        try
        {
            GameObject raiz = Buscar(escena, "ESQUELETO");
            if (raiz == null) throw new InvalidOperationException("No se encontró ESQUELETO en Combate.unity.");
            PersonajeCombate estado = raiz.GetComponent<PersonajeCombate>();
            if (estado == null) throw new InvalidOperationException("ESQUELETO necesita el componente PersonajeCombate.");

            ConfigurarEstado(estado);
            Transform modelo = raiz.transform.Find("SkeletonModel");
            if (modelo == null) modelo = InstalarModelo(raiz, escena);
            Animator animator = modelo.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("El prefab Skeleton no tiene Animator Humanoid.");
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("El prefab Skeleton no tiene un Avatar Humanoid válido.");

            AnimationClip idle = PrepararClip("Sword And Shield Idle.fbx", true);
            AnimationClip slash = PrepararClip("Sword And Shield Slash.fbx", false);
            AnimationClip block = PrepararClip("Sword And Shield Block.fbx", false);
            AnimationClip impact = PrepararClip("Sword And Shield Impact.fbx", false);
            AnimationClip death = PrepararClip("Sword And Shield Death.fbx", false);
            AnimatorController controller = CrearControlador(idle, slash, block, impact, death);
            Undo.RecordObject(animator, "Asignar Animator Controller a ESQUELETO");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            InstalarArma(animator, "PT_Sword_01_a.prefab", "SwordModel", HumanBodyBones.RightHand,
                new Vector3(0f, -0.03f, 0.02f), Quaternion.Euler(0f, 0f, -90f));
            InstalarArma(animator, "PT_Shield_01_a.prefab", "ShieldModel", HumanBodyBones.LeftHand,
                new Vector3(0f, -0.02f, 0.01f), Quaternion.Euler(0f, 0f, 90f));
            VincularFicha(escena, estado);
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) throw new InvalidOperationException("No se pudo guardar Combate.unity.");
        }
        finally { if (abiertaAqui) EditorSceneManager.CloseScene(escena, true); }
    }

    [MenuItem("Tools/RPG/Instalar animaciones y equipo de ESQUELETO", true)]
    private static bool PuedeInstalar() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static void ConfigurarEstado(PersonajeCombate estado)
    {
        Undo.RecordObject(estado, "Configurar ESQUELETO");
        SerializedObject so = new SerializedObject(estado);
        so.FindProperty("nombre").stringValue = "ESQUELETO";
        so.FindProperty("vidaMaxima").intValue = 40;
        so.FindProperty("vidaInicial").intValue = 40;
        so.FindProperty("manaMaximo").intValue = 30;
        so.FindProperty("manaInicial").intValue = 24;
        so.FindProperty("iniciativa").intValue = 18;
        so.FindProperty("esCura").boolValue = false;
        so.FindProperty("esEsqueleto").boolValue = true;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(estado);
    }

    private static Transform InstalarModelo(GameObject raiz, Scene escena)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("No se encontró el prefab Skeleton: " + PrefabPath);
        GameObject doctor = Buscar(escena, "DOCTOR");
        Renderer[] renderersDoctor = doctor != null ? doctor.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        float alturaReferencia = renderersDoctor.Length > 0 ? CombinarBounds(renderersDoctor).size.y : 1.8f;
        Transform[] hijos = raiz.transform.Cast<Transform>().ToArray();
        foreach (Transform hijo in hijos) Undo.DestroyObjectImmediate(hijo.gameObject);
        Renderer rendererRaiz = raiz.GetComponent<Renderer>();
        if (rendererRaiz != null) rendererRaiz.enabled = false;

        GameObject instancia = PrefabUtility.InstantiatePrefab(prefab, escena) as GameObject;
        if (instancia == null) throw new InvalidOperationException("Unity no pudo instanciar el modelo Skeleton.");
        Undo.RegisterCreatedObjectUndo(instancia, "Instalar modelo Skeleton");
        instancia.name = "SkeletonModel";
        instancia.transform.SetParent(raiz.transform, true);
        Renderer[] nuevos = instancia.GetComponentsInChildren<Renderer>(true);
        if (nuevos.Length == 0) throw new InvalidOperationException("El prefab Skeleton no tiene mallas visibles.");
        Bounds boundsNuevos = CombinarBounds(nuevos);
        if (alturaReferencia > 0.001f && boundsNuevos.size.y > 0.001f)
            instancia.transform.localScale *= alturaReferencia / boundsNuevos.size.y;
        boundsNuevos = CombinarBounds(nuevos);
        Vector3 posicionBase = raiz.transform.position;
        instancia.transform.position += new Vector3(posicionBase.x - boundsNuevos.center.x,
            posicionBase.y - boundsNuevos.min.y, posicionBase.z - boundsNuevos.center.z);
        return instancia.transform;
    }

    private static AnimationClip PrepararClip(string archivo, bool loop)
    {
        string ruta = RutaAnimaciones + archivo;
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se encontró el FBX de animación " + ruta);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.autoGenerateAvatarMappingIfUnspecified = true;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) throw new InvalidOperationException("El FBX no contiene clips: " + ruta);
        foreach (ModelImporterClipAnimation clip in clips) if (clip != null) clip.loopTime = loop;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        Avatar avatarOrigen = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Avatar>()
            .FirstOrDefault(a => a != null && a.isValid && a.isHuman);
        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
            .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (avatarOrigen == null || resultado == null)
            throw new InvalidOperationException("Unity no pudo importar clip y Avatar Humanoid válidos desde " + ruta);
        return resultado;
    }

    private static AnimatorController CrearControlador(AnimationClip idleClip, AnimationClip slashClip,
        AnimationClip blockClip, AnimationClip impactClip, AnimationClip deathClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControladorPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControladorPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = Estado(sm, "Idle") ?? sm.AddState("Idle");
        AnimatorState slash = Estado(sm, "Slash") ?? sm.AddState("Slash");
        AnimatorState block = Estado(sm, "Shield Block") ?? Estado(sm, "Block") ?? sm.AddState("Shield Block");
        block.name = "Shield Block";
        AnimatorState impact = Estado(sm, "Impact") ?? sm.AddState("Impact");
        AnimatorState death = Estado(sm, "Death") ?? sm.AddState("Death");
        idle.motion = idleClip; slash.motion = slashClip; block.motion = blockClip;
        impact.motion = impactClip; death.motion = deathClip; sm.defaultState = idle;
        Trigger(controller, "Attack"); Trigger(controller, "Block"); Trigger(controller, "Hit"); Trigger(controller, "Death");
        Transicion(sm, slash, "Attack"); Transicion(sm, block, "Block");
        Transicion(sm, impact, "Hit"); Transicion(sm, death, "Death");
        Retorno(idle, slash); Retorno(idle, block); Retorno(idle, impact);
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        return controller;
    }

    private static void InstalarArma(Animator animator, string archivo, string nombre,
        HumanBodyBones hueso, Vector3 posicion, Quaternion rotacion)
    {
        Transform mano = animator.GetBoneTransform(hueso);
        if (mano == null) throw new InvalidOperationException("El Avatar Skeleton no tiene el hueso " + hueso);
        foreach (Transform hijo in animator.GetComponentsInChildren<Transform>(true))
            if (hijo.name == nombre) Undo.DestroyObjectImmediate(hijo.gameObject);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaArmas + archivo);
        if (prefab == null) throw new InvalidOperationException("No se encontró el prefab de equipo " + archivo);
        GameObject objeto = PrefabUtility.InstantiatePrefab(prefab, animator.gameObject.scene) as GameObject;
        if (objeto == null) throw new InvalidOperationException("No se pudo instanciar " + archivo);
        Undo.RegisterCreatedObjectUndo(objeto, "Equipar " + nombre);
        objeto.name = nombre;
        objeto.transform.SetParent(mano, false);
        objeto.transform.localPosition = posicion;
        objeto.transform.localRotation = rotacion;
    }

    private static void VincularFicha(Scene escena, PersonajeCombate personaje)
    {
        PersonajeCardUI[] fichas = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PersonajeCardUI>(true))
            .OrderBy(f => ((RectTransform)f.transform).anchoredPosition.x).ToArray();
        if (fichas.Length < 3) throw new InvalidOperationException("No se encontró la tercera tarjeta de aliado para ESQUELETO.");
        Undo.RecordObject(fichas[2], "Vincular ficha de ESQUELETO");
        fichas[2].Vincular(personaje);
        EditorUtility.SetDirty(fichas[2]);
    }

    private static AnimatorState Estado(AnimatorStateMachine sm, string nombre) =>
        sm.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == nombre);

    private static void Trigger(AnimatorController controller, string nombre)
    {
        if (!controller.parameters.Any(p => p.name == nombre))
            controller.AddParameter(nombre, AnimatorControllerParameterType.Trigger);
    }

    private static void Transicion(AnimatorStateMachine sm, AnimatorState destino, string trigger)
    {
        if (sm.anyStateTransitions.Any(t => t.destinationState == destino && t.conditions.Any(c => c.parameter == trigger))) return;
        AnimatorStateTransition t = sm.AddAnyStateTransition(destino);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
    }

    private static void Retorno(AnimatorState idle, AnimatorState accion)
    {
        if (accion.transitions.Any(t => t.destinationState == idle && t.hasExitTime)) return;
        AnimatorStateTransition t = accion.AddTransition(idle);
        t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.1f;
    }

    private static Bounds CombinarBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static GameObject Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t.gameObject;
        return null;
    }
}
