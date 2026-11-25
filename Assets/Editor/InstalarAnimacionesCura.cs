using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Configura las animaciones Humanoid y el bastón del personaje CURA.</summary>
public static class InstalarAnimacionesCura
{
    private const string RutaEscena = "Assets/Scenes/Combate.unity";
    private const string RutaAvatar = "Assets/Polytope Studio/Lowpoly_Characters/Sources/Meshes/PT_Priest_StPatrick.fbx";
    private const string RutaIdle = "Assets/CURA/Old Man Idle.fbx";
    private const string RutaSmash = "Assets/CURA/Smash.fbx";
    private const string RutaHeal = "Assets/CURA/Magic Heal.fbx";
    private const string RutaAtaqueMagico = "Assets/CURA/Standing 1H Magic Attack 01.fbx";
    private const string RutaGolpe = "Assets/Personaje/Hit To Body.fbx";
    private const string RutaMuerte = "Assets/Personaje/Standing React Death Backward.fbx";
    private const string RutaControlador = "Assets/CURA/CURA_Combat.controller";
    private const string RutaBaston = "Assets/Polytope Studio/Lowpoly_Weapons/Prefabs/PT_Staff_StPatrick_01.prefab";
    private const string RutaTexturaBaston = "Assets/Polytope Studio/Lowpoly_Weapons/Sources/Textures/PT_Weapons_Texture_01_base.png";
    private const string RutaMaterialBaston = "Assets/Materials/Combat/CURA_Staff_URP.mat";
    private const string NombreSolicitud = "InstalarAnimacionesCura.once";

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
            Debug.Log("Animator Controller, animaciones y bastón asignados a CURA.");
        }
        catch (Exception excepcion) { Debug.LogException(excepcion); }
    }

    private static void ReanudarAlVolverAEdicion(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= ReanudarAlVolverAEdicion;
        EditorApplication.delayCall += EjecutarSolicitudSegura;
    }

    [MenuItem("Tools/RPG/Asignar animaciones y bastón a CURA")]
    public static void InstalarEnEscena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Sal de Play Mode antes de instalar animaciones de CURA en la escena.");
        // Garantiza que el modelo PRIEST ya esté instalado antes de buscar su Animator.
        InstalarCura.Aplicar();

        Scene escena = SceneManager.GetSceneByPath(RutaEscena);
        bool abiertaAqui = !escena.IsValid() || !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        try
        {
            GameObject cura = Buscar(escena, "CURA");
            if (cura == null) throw new InvalidOperationException("No se encontró CURA en Combate.unity.");
            Animator animator = cura.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("El modelo PRIEST no contiene Animator.");

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RutaAvatar).OfType<Avatar>().FirstOrDefault(a => a != null && a.isHuman);
            if (avatar == null) throw new InvalidOperationException("No se encontró un Avatar Humanoid válido para PRIEST.");
            if (!animator.isHuman && animator.avatar != null && !animator.avatar.isHuman)
                throw new InvalidOperationException("El Animator del modelo PRIEST no acepta un Avatar Humanoid.");

            AnimationClip idle = PrepararClip(RutaIdle, true, false);
            AnimationClip smash = PrepararClip(RutaSmash, false, false);
            AnimationClip heal = PrepararClip(RutaHeal, false, false);
            // Mantiene el root ligado a la altura de los pies durante Rayo sagrado,
            // evitando que el retarget del movimiento lo empuje bajo el suelo.
            AnimationClip ataqueMagico = PrepararClip(RutaAtaqueMagico, false, true);
            AnimationClip golpe = PrepararClipCompartido(RutaGolpe, false, false);
            AnimationClip muerte = PrepararClipCompartido(RutaMuerte, false, true);
            AnimatorController controller = CrearControlador(idle, smash, heal, ataqueMagico, golpe, muerte);

            Undo.RecordObject(animator, "Asignar animaciones Humanoid a CURA");
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            InstalarBaston(animator, escena);

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena))
                throw new InvalidOperationException("Unity no pudo guardar Assets/Scenes/Combate.unity.");
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }

    [MenuItem("Tools/RPG/Asignar animaciones y bastón a CURA", true)]
    private static bool PuedeInstalar() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static AnimationClip PrepararClip(string ruta, bool loop, bool fijarAlturaRaiz)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se pudo leer el importador de animación: " + ruta);

        // Los FBX de Giant Animations tienen una jerarquía PT_NPC_* distinta a PRIEST.
        // Cada archivo necesita un Avatar de origen propio; el Animator de CURA retargetea luego sobre el Avatar PRIEST.
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        importer.autoGenerateAvatarMappingIfUnspecified = true;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) throw new InvalidOperationException("El FBX no tiene clips: " + ruta);
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip == null) continue;
            clip.loopTime = loop;
            clip.lockRootHeightY = fijarAlturaRaiz;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
        Avatar avatarOrigen = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<Avatar>()
            .FirstOrDefault(avatar => avatar != null && avatar.isValid && avatar.isHuman);
        if (avatarOrigen == null)
            throw new InvalidOperationException("Unity no pudo crear un Avatar Humanoid propio para " + ruta + ". Revisa la pestaña Rig del FBX.");

        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (resultado == null)
            throw new InvalidOperationException("Unity no importó AnimationClip desde " + ruta + ". El Avatar Humanoid de origen se creó, pero el FBX no expuso ningún clip.");
        return resultado;
    }

    private static AnimationClip PrepararClipCompartido(string ruta, bool loop, bool fijarAlturaRaiz)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se pudo leer el importador de animación: " + ruta);
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) throw new InvalidOperationException("El FBX no tiene clips: " + ruta);
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip == null) continue;
            clip.loopTime = loop;
            clip.lockRootHeightY = fijarAlturaRaiz;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        AnimationClip resultado = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (resultado == null) throw new InvalidOperationException("Unity no importó AnimationClip desde " + ruta);
        return resultado;
    }

    private static AnimatorController CrearControlador(AnimationClip idleClip, AnimationClip smashClip,
        AnimationClip healClip, AnimationClip ataqueMagicoClip, AnimationClip hitClip, AnimationClip deathClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        if (controller == null) throw new InvalidOperationException("No se pudo crear CURA_Combat.controller.");

        AnimatorStateMachine maquina = controller.layers[0].stateMachine;
        AnimatorState idle = ObtenerEstado(maquina, "Idle");
        if (idle == null && maquina.states.Length > 0)
        {
            idle = maquina.states[0].state;
            idle.name = "Idle";
        }
        if (idle == null) idle = maquina.AddState("Idle");
        AnimatorState smash = ObtenerEstado(maquina, "Smash") ?? maquina.AddState("Smash");
        AnimatorState heal = ObtenerEstado(maquina, "Heal") ?? maquina.AddState("Heal");
        AnimatorState ataqueMagico = ObtenerEstado(maquina, "MagicAttack") ?? maquina.AddState("MagicAttack");
        AnimatorState hit = ObtenerEstado(maquina, "Hit") ?? maquina.AddState("Hit");
        AnimatorState death = ObtenerEstado(maquina, "Death") ?? maquina.AddState("Death");

        idle.motion = idleClip;
        smash.motion = smashClip;
        heal.motion = healClip;
        ataqueMagico.motion = ataqueMagicoClip;
        hit.motion = hitClip;
        death.motion = deathClip;
        maquina.defaultState = idle;

        AgregarTriggerSiFalta(controller, "Smash");
        AgregarTriggerSiFalta(controller, "Heal");
        AgregarTriggerSiFalta(controller, "MagicAttack");
        AgregarTriggerSiFalta(controller, "Hit");
        AgregarTriggerSiFalta(controller, "Death");
        AsegurarTransicionAnyState(maquina, smash, "Smash");
        AsegurarTransicionAnyState(maquina, heal, "Heal");
        AsegurarTransicionAnyState(maquina, ataqueMagico, "MagicAttack");
        AsegurarTransicionAnyState(maquina, hit, "Hit");
        AsegurarTransicionAnyState(maquina, death, "Death");
        AsegurarRetorno(idle, smash);
        AsegurarRetorno(idle, heal);
        AsegurarRetorno(idle, ataqueMagico);
        AsegurarRetorno(idle, hit);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void InstalarBaston(Animator animator, Scene escena)
    {
        Transform mano = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (mano == null) throw new InvalidOperationException("No se encontró la mano derecha del Avatar PRIEST.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaBaston);
        if (prefab == null) throw new InvalidOperationException("No se encontró el prefab del bastón St Patrick.");

        foreach (Transform hijo in animator.GetComponentsInChildren<Transform>(true))
            if (hijo.name == "StaffModel") Undo.DestroyObjectImmediate(hijo.gameObject);

        GameObject baston = PrefabUtility.InstantiatePrefab(prefab, escena) as GameObject;
        if (baston == null) throw new InvalidOperationException("No se pudo instanciar el bastón St Patrick.");
        Undo.RegisterCreatedObjectUndo(baston, "Añadir bastón al PRIEST");
        baston.name = "StaffModel";
        baston.transform.SetParent(mano, false);
        // Posición y orientación tomadas del punto de montaje derecho del prefab de demostración del PRIEST.
        baston.transform.localPosition = new Vector3(-0.0193496f, -0.099109754f, -0.023399422f);
        baston.transform.localRotation = new Quaternion(0.65023524f, 0.7249253f, -0.1828025f, 0.13513233f);

        Material material = ObtenerMaterialBaston();
        Renderer[] renderers = baston.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materiales = renderer.sharedMaterials;
            for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
            renderer.sharedMaterials = materiales;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Material ObtenerMaterialBaston()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialBaston);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("No se encontró el shader URP/Lit para el bastón.");
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTexturaBaston);
        material = new Material(shader) { name = "CURA_Staff_URP" };
        if (textura != null) material.SetTexture("_BaseMap", textura);
        material.SetColor("_BaseColor", new Color(0.66f, 0.43f, 0.22f, 1f));
        material.SetFloat("_Metallic", 0.12f);
        material.SetFloat("_Smoothness", 0.32f);
        AssetDatabase.CreateAsset(material, RutaMaterialBaston);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static AnimatorState ObtenerEstado(AnimatorStateMachine maquina, string nombre)
    {
        foreach (ChildAnimatorState hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == nombre) return hijo.state;
        return null;
    }

    private static void AgregarTriggerSiFalta(AnimatorController controller, string nombre)
    {
        if (!controller.parameters.Any(parametro => parametro.name == nombre))
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

    private static GameObject Buscar(Scene escena, string nombre)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform elemento in raiz.GetComponentsInChildren<Transform>(true))
                if (elemento.name == nombre) return elemento.gameObject;
        return null;
    }
}
