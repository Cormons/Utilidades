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
            //MessageBox.Show("▶ Iniciando programa", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try
            {
                if (args.Length == 0)
                {
                    //MessageBox.Show("❌ No se recibieron parámetros.\nDebe proporcionar el archivo CSV como argumento.", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Console.WriteLine("Debe proporcionar el archivo CSV como argumento.");
                    return 1;
                }

                //MessageBox.Show($"✔ Parámetros recibidos:\n{string.Join("\n", args)}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

                string csvFile = args[0];
                //MessageBox.Show($"📂 Archivo CSV recibido: {csvFile}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

                string navegador = args.Length >= 2 ? args[1].Trim().ToLower() : "c";
                //MessageBox.Show($"🌐 Navegador recibido: {navegador}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

                var automation = new WhatsAppAutomation();
                //MessageBox.Show("⚙ Instanciado WhatsAppAutomation", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

                var progreso = new Progress<string>(msg =>
                {
                    Console.WriteLine(msg);
                    //MessageBox.Show($"📢 Progreso: {msg}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });

                var progresoBarra = new Progress<int>(p =>
                {
                    //MessageBox.Show($"📊 Barra de progreso: {p}%", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });

                Console.WriteLine($"[DEBUG] Navegador solicitado: {navegador}");
                //MessageBox.Show($"▶ Llamando a RunAsync con:\nCSV: {csvFile}\nNavegador: {navegador}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Ejecutar RunAsync de forma síncrona
                Task.Run(() =>
                    automation.Run(csvFile, progreso, progresoBarra, CancellationToken.None, navegador)
                ).GetAwaiter().GetResult();

                //MessageBox.Show("✅ RunAsync terminó correctamente", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Todo salió bien
                //MessageBox.Show("🏁 Programa terminó BIEN", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                //MessageBox.Show($"💥 ERROR en Main:\n{ex.Message}", "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

    }
}

