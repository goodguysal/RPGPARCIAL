using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Estado y decisiones básicas del enemigo del encuentro.</summary>
public sealed class EnemigoCombate : MonoBehaviour
{
    [Header("Enemigo")]
    [SerializeField] private string nombre = "Acechador de turba";
    [SerializeField, Min(1)] private int vidaMaxima = 42;
    [SerializeField, Min(0)] private int vidaInicial = 42;

    [Header("Decisiones")]
    [SerializeField, Range(0f, 1f)] private float probabilidadGolpeFuerte = 0.25f;
    [SerializeField, Range(0f, 1f)] private float probabilidadGolpeFuerteHerido = 0.55f;
    [SerializeField] private AtaqueEnemigo[] ataques =
    {
        new AtaqueEnemigo("Zarpazo", 5, 8),
        new AtaqueEnemigo("Embate de turba", 8, 12)
    };

    public string Nombre => nombre;
    public int VidaMaxima => vidaMaxima;
    public int Vida { get; private set; }

    private void Awake() => Reiniciar();

    public void Reiniciar() => Vida = Mathf.Clamp(vidaInicial, 0, vidaMaxima);

    public void RecibirDano(int cantidad) => Vida = Mathf.Max(0, Vida - Mathf.Max(0, cantidad));

    /// <summary>Elige ataque y objetivo, pero reserva el daño para el momento del impacto.</summary>
    public AccionEnemigaPendiente PrepararAccion(PersonajeCombate[] posiblesObjetivos, PersonajeCombate objetivoProtegido = null)
    {
        List<PersonajeCombate> vivos = new List<PersonajeCombate>();
        if (posiblesObjetivos != null)
            foreach (PersonajeCombate candidato in posiblesObjetivos)
                if (candidato != null && candidato.isActiveAndEnabled && candidato.Vida > 0)
                    vivos.Add(candidato);

        if (vivos.Count == 0 || ataques == null || ataques.Length == 0)
            return default;

        PersonajeCombate objetivo = ElegirObjetivo(vivos);
        AtaqueEnemigo ataque = ElegirAtaque();
        int dano = UnityEngine.Random.Range(ataque.danoMinimo, ataque.danoMaximo + 1);
        bool defendido = objetivo == objetivoProtegido;
        if (defendido) dano = Mathf.CeilToInt(dano * 0.5f);
        return new AccionEnemigaPendiente(ataque.nombre, objetivo, dano, defendido);
    }

    public ResultadoAccionEnemiga EjecutarAccion(AccionEnemigaPendiente accion)
    {
        if (accion.objetivo == null || !accion.objetivo.isActiveAndEnabled || accion.objetivo.Vida <= 0)
            return default;
        accion.objetivo.RecibirDano(accion.dano);
        return new ResultadoAccionEnemiga(accion.nombreAtaque, accion.objetivo, accion.dano, accion.objetivoDefendido);
    }

    private PersonajeCombate ElegirObjetivo(List<PersonajeCombate> objetivos)
    {
        PersonajeCombate elegido = objetivos[0];
        float mejorPuntaje = float.MinValue;
        foreach (PersonajeCombate objetivo in objetivos)
        {
            float proporcionVida = objetivo.VidaMaxima <= 0 ? 0f : (float)objetivo.Vida / objetivo.VidaMaxima;
            float puntaje = (1f - proporcionVida) * 0.7f + UnityEngine.Random.value * 0.3f;
            if (puntaje > mejorPuntaje) { mejorPuntaje = puntaje; elegido = objetivo; }
        }
        return elegido;
    }

    private AtaqueEnemigo ElegirAtaque()
    {
        if (ataques.Length == 1) return ataques[0];
        float vidaProporcional = vidaMaxima <= 0 ? 1f : (float)Vida / vidaMaxima;
        float probabilidad = vidaProporcional <= 0.5f ? probabilidadGolpeFuerteHerido : probabilidadGolpeFuerte;
        return UnityEngine.Random.value < probabilidad ? ataques[1] : ataques[0];
    }

    [Serializable]
    private struct AtaqueEnemigo
    {
        public string nombre;
        [Min(0)] public int danoMinimo;
        [Min(0)] public int danoMaximo;
        public AtaqueEnemigo(string nombre, int danoMinimo, int danoMaximo)
        { this.nombre = nombre; this.danoMinimo = danoMinimo; this.danoMaximo = danoMaximo; }
    }
}

public readonly struct AccionEnemigaPendiente
{
    public readonly string nombreAtaque;
    public readonly PersonajeCombate objetivo;
    public readonly int dano;
    public readonly bool objetivoDefendido;
    public AccionEnemigaPendiente(string nombreAtaque, PersonajeCombate objetivo, int dano, bool objetivoDefendido)
    { this.nombreAtaque = nombreAtaque; this.objetivo = objetivo; this.dano = dano; this.objetivoDefendido = objetivoDefendido; }
}

public readonly struct ResultadoAccionEnemiga
{
    public readonly string nombreAtaque;
    public readonly PersonajeCombate objetivo;
    public readonly int dano;
    public readonly bool objetivoDefendido;
    public ResultadoAccionEnemiga(string nombreAtaque, PersonajeCombate objetivo, int dano, bool objetivoDefendido)
    { this.nombreAtaque = nombreAtaque; this.objetivo = objetivo; this.dano = dano; this.objetivoDefendido = objetivoDefendido; }
}
