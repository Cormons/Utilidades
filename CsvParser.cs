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
                if (cols.Length < 5) continue;

                var record = new ContactoInfo
                {
                    Telefono = cols[1].Trim(),
                    Importe = cols[2].Trim(),
                    Mensaje = cols[3].Trim(),
                    Archivo = cols[4].Trim(),
                    //LinkPago = cols.Length > 5 ? cols[5].Trim() : ""
                };

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
                if (c == ',' && !inQuotes) { parts.Add(sb.ToString()); sb.Clear(); continue; }
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
