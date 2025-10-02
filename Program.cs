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
                    Console.WriteLine("❌ Debe proporcionar el archivo CSV como argumento.");
                    return 1;  // código de error
                }

                string csvFile = args[0];
                string navegador = args.Length >= 2 ? args[1].Trim().ToLower() : "c";

                var automation = new WhatsAppAutomation();
                var progreso = new Progress<string>(msg => Console.WriteLine(msg));
                var progresoBarra = new Progress<int>(p => { });

                Console.WriteLine($"[DEBUG] Navegador solicitado: {navegador}");

                // Ejecutar RunAsync de forma síncrona
                Task.Run(() =>
                    automation.RunAsync(csvFile, progreso, progresoBarra, CancellationToken.None, navegador)
                ).GetAwaiter().GetResult();

                // Todo salió bien
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                return 1;
            }
        }
    }
}
