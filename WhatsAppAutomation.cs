using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.IE;
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
        public async Task RunAsync(string csvFile, IProgress<string> progreso, IProgress<int> progressBar, CancellationToken ct, string navegador = "c")
        {
            if (!File.Exists(csvFile))
                throw new FileNotFoundException("No se encontró el archivo CSV", csvFile);

            string folder = Path.GetDirectoryName(csvFile);
            var clientes = CsvParser.ParseFile(csvFile);

            if (clientes.Count == 0)
            {
                progreso.Report("⚠️ No hay filas para procesar. Se cancela la ejecución.");
                return;
            }

            using (var driver = InicializarDriver(progreso, navegador))
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
                    
                    await EnviarMensajeAsync(driver, wait, cliente, folder, progreso, navegador);

                    processed++;
                    int percent = (int)((processed / (double)total) * 100);
                    progressBar?.Report(percent);
                }

                driver.Quit();
            }
        }

        private IWebDriver InicializarDriver(IProgress<string> progreso, string navegador)
        {
            Console.WriteLine($"[DEBUG] Entrando a InicializarDriver con: {navegador}");
            IWebDriver driver = null;

            switch (navegador.ToLower())
            {
                case "c":
                    new DriverManager().SetUpDriver(new ChromeConfig());
                    var chromeOptions = new ChromeOptions();
                    string chromeProfile = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Google\Chrome\User Data\WhatsAppSession");

                    chromeOptions.AddArgument("--user-data-dir=" + chromeProfile);
                    chromeOptions.AddArgument("--disable-dev-shm-usage");
                    chromeOptions.AddArgument("--no-sandbox");
                    chromeOptions.AddArgument("--disable-gpu");
                    chromeOptions.AddArgument("--log-level=3");
                    chromeOptions.AddArgument("--silent");

                    var chromeService = ChromeDriverService.CreateDefaultService();
                    chromeService.HideCommandPromptWindow = true;
                    chromeService.SuppressInitialDiagnosticInformation = true;

                    progreso.Report("🚀 Iniciando navegador Chrome...");
                    driver = new ChromeDriver(chromeService, chromeOptions);
                    break;

                case "f":
                    new DriverManager().SetUpDriver(new FirefoxConfig());
                    var ffOptions = new FirefoxOptions();   

                    // Perfil de usuario persistente para guardar la sesión
                    string ffProfilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Mozilla\Firefox\WhatsAppSession");

                    if (!Directory.Exists(ffProfilePath))
                    {
                        Directory.CreateDirectory(ffProfilePath);
                    }

                    ffOptions.Profile = new FirefoxProfile(ffProfilePath);

                    var ffService = FirefoxDriverService.CreateDefaultService();
                    ffService.HideCommandPromptWindow = true;
                    ffService.SuppressInitialDiagnosticInformation = true;

                    progreso.Report("🚀 Iniciando navegador Firefox...");
                    driver = new FirefoxDriver(ffService, ffOptions);
                    break;

                case "e":
                    new DriverManager().SetUpDriver(new EdgeConfig());
                    var edgeOptions = new EdgeOptions();
                    string edgeProfile = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Microsoft\Edge\User Data\WhatsAppSession");
                    edgeOptions.AddArgument("--user-data-dir=" + edgeProfile);
                    edgeOptions.AddArgument("--disable-dev-shm-usage");

                    var edgeService = EdgeDriverService.CreateDefaultService();
                    edgeService.HideCommandPromptWindow = true;
                    edgeService.SuppressInitialDiagnosticInformation = true;

                    progreso.Report("🚀 Iniciando navegador Edge...");
                    driver = new EdgeDriver(edgeService, edgeOptions);
                    break;

                default:
                    throw new ArgumentException($"Navegador no soportado: {navegador}");
            }

            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl("https://web.whatsapp.com");

            return driver;
        }

        private async Task EnviarMensajeAsync(
            IWebDriver driver,
            WebDriverWait wait,
            ContactoInfo cliente,
            string folder,
            IProgress<string> progreso,
            string navegador)
        {
            string archivoPath = Path.Combine(folder, cliente.Archivo);
            if (!File.Exists(archivoPath))
            {
                progreso.Report($"❌ Archivo no encontrado: {archivoPath}");
                return;
            }

            if (navegador != "f")
            {
                try
                {
                    // 🔎 Buscar contacto
                    progreso.Report("Paso 1: Buscar contacto");

                    var searchBox = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-label='Cuadro de texto para ingresar la búsqueda']")));
                    searchBox.Click();
                    searchBox.Clear();
                    searchBox.SendKeys(cliente.Telefono + OpenQA.Selenium.Keys.Enter);

                    // 🟢 Esperar apertura de chat
                    progreso.Report("Paso 2: Esperando apertura de chat");

                    wait.Until(ExpectedConditions.ElementIsVisible(
                        By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                    //await Task.Delay(1000);

                    // 💬 Escribir mensaje
                    progreso.Report("Paso 3: Escribiendo mensaje");

                    var inputText = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));
                    inputText.Click();
                    inputText.SendKeys(cliente.Mensaje);
                    //await Task.Delay(500);

                    // 📎 Adjuntar archivo
                    progreso.Report("Paso 4: Adjuntar archivo");

                    // 📎 Adjuntar archivo usando Shift+Tab + Enter
                    var actions = new Actions(driver);

                    // Shift+Tab dos veces
                    actions.KeyDown(OpenQA.Selenium.Keys.Shift).SendKeys(OpenQA.Selenium.Keys.Tab).SendKeys(OpenQA.Selenium.Keys.Tab).KeyUp(OpenQA.Selenium.Keys.Shift).Perform();
                    await Task.Delay(500);

                    // Enter para activar el botón
                    actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
                    await Task.Delay(500);

                    // Ahora enviamos el archivo al input (puede estar oculto)
                    var inputFile = wait.Until(d => d.FindElement(By.CssSelector("input[type='file']")));
                    inputFile.SendKeys(archivoPath);
                    await Task.Delay(2000);

                    // 📤 Enviar
                    progreso.Report("Paso 5: Enviando archivo");

                    var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Enviar']")));
                    enviar.Click();

                    // ✅ Confirmación de envío
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

                    await Task.Delay(3000);
                }
                catch (Exception ex)
                {
                    progreso.Report($"❗ Error enviando a {cliente.Telefono}: {ex.Message}");
                }

            }
            else
            {
                var actions = new Actions(driver);

                progreso.Report("Paso 1: Buscar contacto");

                var searchBox = wait.Until(ExpectedConditions.ElementIsVisible(
                    By.XPath("//div[@role='textbox' and @aria-label='Cuadro de texto para ingresar la búsqueda']")));

                actions.MoveToElement(searchBox).Click()
                       .SendKeys(cliente.Telefono + OpenQA.Selenium.Keys.Enter)
                       .Perform();

                progreso.Report("Paso 2: Esperando apertura de chat");
                wait.Until(ExpectedConditions.ElementIsVisible(
                    By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));
                await Task.Delay(2000);

                progreso.Report("Paso 3: Escribiendo mensaje");
                var inputText = wait.Until(ExpectedConditions.ElementIsVisible(
                    By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                actions.MoveToElement(inputText).Click()
                       .SendKeys(cliente.Mensaje)
                       .Perform();

                progreso.Report("Paso 4: Adjuntar archivo");

                actions.KeyDown(OpenQA.Selenium.Keys.Shift).SendKeys(OpenQA.Selenium.Keys.Tab).SendKeys(OpenQA.Selenium.Keys.Tab).KeyUp(OpenQA.Selenium.Keys.Shift).Perform();
                await Task.Delay(200);
                actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
                await Task.Delay(500);

                var inputFile = wait.Until(d => d.FindElement(By.CssSelector("input[type='file']")));
                inputFile.SendKeys(archivoPath);
                await Task.Delay(1000);

                progreso.Report("Paso 5: Enviando archivo");
                var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//div[@aria-label='Enviar']")));
                enviar.Click();

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
        }
    }
}
