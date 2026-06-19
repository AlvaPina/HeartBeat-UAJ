using System;
using UnityEngine;

public enum TipoMuerte
{
    Enemy,
    Fall,
    Trap,
    Unknown
}

public enum TipoHabilidad
{
    DoubleJump,
    Magic
}

public enum ContextoUso
{
    Exploracion,
    Combate
}

public enum TipoItem
{
    Heart,
    Unknown
}

[Serializable]
public class EventoInicioSesion : EventoBase
{
    public EventoInicioSesion(int nivel) : base(TipoEvento.SesionStart, nivel) { }
}

[Serializable]
public class EventoFinSesion : EventoBase
{
    public EventoFinSesion(int nivel) : base(TipoEvento.SesionEnd, nivel) { }
}

[Serializable]
public class EventoInicioNivel : EventoBase
{
    public EventoInicioNivel(int nivel) : base(TipoEvento.LevelStart, nivel) { }
}

[Serializable]
public class EventoNivelCompletado : EventoBase
{

    public EventoNivelCompletado(int nivel) : base(TipoEvento.LevelComplete, nivel)
    {
        
    }
}

[Serializable]
public class EventoNivelFallido : EventoBase
{
    public string motivo;

    public EventoNivelFallido(int nivel, string motivo = "") : base(TipoEvento.LevelFail, nivel)
    {
        this.motivo = motivo;
    }
}

[Serializable]
public class EventoAtaque : EventoBase
{
    public Vector3 posicion;
    public float velocidad;
    public bool enAire;

    public EventoAtaque(
        int nivel,
        Vector3 posicion,
        float velocidad,
        bool enAire
    ) : base(TipoEvento.AttackPerformed, nivel)
    {
        this.posicion = posicion;
        this.velocidad = velocidad;
        this.enAire = enAire;
    }
}

[Serializable]
public class EventoInicioCombate : EventoBase
{
    public string combatAreaId;
    public Vector3 posicionJugador;

    public EventoInicioCombate(int nivel, string combatAreaId, Vector3 posicionJugador)
        : base(TipoEvento.EnemyEncounterStart, nivel)
    {
        this.combatAreaId = combatAreaId;
        this.posicionJugador = posicionJugador;
    }
}

[Serializable]
public class EventoFinCombate : EventoBase
{
    public string combatAreaId;
    public Vector3 posicionJugador;
    public bool huida;

    public EventoFinCombate(
        int nivel,
        string combatAreaId,
        Vector3 posicionJugador,
        bool huida
    ) : base(TipoEvento.EnemyEncounterEnd, nivel)
    {
        this.combatAreaId = combatAreaId;
        this.posicionJugador = posicionJugador;
        this.huida = huida;
    }
}

[Serializable]
public class EventoJugadorDañado : EventoBase
{
    public Vector3 posicion;
    public int daño;
    public string causa;
    public int vidaRestante;

    public EventoJugadorDañado(
        int nivel,
        Vector3 posicion,
        int daño,
        string causa,
        int vidaRestante
    ) : base(TipoEvento.PlayerDamaged, nivel)
    {
        this.posicion = posicion;
        this.daño = daño;
        this.causa = causa;
        this.vidaRestante = vidaRestante;
    }
}

[Serializable]
public class EventoJugadorMuere : EventoBase
{
    public Vector3 posicion;
    public TipoMuerte tipoMuerte;

    public EventoJugadorMuere(
        int nivel,
        Vector3 posicion,
        TipoMuerte tipoMuerte
    ) : base(TipoEvento.PlayerDeath, nivel)
    {
        this.posicion = posicion;
        this.tipoMuerte = tipoMuerte;
    }
}

[Serializable]
public class EventoJugadorRespawn : EventoBase
{
    public float tiempoDesdeMuerte;
    public Vector3 posicionRespawn;

    public EventoJugadorRespawn(
        int nivel,
        float tiempoDesdeMuerte,
        Vector3 posicionRespawn
    ) : base(TipoEvento.PlayerRespawn, nivel)
    {
        this.tiempoDesdeMuerte = tiempoDesdeMuerte;
        this.posicionRespawn = posicionRespawn;
    }
}

[Serializable]
public class EventoItemSpawneado : EventoBase
{
    public TipoItem tipoItem;
    public Vector3 posicion;
    public string itemInstanceId;

    public EventoItemSpawneado(
        int nivel,
        TipoItem tipoItem,
        Vector3 posicion,
        string itemInstanceId
    ) : base(TipoEvento.ItemSpawned, nivel)
    {
        this.tipoItem = tipoItem;
        this.posicion = posicion;
        this.itemInstanceId = itemInstanceId;
    }
}

[Serializable]
public class EventoItemRecogido : EventoBase
{
    public TipoItem tipoItem;
    public Vector3 posicion;
    public string itemInstanceId;

    public EventoItemRecogido(
        int nivel,
        TipoItem tipoItem,
        Vector3 posicion,
        string itemInstanceId
    ) : base(TipoEvento.ItemCollected, nivel)
    {
        this.tipoItem = tipoItem;
        this.posicion = posicion;
        this.itemInstanceId = itemInstanceId;
    }
}

[Serializable]
public class EventoHabilidadUsada : EventoBase
{
    public TipoHabilidad tipoHabilidad;
    public ContextoUso contexto;
    public Vector3 posicion;

    public EventoHabilidadUsada(
        int nivel,
        TipoHabilidad tipoHabilidad,
        ContextoUso contexto,
        Vector3 posicion
    ) : base(TipoEvento.AbilityUsed, nivel)
    {
        this.tipoHabilidad = tipoHabilidad;
        this.contexto = contexto;
        this.posicion = posicion;
    }
}

[Serializable]
public class EventoHabilidadDesbloqueada : EventoBase
{
    public TipoHabilidad tipoHabilidad;

    public EventoHabilidadDesbloqueada(int nivel, TipoHabilidad tipoHabilidad)
        : base(TipoEvento.AbilityUnlocked, nivel)
    {
        this.tipoHabilidad = tipoHabilidad;
    }
}

[Serializable]
public class EventoMagiaAcierto : EventoBase
{
    public Vector3 posicionJugador;
    public Vector3 posicionImpacto;
    public string objetivo;

    public EventoMagiaAcierto(
        int nivel,
        Vector3 posicionJugador,
        Vector3 posicionImpacto,
        string objetivo
    ) : base(TipoEvento.MagicProjectileHit, nivel)
    {
        this.posicionJugador = posicionJugador;
        this.posicionImpacto = posicionImpacto;
        this.objetivo = objetivo;
    }
}

[Serializable]
public class EventoMagiaFallo : EventoBase
{
    public Vector3 posicionJugador;
    public string motivo;

    public EventoMagiaFallo(
        int nivel,
        Vector3 posicionJugador,
        string motivo = ""
    ) : base(TipoEvento.MagicProjectileMiss, nivel)
    {
        this.posicionJugador = posicionJugador;
        this.motivo = motivo;
    }
}

[Serializable]
public class EventoPosicionJugador : EventoBase
{
    public Vector3 posicion;
    public float velocidad;

    public EventoPosicionJugador(int nivel, Vector3 posicion, float velocidad)
        : base(TipoEvento.PlayerPositionSample, nivel)
    {
        this.posicion = posicion;
        this.velocidad = velocidad;
    }
}