using System;
using UnityEngine;

[Serializable]
public abstract class EventoBase
{
    public long timestamp;
    public int idEvento;
    public string idSesion;
    public string tipoEvento;
    public int nivel;

    private static int numeroEventosBase = 0;

    protected EventoBase(TipoEvento tipo, int nivel)
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        idEvento = numeroEventosBase++;
        idSesion = string.Empty;
        tipoEvento = tipo.ToString();
        this.nivel = nivel;
    }

    public void AsignarSesion(string nuevaSesion)
    {
        idSesion = nuevaSesion ?? string.Empty;
    }
}
