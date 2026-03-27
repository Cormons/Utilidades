using System;
using System.Threading;
using System.Threading.Tasks;

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
                    Console.WriteLine("Debe proporcionar el archivo CSV como argumento.");
                    Console.WriteLine("Uso: programa.exe <archivo.csv> [navegador] [tiempoConfirmacion]");
                    Console.WriteLine("  navegador: c=Chrome, f=Firefox, e=Edge (default: c)");
                    Console.WriteLine("  tiempoConfirmacion: 0=esperar tilde, >0=segundos de espera (default: 0)");
                    return 1;
                }

                string csvFile = args[0];

                // Navegador (parámetro 2)
                string navegador = args.Length >= 2 ? args[1].Trim().ToLower() : "c";

                // Tiempo de confirmación (parámetro 3)
                int tiempoConfirmacion = 0; // Default: esperar tilde
                if (args.Length >= 3)
                {
                    if (!int.TryParse(args[2], out tiempoConfirmacion) || tiempoConfirmacion < 0)
                    {
                        Console.WriteLine("Tiempo de confirmación inválido, usando default (0 = esperar tilde)");
                        tiempoConfirmacion = 0;
                    }
                }

                var automation = new WhatsAppAutomation();

                var progreso = new Progress<string>(msg =>
                {
                    Console.WriteLine(msg);
                });

                var progresoBarra = new Progress<int>(p => { });

                Console.WriteLine($"[DEBUG] Navegador: {navegador}");
                Console.WriteLine($"[DEBUG] Tiempo confirmación: {(tiempoConfirmacion == 0 ? "Esperar tilde" : $"{tiempoConfirmacion} segundos")}");

                // Ejecutar RunAsync con el nuevo parámetro
                Task.Run(() =>
                    automation.Run(csvFile, progreso, progresoBarra, CancellationToken.None, navegador, tiempoConfirmacion)
                ).GetAwaiter().GetResult();

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
        }
    }
}
