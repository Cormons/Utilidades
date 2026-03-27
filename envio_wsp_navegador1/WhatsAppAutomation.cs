using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using WinForms = System.Windows.Forms;

namespace GoriziaEnviadorUnitario
{
    public class WhatsAppAutomation
    {
        public void Run(string csvFile, IProgress<string> progreso, IProgress<int> progressBar, CancellationToken ct, string navegador = "c", int tiempoConfirmacion = 0)
        {
            if (!File.Exists(csvFile))
                throw new FileNotFoundException("No se encontró el archivo CSV", csvFile);

            string folder = Path.GetDirectoryName(csvFile);
            var clientes = CsvParser.ParseFile(csvFile);

            if (clientes.Count == 0)
            {
                progreso.Report("No hay filas para procesar. Se cancela la ejecución.");
                return;
            }

            // Ahora delegamos la lógica al nuevo método RunInternal
            RunInternal(clientes, folder, navegador, tiempoConfirmacion, progreso, progressBar, ct);
        }

        private void RunInternal(List<ContactoInfo> clientes, string folder, string navegador, int tiempoConfirmacion,
                         IProgress<string> progreso, IProgress<int> progressBar, CancellationToken ct)
        {
            using (var driver = InicializarDriver(progreso, navegador))
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

                progreso.Report("Esperando carga de interfaz de chat...");
                // Intenta esto para ver si al menos carga la página base
                wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
                progreso.Report("Abriendo WhatsApp Web. Si es la primera vez escanea el QR.");
                EsperarWhatsAppListo(driver, progreso, navegador);

                int total = clientes.Count;
                int processed = 0;

                foreach (var cliente in clientes)
                {
                    if (ct.IsCancellationRequested)
                    {
                        progreso.Report("Cancelado por el usuario.");
                        break;
                    }

                    try
                    {
                        if (string.IsNullOrEmpty(cliente.Estado) || !cliente.Estado.Contains("ERROR"))
                        {
                            EnviarMensaje(driver, wait, cliente, folder, progreso, navegador, tiempoConfirmacion);

                            if (string.IsNullOrEmpty(cliente.Estado) || !cliente.Estado.Contains("ERROR"))
                            {
                                cliente.Estado = "OK";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        cliente.Estado = $"ERROR: {ex.Message}";
                        Console.WriteLine(ex.Message);
                        progreso.Report($"{cliente.Telefono}: {ex.Message}");
                    }

                    processed++;
                    int percent = (int)((processed / (double)total) * 100);
                    progressBar?.Report(percent);
                }

                driver.Quit();

                // Generar CSV solo en el caso masivo
                string resultadoCsv = Path.Combine(folder, Path.GetFileNameWithoutExtension("clientes") + "_resultado.csv");
                using (var writer = new StreamWriter(resultadoCsv, false, Encoding.GetEncoding(1252)))
                {
                    foreach (var c in clientes)
                    {
                        writer.WriteLine(c.Estado);
                    }
                }
                progreso.Report($"CSV de resultados generado en: {resultadoCsv}");
            }
        }

        public void RunSingle(ContactoInfo cliente, string rutaReferencia, string navegador, int tiempoConfirmacion)
        {
            var clientes = new System.Collections.Generic.List<ContactoInfo> { cliente };

            // Si rutaReferencia es carpeta, la usamos. Si es archivo, sacamos su carpeta.
            string folder = Path.HasExtension(rutaReferencia)
                            ? Path.GetDirectoryName(rutaReferencia)
                            : rutaReferencia;

            RunInternal(clientes, folder, navegador, tiempoConfirmacion,
                        new Progress<string>(msg => Console.WriteLine(msg)),
                        null,
                        CancellationToken.None);

            // Guardar resultado
            try
            {
                string rutaLog = Path.Combine(folder, "resultado_envio.csv");
                File.WriteAllText(rutaLog, cliente.Telefono + "," + cliente.Estado + "," + DateTime.Now);
            }
            catch (Exception ex) { Console.WriteLine("Error Log: " + ex.Message); }
        }



        private IWebDriver InicializarDriver(IProgress<string> progreso, string navegador)
        {
            Console.WriteLine($"[DEBUG] Entrando a InicializarDriver con: {navegador}");
            IWebDriver driver = null;

            switch (navegador.ToLower())
            {
                case "c":
                    new DriverManager().SetUpDriver(new ChromeConfig(), "MatchingBrowser");
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

                    progreso.Report("Iniciando navegador Chrome...");
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

                    progreso.Report("  Iniciando navegador Firefox...");
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

                    progreso.Report("  Iniciando navegador Edge...");
                    driver = new EdgeDriver(edgeService, edgeOptions);
                    break;

                default:
                    throw new ArgumentException($"Navegador no soportado: {navegador}");
            }

            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl("https://web.whatsapp.com");

            return driver;
        }

        private void EsperarWhatsAppListo(IWebDriver driver, IProgress<string> progreso, string navegador)
        {
            var waitShort = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
            var waitChat = new WebDriverWait(driver, TimeSpan.FromSeconds(180));

            Func<IWebDriver, IWebElement> buscarInput = d =>
                d.FindElements(By.CssSelector("div[role='textbox'][contenteditable='true']"))
                 .FirstOrDefault(e => e.Displayed);

            Func<IWebDriver, bool> hayQr = d =>
                d.FindElements(By.CssSelector("canvas, [data-testid='qrcode'], [aria-label*='QR'], [aria-label*='Scan'], [aria-label*='Escanea']"))
                 .Any(e => e.Displayed);

            // Intento rápido: ya está logueado
            try
            {
                var input = waitShort.Until(buscarInput);
                if (input != null) return;
            }
            catch (WebDriverTimeoutException) { }

            // Si aparece QR, pedir escaneo y esperar chat
            if (hayQr(driver))
            {
                progreso.Report("QR detectado. Esperando que se inicie sesión...");
                waitChat.Until(buscarInput);
                return;
            }

            // Fallback: esperar chat de todos modos
            waitChat.Until(buscarInput);
        }

        private void EnviarMensaje(
            IWebDriver driver,
            WebDriverWait wait,
            ContactoInfo cliente,
            string folder,
            IProgress<string> progreso,
            string navegador,
            int tiempoConfirmacion)
        {
            // Validar que al menos uno de los dos exista
            bool tieneArchivo = !string.IsNullOrWhiteSpace(cliente.Archivo);
            bool tieneMensaje = !string.IsNullOrWhiteSpace(cliente.Mensaje);

            if (!tieneArchivo && !tieneMensaje)
            {
                cliente.Estado = "ERROR: Debe especificar al menos un mensaje o un archivo";
                throw new Exception("Debe especificar al menos un mensaje o un archivo");
            }

            string archivoPath = null;
            if (tieneArchivo)
            {
                archivoPath = Path.Combine(folder, cliente.Archivo);
                if (!File.Exists(archivoPath))
                {
                    cliente.Estado = $"ERROR: Archivo no encontrado - {cliente.Archivo}";
                    throw new Exception($"Archivo no encontrado: {archivoPath}");
                }
            }

            if (navegador != "f")
            {
                try
                {
                    progreso.Report("Paso 1: Buscar contacto");

                    var searchBox = wait.Until(ExpectedConditions.ElementIsVisible(
                        By.XPath("//div[@role='textbox' and @aria-label='Cuadro de texto para ingresar la búsqueda']")));

                    var actions = new Actions(driver);
                    actions.MoveToElement(searchBox).Click().Perform();
                    Thread.Sleep(500);

                    //  Navegar con teclado a Nuevo Chat
                    actions.KeyDown(Keys.Shift).SendKeys(Keys.Tab).KeyUp(Keys.Shift).Perform();
                    Thread.Sleep(300);

                    actions.KeyDown(Keys.Shift).SendKeys(Keys.Tab).KeyUp(Keys.Shift).Perform();
                    Thread.Sleep(300);

                    actions.SendKeys(Keys.Enter).Perform();
                    Thread.Sleep(1000);

                    // Esperar input de búsqueda del diálogo
                    var inputBusqueda = wait.Until(ExpectedConditions.ElementIsVisible(
                        By.XPath("//div[@contenteditable='true' and @role='textbox']")));

                    inputBusqueda.Click();
                    inputBusqueda.SendKeys(Keys.Control + "a");
                    inputBusqueda.SendKeys(Keys.Backspace);
                    Thread.Sleep(300);

                    foreach (char c in cliente.Telefono)
                    {
                        inputBusqueda.SendKeys(c.ToString());
                        Thread.Sleep(50);
                    }

                    Thread.Sleep(1500);

                    var resultados = driver.FindElements(By.XPath("//span[contains(text(), 'No se encontraron resultados')]"));
                    if (resultados.Count > 0)
                    {
                        cliente.Estado = $"Número inválido: {cliente.Telefono}";
                        progreso.Report(cliente.Estado);
                        throw new Exception($"Número inválido: {cliente.Telefono}");
                    }

                    inputBusqueda.SendKeys(Keys.Enter);

                    progreso.Report("Paso 2: Esperando apertura de chat");
                    wait.Until(ExpectedConditions.ElementIsVisible(
                        By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                    // Escribir mensaje solo si existe
                    if (tieneMensaje)
                    {
                        progreso.Report("Paso 3: Escribiendo mensaje");
                        var inputText = wait.Until(ExpectedConditions.ElementIsVisible(
                            By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));
                        inputText.Click();
                        inputText.SendKeys(cliente.Mensaje);
                    }

                    // Adjuntar archivo solo si existe
                    if (tieneArchivo)
                    {
                        progreso.Report("Paso 4: Adjuntar archivo");
                        actions.KeyDown(OpenQA.Selenium.Keys.Shift)
                               .SendKeys(OpenQA.Selenium.Keys.Tab)
                               .SendKeys(OpenQA.Selenium.Keys.Tab)
                               .KeyUp(OpenQA.Selenium.Keys.Shift)
                               .Perform();
                        Thread.Sleep(500);

                        actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
                        Thread.Sleep(500);

                        // Ir a "Documentos"
                        actions.SendKeys(OpenQA.Selenium.Keys.ArrowDown).Perform();
                        Thread.Sleep(300);
                        actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();

                        // Esperar a que se abra el explorador
                        Thread.Sleep(2000);

                        //   CAMBIO: Escapar caracteres especiales para SendKeys
                        string rutaEscapada = archivoPath
                            .Replace("{", "{{}")
                            .Replace("}", "{}}")
                            .Replace("(", "{(}")
                            .Replace(")", "{)}")
                            .Replace("+", "{+}")
                            .Replace("^", "{^}")
                            .Replace("%", "{%}")
                            .Replace("~", "{~}");

                        WinForms.SendKeys.SendWait(rutaEscapada);
                        Thread.Sleep(500);

                        // Presionar Enter
                        WinForms.SendKeys.SendWait("{ENTER}");
                        Thread.Sleep(2000);

                        progreso.Report("Paso 5: Enviando archivo");

                        var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(
                            By.XPath("//div[@aria-label='Enviar']")));
                        enviar.Click();
                    }
                    else
                    {
                        // Si solo hay mensaje, enviar con Enter
                        progreso.Report("Paso 5: Enviando mensaje");
                        var inputText = driver.FindElement(
                            By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']"));
                        inputText.SendKeys(Keys.Enter);
                    }

                    progreso.Report("Paso 6: Confirmando envío");

                    if (tiempoConfirmacion == 0)
                    {
                        try
                        {
                            new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                                .Until(d => d.FindElements(By.CssSelector(
                                    "span[data-icon='msg-check']")).Count > 0);

                            string tipo = tieneArchivo && tieneMensaje ? "mensaje y archivo" :
                                          tieneArchivo ? cliente.Archivo : "mensaje";
                            progreso.Report($"  Confirmado envío a {cliente.Telefono}: {tipo}");
                        }
                        catch (WebDriverTimeoutException)
                        {
                            cliente.Estado = "Envío pendiente";
                            progreso.Report($"El envío a {cliente.Telefono} no se confirmó (pendiente).");
                            throw new Exception("Timeout esperando confirmación de envío");
                        }
                    }
                    else
                    {
                        //  Modo nuevo: Esperar X segundos sin validar
                        progreso.Report($"  Esperando {tiempoConfirmacion} segundos...");
                        Thread.Sleep(tiempoConfirmacion * 1000);
                        progreso.Report($"  Tiempo de espera cumplido para {cliente.Telefono}");
                    }
                    Thread.Sleep(2000);
                }
                catch (Exception ex)
                {
                    cliente.Estado = $"ERROR: {ex.Message}";
                    progreso.Report($"Error enviando a {cliente.Telefono}: {ex.Message}");
                    throw;
                }
            }
            else // Firefox
            {
                var actions = new Actions(driver);

                try
                {
                    progreso.Report("Paso 1: Buscar contacto");
                    IWebElement inputBusqueda = null;

                    // Intento 1: atajo Ctrl+K para abrir búsqueda (más confiable en Firefox)
                    try
                    {
                        actions.KeyDown(Keys.Control).SendKeys("k").KeyUp(Keys.Control).Perform();
                        progreso.Report("Atajo enviado, localizando cuadro de búsqueda...");
                        inputBusqueda = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                            .Until(d =>
                            {
                                var candidatos = d.FindElements(By.CssSelector(
                                    "div[role='dialog'] div[role='textbox'][contenteditable='true'], " +
                                    "div[role='textbox'][contenteditable='true']"));
                                return candidatos.FirstOrDefault(e => e.Displayed);
                            });
                    }
                    catch (WebDriverTimeoutException)
                    {
                        inputBusqueda = null;
                    }

                    if (inputBusqueda == null)
                    {
                        // Fallback: abrir chat por URL directa
                        progreso.Report("No se detectó el cuadro de búsqueda. Probando por URL directa...");
                        driver.Navigate().GoToUrl($"https://web.whatsapp.com/send?phone={cliente.Telefono}");
                    }
                    else
                    {
                        // Asegurar foco en el input de búsqueda
                        actions.MoveToElement(inputBusqueda).Click().Perform();
                        Thread.Sleep(300);

                        inputBusqueda.SendKeys(Keys.Control + "a");
                        inputBusqueda.SendKeys(Keys.Backspace);
                        Thread.Sleep(300);

                        // Escribir número carácter por carácter
                        foreach (char c in cliente.Telefono)
                        {
                            inputBusqueda.SendKeys(c.ToString());
                            Thread.Sleep(50);
                        }
                        Thread.Sleep(2500);

                        // Verificar con timeout si aparece "No se encontraron"
                        try
                        {
                            var resultados = new WebDriverWait(driver, TimeSpan.FromSeconds(3))
                                .Until(d => d.FindElements(By.XPath(
                                    "//span[contains(text(), 'No se encontraron resultados')]")));

                            if (resultados.Count > 0)
                            {
                                cliente.Estado = $"Número inválido: {cliente.Telefono}";
                                progreso.Report(cliente.Estado);
                                throw new Exception($"Número inválido: {cliente.Telefono}");
                            }
                        }
                        catch (WebDriverTimeoutException)
                        {
                            // Si no apareció el mensaje de error, continuar
                        }

                        // Presionar Enter para abrir el chat
                        inputBusqueda.SendKeys(Keys.Enter);
                        Thread.Sleep(1000);
                    }

                    // Esperar apertura de chat (verificar que sí se abrió)
                    progreso.Report("Paso 2: Esperando apertura de chat");
                    try
                    {
                        wait.Until(ExpectedConditions.ElementIsVisible(
                            By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));
                    }
                    catch (WebDriverTimeoutException)
                    {
                        // Si no se abrió el chat, validar si es número inválido
                        var invalid = driver.FindElements(By.XPath(
                            "//*[contains(text(), 'número de teléfono') or contains(text(), 'Phone number')]"));
                        if (invalid.Count > 0)
                        {
                            cliente.Estado = $"Número inválido: {cliente.Telefono}";
                            progreso.Report(cliente.Estado);
                            throw new Exception($"Número inválido: {cliente.Telefono}");
                        }

                        // Si llegamos acá, solo reportar el timeout
                        throw;
                    }

                    Thread.Sleep(2000);

                    // Escribir mensaje solo si existe
                    if (tieneMensaje)
                    {
                        progreso.Report("Paso 3: Escribiendo mensaje");
                        var inputText = wait.Until(ExpectedConditions.ElementIsVisible(
                            By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']")));

                        actions.MoveToElement(inputText).Click()
                               .SendKeys(cliente.Mensaje)
                               .Perform();
                    }

                    // Adjuntar archivo solo si existe
                    if (tieneArchivo)
                    {
                        progreso.Report("Paso 4: Adjuntar archivo");

                        actions.KeyDown(OpenQA.Selenium.Keys.Shift)
                               .SendKeys(OpenQA.Selenium.Keys.Tab)
                               .SendKeys(OpenQA.Selenium.Keys.Tab)
                               .KeyUp(OpenQA.Selenium.Keys.Shift)
                               .Perform();
                        Thread.Sleep(200);

                        actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
                        Thread.Sleep(500);

                        // Ir a "Documentos"
                        actions.SendKeys(OpenQA.Selenium.Keys.ArrowDown).Perform();
                        Thread.Sleep(300);
                        actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();

                        // Esperar a que se abra el explorador
                        Thread.Sleep(2000);

                        //   CAMBIO: Escapar caracteres especiales para SendKeys
                        string rutaEscapada = archivoPath
                            .Replace("{", "{{}")
                            .Replace("}", "{}}")
                            .Replace("(", "{(}")
                            .Replace(")", "{)}")
                            .Replace("+", "{+}")
                            .Replace("^", "{^}")
                            .Replace("%", "{%}")
                            .Replace("~", "{~}");

                        WinForms.SendKeys.SendWait(rutaEscapada);
                        Thread.Sleep(500);

                        // Presionar Enter
                        WinForms.SendKeys.SendWait("{ENTER}");
                        Thread.Sleep(2000);

                        progreso.Report("Paso 5: Enviando archivo");

                        wait.Until(d =>
                        {
                            var preview = d.FindElements(By.CssSelector("img[src^='blob:']"));
                            return preview.Count > 0 && preview.All(p => p.Displayed);
                        });

                        // Enviar
                        progreso.Report("Paso 5: Enviando archivo");
                        var enviar = wait.Until(ExpectedConditions.ElementToBeClickable(
                            By.XPath("//div[@aria-label='Enviar']")));
                        enviar.Click();
                    }
                    else
                    {
                        // Si solo hay mensaje, enviar con Enter
                        progreso.Report("Paso 5: Enviando mensaje");
                        var inputText = driver.FindElement(
                            By.XPath("//div[@role='textbox' and @aria-placeholder='Escribe un mensaje']"));
                        inputText.SendKeys(Keys.Enter);
                    }

                    // Confirmación de envío
                    progreso.Report("Paso 6: Confirmando envío");

                    if (tiempoConfirmacion == 0)
                    {
                        //   Modo automático: Esperar hasta ver el tilde
                        try
                        {
                            new WebDriverWait(driver, TimeSpan.FromSeconds(120))
                                .Until(d => d.FindElements(By.CssSelector(
                                    "span[data-icon='msg-check']")).Count > 0);

                            string tipo = tieneArchivo && tieneMensaje ? "mensaje y archivo" :
                                          tieneArchivo ? cliente.Archivo : "mensaje";
                            progreso.Report($"  Confirmado envío a {cliente.Telefono}: {tipo}");
                        }
                        catch (WebDriverTimeoutException)
                        {
                            cliente.Estado = "Envío pendiente";
                            progreso.Report($"El envío a {cliente.Telefono} no se confirmó (pendiente).");
                            throw new Exception("Timeout esperando confirmación de envío");
                        }
                    }
                    else
                    {
                        //   Modo manual: Esperar X segundos sin validar
                        progreso.Report($" Esperando {tiempoConfirmacion} segundos...");
                        Thread.Sleep(tiempoConfirmacion * 1000);
                        progreso.Report($"  Tiempo de espera cumplido para {cliente.Telefono}");
                    }

                    Thread.Sleep(3000);

                    // Cerrar cualquier diálogo
                    actions.SendKeys(Keys.Escape).Perform();
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    cliente.Estado = $"ERROR: {ex.Message}";
                    progreso.Report($"Error enviando a {cliente.Telefono}: {ex.Message}");

                    // AGREGADO: Cerrar diálogos después de error
                    try
                    {
                        actions.SendKeys(Keys.Escape).Perform();
                        Thread.Sleep(500);
                        actions.SendKeys(Keys.Escape).Perform();
                        Thread.Sleep(500);
                    }
                    catch { }

                    throw;
                }
            }
        }
    }
}
