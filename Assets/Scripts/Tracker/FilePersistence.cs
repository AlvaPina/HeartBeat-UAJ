using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class FilePersistence : IPersistence
{
    private readonly ISerializer serializer;
    private readonly List<string> buffer;
    private readonly string filePath;
    private readonly bool writeHeaderIfCsv;
    private bool headerWritten;

    public FilePersistence(ISerializer serializer, string fileName = "telemetry.log", bool writeHeaderIfCsv = false)
    {
        this.serializer = serializer;
        this.buffer = new List<string>();
        this.writeHeaderIfCsv = writeHeaderIfCsv;

        filePath = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log("Telemetry path: " + filePath);
        headerWritten = File.Exists(filePath) && new FileInfo(filePath).Length > 0;
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
            Debug.LogWarning($"[FilePersistence] Error en Send. Evento descartado. {ex.Message}");
        }
    }

    public void Flush()
    {
        if (buffer.Count == 0) return;

        try
        {
            EnsureDirectoryExists();

            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                if (writeHeaderIfCsv && !headerWritten && serializer is CsvSerializer)
                {
                    writer.WriteLine(CsvConstants.Header);
                    headerWritten = true;
                }

                foreach (string line in buffer)
                    writer.WriteLine(line);
            }

            buffer.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FilePersistence] Error al hacer Flush. Los eventos siguen en memoria si no limpias el buffer manualmente. {ex.Message}");
        }
    }

    private void EnsureDirectoryExists()
    {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    public string GetPath()
    {
        return filePath;
    }
}