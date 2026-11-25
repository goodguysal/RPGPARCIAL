using System;
using System.Collections.Generic;
using UnityEngine;

public enum AccionVFXCombate
{
    AtaqueBasicoDoctor,
    AtaquePesadoDoctor,
    GolpeImpulsorDoctor,
    SanacionDoctor,
    GolpeBaculoCura,
    RayoSagradoCura,
    RegenerarManaCura,
    AtaqueBasicoEsqueleto,
    AtaquePesadoEsqueleto,
    GuardiaReflejaEsqueleto,
    Defenderse,
    Venda,
    Tonico,
    SalesDeCombate,
    Veneno,
    EsenciaBestia,
    AceleradorATB,
    GolpeGolem
}

[Serializable]
public sealed class VFXAccionCombate
{
    [Tooltip("Acción que activa este efecto.")]
    public AccionVFXCombate accion;
    [Tooltip("Prefab VFX opcional para esta acción.")]
    public GameObject prefab;
    [Tooltip("Si está activo y se pasó un objetivo, el efecto aparece junto a ese objetivo; si no, aparece junto al personaje que actúa.")]
    public bool usarObjetivo;
    [Tooltip("Mantiene el efecto unido al personaje donde apareció.")]
    public bool seguirPuntoDeAparicion;
    public Vector3 desplazamiento;
    public Vector3 rotacionEuler;
    [Min(0f), Tooltip("Segundos antes de destruir la instancia. Cero la deja hasta que el propio VFX termine o se destruya.")]
    public float duracionSegundos = 4f;
}

[CreateAssetMenu(fileName = "VFX_Personaje", menuName = "RPG/Configuración VFX de personaje")]
public sealed class ConfiguracionVFXCombate : ScriptableObject
{
    public string personaje;
    public List<VFXAccionCombate> acciones = new List<VFXAccionCombate>();

    public GameObject BuscarPrefab(AccionVFXCombate accion)
    {
        VFXAccionCombate entrada = acciones.Find(item => item != null && item.accion == accion);
        return entrada != null ? entrada.prefab : null;
    }

    public VFXAccionCombate Buscar(AccionVFXCombate accion) =>
        acciones.Find(item => item != null && item.accion == accion);
}
