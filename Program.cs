using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoriziaUtilidades
{
    internal static class Program
    {
        [STAThread] // Necesario para que funcionen bien ventanas y controles de Windows
        static void Main(string[] args)
        {
            if (args.Length >= 1)
            {
                // 📌 Modo AUTOMÁTICO (se pasó CSV y mensaje como parámetros)
                string csvFile = args[0];
                string navegador = args.Length >= 2 ? args[1].Trim().ToLower() : "chrome";
                // string mensajeDefault = args.Length >= 2 ? args[1] : "Hola";

                var automation = new WhatsAppAutomation();

                // Para mostrar avances en consola
                // Buscar forma de que se entere VFP del progreso en tiempo real
                var progreso = new Progress<string>(msg => Console.WriteLine(msg));
                var progresoBarra = new Progress<int>(p => { });

                // Ejecutar de manera síncrona el proceso
                Console.WriteLine($"[DEBUG] Navegador solicitado: {navegador}");
                Task.Run(() =>
                    automation.RunAsync(csvFile, progreso, progresoBarra, CancellationToken.None, navegador)
                ).GetAwaiter().GetResult();
            }
            else
            {
                // 📌 Modo MANUAL (sin parámetros, abre la ventana)
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }
    }
}
