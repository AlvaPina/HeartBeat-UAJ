using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class CsvConstants
{
    public const string Header = "\"timestamp\",\"idEvento\",\"idSesion\",\"tipoEvento\",\"nivel\",\"payload\"";
}

public class CsvSerializer : ISerializer
{
    public string Serialize(EventoBase evento)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append(Escape(evento.timestamp.ToString()));
        sb.Append(",");
        sb.Append(Escape(evento.idEvento.ToString()));
        sb.Append(",");
        sb.Append(Escape(evento.idSesion ?? ""));
        sb.Append(",");
        sb.Append(Escape(evento.tipoEvento ?? ""));
        sb.Append(",");
        sb.Append(Escape(evento.nivel.ToString()));
        sb.Append(",");
        sb.Append(Escape(GetExtraFieldsAsKeyValue(evento)));

        return sb.ToString();
    }

    private string GetExtraFieldsAsKeyValue(EventoBase evento)
    {
        var baseFields = new HashSet<string>(
            typeof(EventoBase)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
        );

        var allFields = evento.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        StringBuilder sb = new StringBuilder();

        foreach (var field in allFields)
        {
            if (baseFields.Contains(field.Name))
                continue;

            object value = field.GetValue(evento);
            sb.Append(field.Name);
            sb.Append("=");
            sb.Append(FormatValue(value));
            sb.Append(";");
        }

        return sb.ToString();
    }

    private string FormatValue(object value)
    {
        if (value == null) return "null";

        if (value is Vector3 v)
            return $"({v.x:F3},{v.y:F3},{v.z:F3})";

        return value.ToString();
    }

    private string Escape(string value)
    {
        if (value == null) return "\"\"";

        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }
}