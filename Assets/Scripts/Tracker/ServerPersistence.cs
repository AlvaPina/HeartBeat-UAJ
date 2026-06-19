using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ServerPersistence : IPersistence
{
    private readonly ISerializer serializer;
    private readonly List<string> buffer;
    private readonly string endpoint;
    private readonly MonoBehaviour coroutineRunner;

    public ServerPersistence(ISerializer serializer, string endpoint, MonoBehaviour coroutineRunner)
    {
        this.serializer = serializer;
        this.endpoint = endpoint;
        this.coroutineRunner = coroutineRunner;
        this.buffer = new List<string>();
    }

    public void Send(EventoBase evento)
    {
        if (evento == null) return;

        try
        {
            string serializedEvent = serializer.Serialize(evento);
            buffer.Add(serializedEvent);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ServerPersistence] Error en Send. Evento descartado. {ex.Message}");
        }
    }

    public void Flush()
    {
        if (buffer.Count == 0) return;

        if (!(serializer is JsonSerializer))
        {
            Debug.LogWarning("[ServerPersistence] Se recomienda usar JsonSerializer para envíos HTTP.");
        }

        string payload = BuildPayload();
        buffer.Clear();

        if (coroutineRunner != null)
            coroutineRunner.StartCoroutine(PostData(payload));
        else
            Debug.LogWarning("[ServerPersistence] No hay coroutineRunner. No se puede enviar al servidor.");
    }

    private string BuildPayload()
    {
        // JSON lines o array simple; aquí lo dejamos como array JSON manual
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < buffer.Count; i++)
        {
            sb.Append(buffer[i]);
            if (i < buffer.Count - 1)
                sb.Append(",");
        }
        sb.Append("]");
        return sb.ToString();
    }

    private IEnumerator PostData(string payload)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ServerPersistence] Error enviando datos: {request.error}");
            }
            else
            {
                Debug.Log("[ServerPersistence] Datos enviados correctamente.");
            }
        }
    }
}
