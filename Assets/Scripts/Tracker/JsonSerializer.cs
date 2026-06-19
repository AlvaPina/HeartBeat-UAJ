using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JsonSerializer : ISerializer
{
    public string Serialize(EventoBase evento)
    {
        return JsonUtility.ToJson(evento);
    }
}
