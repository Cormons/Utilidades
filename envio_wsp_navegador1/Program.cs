using GoriziaEnviadorUnitario;
using System;
using System.IO;
using System.Threading;

namespace GoriziaUtilidades
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Uso: programa.exe <ruta> [navegador] [tiempo] [celular] [mensaje]");
                    return 1;
                }

                var automation = new WhatsAppAutomation();
                var progreso = new Progress<string>(msg => Console.WriteLine(msg));

                // PARÁMETROS BASE
                string ruta = args[0];
                string navegador = args.Length >= 2 ? args[1] : "c";
                int tiempo = 0;
                if (args.Length >= 3) int.TryParse(args[2], out tiempo);

                // --- DETECCIÓN DE ESCENARIOS ---

                bool esDirecto = args.Length >= 5; // Si vienen celular y mensaje
                bool esCSV = ruta.ToLower().EndsWith(".csv");
                bool esCarpeta = !Path.HasExtension(ruta);

                if (esCSV && !esDirecto)
                {
                    // ESCENARIO 1: RUTA + .CSV (Modo lectura masiva)
                    automation.Run(ruta, progreso, null, CancellationToken.None, navegador, tiempo);
                }
                else if (esDirecto)
                {
                    // ESCENARIO 2 y 3: MODO UNITARIO (Viene Celular y Mensaje)
                    string celular = args[3];
                    string mensaje = args[4].Replace("/n", Environment.NewLine);
                    string archivoAdjunto = null;

                    if (esCarpeta)
                    {
                        // ESCENARIO 2: Es solo una ruta (carpeta), no hay archivo para adjuntar
                        archivoAdjunto = null;
                    }
                    else
                    {
                        // ESCENARIO 3: Es una ruta con archivo (que no es .csv), se adjunta
                        archivoAdjunto = ruta;
                    }

                    var cliente = new ContactoInfo
                    {
                        Telefono = celular,
                        Mensaje = mensaje,
                        Archivo = archivoAdjunto
                    };

                    // Ejecutamos pasándole la 'ruta' como referencia para el log
                    automation.RunSingle(cliente, ruta, navegador, tiempo);
                }
                else
                {
                    Console.WriteLine("Error: Parámetros insuficientes para el modo directo.");
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }
    }
}