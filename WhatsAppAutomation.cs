using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace GoriziaUtilidades
{
    public class WhatsAppAutomation
    {
        // Ejecuta todo: basePath = carpeta con clientes.csv y los archivos
        public async Task RunAsync(string basePath, string mensaje, IProgress<string> progress, CancellationToken ct)
        {
            string csvFile = Path.Combine(basePath, "clientes.csv");
            if (!File.Exists(csvFile))
                throw new FileNotFoundException("No se encontró clientes.csv", csvFile);

            // Lee todas las líneas (codificación cp1252 como en tu script Python)
            var lines = File.ReadAllLines(csvFile, Encoding.GetEncoding(1252))
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

            var options = new ChromeOptions();
            // Ajusta la ruta del perfil para preservar la sesión y no tener que escanear QR cada vez
            options.AddArgument(@"--user-data-dir=C:\Users\Matías\AppData\Local\Google\Chrome\User Data\WhatsAppSession");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--remote-debugging-port=9222");
            // Nota: no headless, porque necesitas la UI para WhatsApp Web

            // Crea el driver (si usas Selenium.WebDriver.ChromeDriver, el ejecutable estará en output)
            using (var driver = new ChromeDriver(options))
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

                // Abrir WhatsApp Web y esperar que cargue la sesión
                driver.Navigate().GoToUrl("https://web.whatsapp.com");
                progress.Report("🔑 Abriendo WhatsApp Web. Si es la primera vez escanea el QR.");
                wait.Until(d => d.FindElements(By.XPath("//div[@aria-label='Nuevo chat']")).Count > 0);

                foreach (var line in lines)
                {
                    if (ct.IsCancellationRequested) { progress.Report("⏹️ Cancelado por el usuario."); break; }

                    var cols = ParseCsvLine(line);
                    if (cols.Length <= 4) continue;

                    string telefono = cols[1].Trim();
                    string archivo = cols[4].Trim();
                    if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(archivo)) continue;

                    string archivoPath = Path.Combine(basePath, archivo);
                    if (!File.Exists(archivoPath))
                    {
                        progress.Report($"❌ Archivo no encontrado: {archivoPath}");
                        continue;
                    }

                    try
                    {
                        // Abrir chat
                        driver.Navigate().GoToUrl($"https://web.whatsapp.com/send?phone={telefono}");
                        wait.Until(d => d.FindElements(By.XPath("//div[@contenteditable='true' and @data-tab='10']")).Count > 0);
                        await Task.Delay(2000); // tiempo extra para asegurar carga

                        // Escribir mensaje
                        var inputText = driver.FindElement(By.XPath("//div[@contenteditable='true' and @data-tab='10']"));
                        inputText.Click();
                        inputText.SendKeys(mensaje);
                        await Task.Delay(500);

                        // Adjuntar
                        var adjuntar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Adjuntar']")));
                        adjuntar.Click();

                        // Subir archivo (input[type=file])
                        var inputFile = wait.Until(d => d.FindElement(By.CssSelector("input[type='file']")));
                        inputFile.SendKeys(archivoPath);
                        await Task.Delay(1000);

                        // Enviar
                        var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Enviar']")));
                        enviar.Click();

                        progress.Report($"📤 Enviado a {telefono}: {archivo}");
                        await Task.Delay(3000); // espera entre envíos (reduce riesgo de bloqueos)
                    }
                    catch (WebDriverTimeoutException tex)
                    {
                        progress.Report($"⚠️ Timeout al enviar a {telefono}: {tex.Message}");
                        // opcional: continuar con el siguiente o reintentar
                    }
                    catch (Exception ex)
                    {
                        progress.Report($"❗ Error enviando a {telefono}: {ex.Message}");
                    }
                }

                driver.Quit();
            }
        }

        // Parser CSV simple que respeta comillas
        private string[] ParseCsvLine(string line)
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
    }

}


