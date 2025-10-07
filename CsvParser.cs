using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace GoriziaUtilidades
{
    public class CsvParser
    {
        public static List<ContactoInfo> ParseFile(string path)
        {
            var result = new List<ContactoInfo>();
            var lines = File.ReadAllLines(path, Encoding.GetEncoding(1252))
                            .Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                var cols = ParseCsvLine(line);

                // Validar que tenga exactamente 4 columnas
                if (cols.Length != 4)
                {
                    result.Add(new ContactoInfo
                    {
                        Telefono = cols.Length > 0 ? cols[0].Trim() : "",
                        Estado = $"ERROR: Fila mal estructurada. Se esperaban 4 columnas, se encontraron {cols.Length}"
                    });
                    continue;
                }

                //if (cols.Length < 4) continue;
                var record = new ContactoInfo
                {
                    Telefono = cols[0].Trim(),
                    Importe = cols[1].Trim(),
                    Mensaje = cols[2].Trim(),
                    Archivo = cols[3].Trim(),
                    //LinkPago = cols.Length > 5 ? cols[5].Trim() : ""
                };

                // Validar que el teléfono tenga exactamente 13 dígitos
                if (record.Telefono.Length != 13 || !record.Telefono.All(char.IsDigit))
                {
                    record.Estado = $"ERROR: Teléfono inválido. Debe tener exactamente 13 dígitos numéricos";
                }
                // agregar link de pago al mensaje si existe
                //if (!string.IsNullOrWhiteSpace(record.LinkPago))
                //record.Mensaje += "\n💳 Pagar rápido: " + record.LinkPago;
                result.Add(record);
            }
            return result;
        }
        public static string[] ParseCsvLine(string line)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { parts.Add(sb.ToString()); sb.Clear(); continue;}
                sb.Append(c);
            }
            parts.Add(sb.ToString());
            return parts.ToArray();
        }

        public static string EscaparCsv(string campo)
        {
            if (string.IsNullOrEmpty(campo))
                return "\"\"";

            return "\"" + campo.Replace("\"", "\"\"") + "\"";
        }

    }
}