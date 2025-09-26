using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GoriziaUtilidades
{
    public class WhatsAppAutomation
    {
        public async Task RunAsync(string csvFile, string mensajeDefault, IProgress<string> progreso, IProgress<int> progressBar, CancellationToken ct)
        {
            if (!File.Exists(csvFile))
                throw new FileNotFoundException("No se encontró el archivo CSV", csvFile);

            string folder = Path.GetDirectoryName(csvFile);
            // Lee todas las líneas (codificación cp1252) y omite líneas vacías o en blanco 
            var lines = File.ReadAllLines(csvFile, Encoding.GetEncoding(1252))
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList(); // Convertir a lista para contar y recorrer
            progreso.Report($"📑 Se leyeron {lines.Count} filas del CSV");

            if (lines.Count == 0)
            {
                progreso.Report("⚠️ No hay filas para procesar. Se cancela la ejecución.");
                return; 
            }

            // --- A partir de aquí: abrir Chrome ---
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;
            service.SuppressInitialDiagnosticInformation = true;

            var options = new ChromeOptions();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromeProfile = Path.Combine(userProfile, @"Google\Chrome\User Data\WhatsAppSession");
            options.AddArgument("--user-data-dir=" + chromeProfile); 
            options.AddArgument("--disable-dev-shm-usage"); 
            options.AddArgument("--no-sandbox");
            options.AddArgument("--remote-debugging-port=9222"); 
            progreso.Report("🚀 Iniciando navegador Chrome...");

            using (var driver = new ChromeDriver(service, options))
            {
                driver.Manage().Window.Maximize();

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(120));

                driver.Navigate().GoToUrl("https://web.whatsapp.com");
                progreso.Report("🔑 Abriendo WhatsApp Web. Si es la primera vez escanea el QR.");
                wait.Until(d => d.FindElements(By.XPath("//div[@aria-label='Nuevo chat']")).Count > 0);

                int total = lines.Count;
                int processed = 0;

                foreach (var line in lines)
                {
                    if (ct.IsCancellationRequested) { progreso.Report("⏹️ Cancelado por el usuario."); break; }  
                    var cols = ParseCsvLine(line);
                    if (cols.Length <= 4) continue;

                    string nombre = cols[0].Trim();
                    string telefono = cols[1].Trim();
                    string importe = cols[2].Trim();
                    string mensajeCliente = cols[3].Trim();
                    string archivo = cols[4].Trim();
                    if (string.IsNullOrWhiteSpace(mensajeCliente))
                        mensajeCliente = mensajeDefault;

                    // Obtenemos el link de pago 
                    string linkPago = cols.Length > 5 ? cols[5].Trim() : "";
                    if (!string.IsNullOrWhiteSpace(linkPago))
                        mensajeCliente += "\n💳 Pagar rápido: " + linkPago;

                    if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(archivo)) continue;
                    progreso.Report($"⚠️ Datos incompletos en línea: {line}");

                    string archivoPath = Path.Combine(folder, archivo);
                    if (!File.Exists(archivoPath))
                    {
                        progreso.Report($"❌ Archivo no encontrado: {archivoPath}");
                        continue;
                    }

                    processed++;
                    int percent = (int)((processed / (double)total) * 100);
                    progressBar?.Report(percent);

                    try
                    {
                        // Abrir chat sin recargar toda la página
                        // Buscamos el cuadro de búsqueda de contactos y escribimos el teléfono allí
                        
                        //var searchBox = wait.Until(d => d.FindElement(By.XPath("//div[@contenteditable='true' and @data-tab='3']")));
                        
                        // Revisar si es conveniente poner tiempo de espera
                        var searchBox = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-label='Cuadro de texto para ingresar la búsqueda']")));
                        
                        searchBox.Click();
                        searchBox.Clear();
                        searchBox.SendKeys(telefono + Keys.Enter);

                        // Esperar que se abra el chat
                        // wait.Until(d => d.FindElements(By.XPath("//div[@contenteditable='true' and @data-tab='10']")).Count > 0);
                        // Esperar que se abra el chat (caja de texto para escribir)
                        wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                        await Task.Delay(2000); // espera extra por seguridad

                        // Escribir mensaje
                        // var inputText = driver.FindElement(By.XPath("//div[@contenteditable='true' and @data-tab='10']"));

                        var inputText = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                        inputText.Click();
                        inputText.SendKeys(mensajeCliente);
                        await Task.Delay(500);

                        // Adjuntar archivo
                        var adjuntar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Adjuntar']")));
                        adjuntar.Click();

                        var inputFile = wait.Until(d => d.FindElement(By.CssSelector("input[type='file']")));
                        inputFile.SendKeys(archivoPath);
                        await Task.Delay(1000);

                        // Enviar
                        var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Enviar']")));
                        enviar.Click();

                        // Esperar confirmación del mensaje
                        try
                        {
                            new WebDriverWait(driver, TimeSpan.FromSeconds(90))
                                .Until(d => d.FindElements(By.CssSelector("span[data-icon='msg-check'], span[data-icon='msg-dblcheck']")).Count > 0);
                            progreso.Report($"✅ Confirmado envío a {telefono}: {archivo}");
                        }
                        catch (WebDriverTimeoutException)
                        {
                            progreso.Report($"⚠️ El envío a {telefono} no se confirmó (pendiente).");
                        }

                        await Task.Delay(5000); // espera prudente entre clientes
                    }
                    catch (WebDriverTimeoutException tex)
                    {
                        progreso.Report($"⚠️ Timeout al enviar a {telefono}: {tex.Message}");
                    }
                    catch (Exception ex)
                    {
                        progreso.Report($"❗ Error enviando a {telefono}: {ex.Message}");
                    }

                    progressBar?.Report(percent);
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
