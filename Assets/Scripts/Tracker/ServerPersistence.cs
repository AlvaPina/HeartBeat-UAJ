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
    private bool sending = false;

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
        if (buffer.Count == 0 || sending) return;

        if (!(serializer is JsonSerializer))
        {
            Debug.LogWarning("[ServerPersistence] Se recomienda usar JsonSerializer para envíos HTTP.");
        }

        if (coroutineRunner == null)
        {
            Debug.LogWarning("[ServerPersistence] No hay coroutineRunner. No se puede enviar al servidor.");
            return;
        }

        List<string> pending = new List<string>(buffer);
        buffer.Clear();
        string payload = BuildPayload(pending);
        coroutineRunner.StartCoroutine(PostData(payload, pending));
    }

    private string BuildPayload(List<string> lines)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < lines.Count; i++)
        {
            sb.Append(lines[i]);
            if (i < lines.Count - 1)
                sb.Append(",");
        }
        sb.Append("]");
        return sb.ToString();
    }

    private IEnumerator PostData(string payload, List<string> pending)
    {
        sending = true;
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);

        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // Reinsertar al principio para no perder telemetría en fallos de red.
                buffer.InsertRange(0, pending);
                Debug.LogWarning($"[ServerPersistence] Error enviando datos. Se reintentará en el siguiente flush: {request.error}");
            }
            else
            {
                Debug.Log("[ServerPersistence] Datos enviados correctamente.");
            }
        }

        sending = false;
    }
}
