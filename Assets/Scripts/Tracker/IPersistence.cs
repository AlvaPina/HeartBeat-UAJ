using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPersistence
{
    void Send(EventoBase evento);
    void Flush();
}
