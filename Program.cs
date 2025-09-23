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
            if (args.Length >= 2)
            {
                // 📌 Modo AUTOMÁTICO (se pasó CSV y mensaje como parámetros)
                string csvFile = args[0];
                string mensajeDefault = args[1];

                var automation = new WhatsAppAutomation();

                // Para mostrar avances en consola
                // Buscar forma de que se entere VFP del progreso en tiempo real
                var progreso = new Progress<string>(msg => Console.WriteLine(msg));
                var progresoBarra = new Progress<int>(p => { });

                // Ejecutar de manera síncrona el proceso
                Task.Run(() =>
                    automation.RunAsync(csvFile, mensajeDefault, progreso, progresoBarra, CancellationToken.None)
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
