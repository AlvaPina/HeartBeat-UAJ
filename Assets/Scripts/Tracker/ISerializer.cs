using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISerializer
{
    string Serialize(EventoBase evento);
}
