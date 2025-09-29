using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace GoriziaUtilidades
{
    public class WhatsAppAutomation
    {
        public async Task RunAsync(string csvFile, string mensajeDefault, IProgress<string> progreso, IProgress<int> progressBar, CancellationToken ct)
        {
            if (!File.Exists(csvFile))
                throw new FileNotFoundException("No se encontró el archivo CSV", csvFile);

            string folder = Path.GetDirectoryName(csvFile);
            var clientes = CsvParser.ParseFile(csvFile, mensajeDefault);

            if (clientes.Count == 0)
            {
                progreso.Report("⚠️ No hay filas para procesar. Se cancela la ejecución.");
                return;
            }

            using (var driver = InicializarDriver(progreso))
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(120));

                progreso.Report("🔑 Abriendo WhatsApp Web. Si es la primera vez escanea el QR.");
                // Esperá hasta 120 segundos a que aparezca y sea visible el cuadro de texto de WhatsApp Web. Si eso pasa antes, seguí; si no, tirá error.
                //wait.Until(d => d.FindElements(By.XPath("//div[@aria-label='Nuevo chat']")).Count > 0);
                wait.Until(ExpectedConditions.ElementIsVisible(
                    By.CssSelector("div[role='textbox'][contenteditable='true']")));

                int total = clientes.Count;
                int processed = 0;

                foreach (var cliente in clientes)
                {
                    if (ct.IsCancellationRequested)
                    {
                        progreso.Report("⏹️ Cancelado por el usuario.");
                        break;
                    }
                    
                    await EnviarMensajeAsync(driver, wait, cliente, folder, progreso);

                    processed++;
                    int percent = (int)((processed / (double)total) * 100);
                    progressBar?.Report(percent);
                }

                driver.Quit();
            }
        }

        private IWebDriver InicializarDriver(IProgress<string> progreso)     
        {
            new DriverManager().SetUpDriver(new ChromeConfig()); 

            var options = new ChromeOptions(); // Configuraciones de Chrome 
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromeProfile = Path.Combine(userProfile, @"Google\\Chrome\\User Data\\WhatsAppSession");

            options.AddArgument("--user-data-dir=" + chromeProfile);
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--log-level=3");
            options.AddArgument("--silent");

            progreso.Report("🚀 Iniciando navegador Chrome...");

            var service = ChromeDriverService.CreateDefaultService(); // Crea el servicio de ChromeDriver 
            service.HideCommandPromptWindow = true;              // Oculta la consola negra
            service.SuppressInitialDiagnosticInformation = true; // Suprime logs iniciales
            service.LogPath = "chromedriver.log";                // Redirige logs si hiciera falta

            var driver = new ChromeDriver(service, options);
            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl("https://web.whatsapp.com");

            return driver;
        }

        private async Task EnviarMensajeAsync(
            IWebDriver driver,
            WebDriverWait wait,
            ContactoInfo cliente,
            string folder,
            IProgress<string> progreso)
        {
            string archivoPath = Path.Combine(folder, cliente.Archivo);
            if (!File.Exists(archivoPath))
            {
                progreso.Report($"❌ Archivo no encontrado: {archivoPath}");
                return;
            }

            try
            {
                // 🔎 Buscar contacto
                //MessageBox.Show("Paso 1: Buscar contacto", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progreso.Report("Paso 1: Buscar contacto");

                var searchBox = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-label='Cuadro de texto para ingresar la búsqueda']")));
                searchBox.Click();
                searchBox.Clear();
                searchBox.SendKeys(cliente.Telefono + OpenQA.Selenium.Keys.Enter);

                // 🟢 Esperar apertura de chat
                //MessageBox.Show("Paso 2: Esperando apertura de chat", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progreso.Report("Paso 2: Esperando apertura de chat");

                wait.Until(ExpectedConditions.ElementIsVisible(
                    By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                await Task.Delay(2000);

                // 💬 Escribir mensaje
                //MessageBox.Show("Paso 3: Escribiendo mensaje", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progreso.Report("Paso 3: Escribiendo mensaje");

                var inputText = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));
                inputText.Click();
                inputText.SendKeys(cliente.Mensaje);
                await Task.Delay(500);

                // 📎 Adjuntar archivo
                //MessageBox.Show("Paso 4: Adjuntar archivo", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progreso.Report("Paso 4: Adjuntar archivo");

                // 📎 Adjuntar archivo usando Shift+Tab + Enter
                //MessageBox.Show("Paso 4: Adjuntar archivo (Shift+Tab + Enter)", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                var actions = new Actions(driver);

                // Shift+Tab dos veces
                actions.KeyDown(OpenQA.Selenium.Keys.Shift).SendKeys(OpenQA.Selenium.Keys.Tab).SendKeys(OpenQA.Selenium.Keys.Tab).KeyUp(OpenQA.Selenium.Keys.Shift).Perform();
                await Task.Delay(200);

                // Enter para activar el botón
                actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
                await Task.Delay(500);

                // Ahora enviamos el archivo al input (puede estar oculto)
                var inputFile = wait.Until(d => d.FindElement(By.CssSelector("input[type='file']")));
                inputFile.SendKeys(archivoPath);
                await Task.Delay(1000);

                // 📤 Enviar
                //MessageBox.Show("Paso 5: Enviando archivo", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progreso.Report("Paso 5: Enviando archivo");

                var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Enviar']")));
                enviar.Click();

                // ✅ Confirmación de envío
                //MessageBox.Show("Paso 6: Confirmando envío", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                progreso.Report("Paso 6: Confirmando envío");

                try
                {
                    new WebDriverWait(driver, TimeSpan.FromSeconds(90))
                        .Until(d => d.FindElements(By.CssSelector("span[data-icon='msg-check'], span[data-icon='msg-dblcheck']")).Count > 0);
                    progreso.Report($"✅ Confirmado envío a {cliente.Telefono}: {cliente.Archivo}");
                }
                catch (WebDriverTimeoutException)
                {
                    progreso.Report($"⚠️ El envío a {cliente.Telefono} no se confirmó (pendiente).");
                }

                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                progreso.Report($"❗ Error enviando a {cliente.Telefono}: {ex.Message}");
            }
        }
    }
}
