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

                progreso.Report("Esperando carga de interfaz...");
                wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));

                progreso.Report("Abriendo WhatsApp Web. Si es necesario, escanea el QR.");

                try
                {
                    // Buscamos el panel lateral de chats (ID 'side' o 'pane-side'). 
                    // Esto confirma que el login fue exitoso y la interfaz cargó.
                    wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("#side, #pane-side, [data-testid='side-panel']")));
                    progreso.Report("Sesión detectada correctamente.");

                    // Un breve respiro para que terminen de cargar los elementos internos
                    Thread.Sleep(2500);
                }
                catch (WebDriverTimeoutException)
                {
                    progreso.Report("ERROR: Tiempo de espera agotado. No se detectó la sesión iniciada.");
                    driver.Quit();
                    return;
                }

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
                            // Llamada a tu método que ya funciona en producción
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

                // Generar CSV de resultados
                string resultadoCsv = Path.Combine(folder, "envio_wa_resultado.csv");
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
                string rutaLog = Path.Combine(folder, "envio_wa_resultado.csv");
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
                    progreso.Report("Paso 1: Iniciando búsqueda (vía atajo)");

                    // 1. Forzar foco al cuerpo de la página para asegurar que el atajo sea escuchado
                    var body = driver.FindElement(By.TagName("body"));
                    body.SendKeys(Keys.Escape); // Limpiar cualquier modal previo
                    Thread.Sleep(500);
                    body.Click();
                    Thread.Sleep(500);

                    // 2. Ejecutar Ctrl + Alt + N (Atajo oficial de WhatsApp para Nuevo Chat)
                    var actions = new Actions(driver);
                    actions.KeyDown(Keys.Control)
                           .KeyDown(Keys.Alt)
                           .SendKeys("n")
                           .KeyUp(Keys.Alt)
                           .KeyUp(Keys.Control)
                           .Perform();

                    progreso.Report("Atajo funcionó, localizando cuadro de búsqueda...");

                    // 3. CAMBIO: Buscar el input de forma genérica y agresiva
                    IWebElement inputBusqueda = null;
                    var waitCorto = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

                    try
                    {
                        inputBusqueda = waitCorto.Until(d => {
                            // Buscamos todos los elementos editables
                            var editables = d.FindElements(By.CssSelector("div[contenteditable='true'], input[type='text'], div[role='textbox']"));

                            // Filtramos el que esté visible y habilitado
                            // En el panel de "Nuevo Chat", el buscador suele ser el único o el primero visible.
                            return editables.FirstOrDefault(e => e.Displayed && e.Enabled);
                        });
                    }
                    catch (WebDriverTimeoutException)
                    {
                        throw new Exception("El panel de búsqueda abrió, pero Selenium no detecta el cuadro para escribir el número.");
                    }

                    progreso.Report("Cuadro de búsqueda localizado. Escribiendo número...");

                    // 4. Interacción segura
                    inputBusqueda.Click();
                    Thread.Sleep(300);
                    inputBusqueda.SendKeys(Keys.Control + "a");
                    inputBusqueda.SendKeys(Keys.Backspace);

                    foreach (char c in cliente.Telefono)
                    {
                        inputBusqueda.SendKeys(c.ToString());
                        Thread.Sleep(50);
                    }

                    progreso.Report($"Número {cliente.Telefono} ingresado.");

                    // 1. Esperar un momento a que WhatsApp termine de filtrar la lista
                    Thread.Sleep(2000);

                    try
                    {
                        // Intentamos dar Enter sobre el input si todavía existe
                        inputBusqueda.SendKeys(Keys.Enter);
                    }
                    catch (StaleElementReferenceException)
                    {
                        // Si el elemento "murió" (Stale), significa que la lista cambió. 
                        // Mandamos el Enter directamente a la página.
                        new Actions(driver).SendKeys(Keys.Enter).Perform();
                    }

                    progreso.Report("Chat seleccionado. Esperando panel de escritura...");
                    Thread.Sleep(2000);

                    // 2. Ahora necesitamos encontrar el cuadro donde se escribe el mensaje.
                    // Como el DOM cambió, buscamos de nuevo todos los editables y elegimos el último (el chat)
                    IWebElement inputText = wait.Until(d => {
                        var editables = d.FindElements(By.CssSelector("div[contenteditable='true']"));
                        return editables.LastOrDefault(e => e.Displayed);
                    });

                    if (tieneMensaje)
                    {
                        progreso.Report("Paso 3: Escribiendo mensaje");
                        inputText.Click();
                        // Dividimos el mensaje en varias líneas usando el carácter '\n' como separador.
                        // Esto genera un arreglo (array) donde cada elemento es una línea del mensaje.
                        var lines = (cliente.Mensaje ?? string.Empty).Split('\n');

                        for (int i = 0; i < lines.Length; i++)
                        {
                            // Escribimos el texto de la línea actual en el cuadro de mensaje de WhatsApp Web.
                            // Puede ser una línea vacía si el mensaje tenía dos saltos seguidos.
                            actions.SendKeys(lines[i]).Perform();

                            // Si NO estamos en la última línea, necesitamos agregar un salto de línea.
                            // En WhatsApp Web, presionar "Enter" envía el mensaje directamente.
                            // Para insertar un salto de línea sin enviar, se usa "Shift+Enter".
                            if (i < lines.Length - 1)
                            {
                                // Simulamos la combinación de teclas Shift+Enter:
                                // - KeyDown: presionamos la tecla Shift
                                // - SendKeys(Enter): presionamos Enter mientras Shift está apretado
                                // - KeyUp: soltamos la tecla Shift
                                // Esto produce un salto de línea dentro del mensaje sin enviarlo todavía.
                                actions.KeyDown(OpenQA.Selenium.Keys.Shift)
                                       .SendKeys(OpenQA.Selenium.Keys.Enter)
                                       .KeyUp(OpenQA.Selenium.Keys.Shift)
                                       .Perform();
                            }
                        }

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
            
            else // Firefoxxxxxxxxxxxxxxx
            {
                var actions = new Actions(driver);

                try
                {
                    // 0. Verificación de seguridad del contexto
                    if (driver.WindowHandles.Count == 0) throw new Exception("La ventana de Firefox se cerró inesperadamente.");

                    progreso.Report("Paso 1: Iniciando búsqueda (vía atajo)");

                    // 1. Forzar el foco en el cuerpo de la página antes del atajo
                    try
                    {
                        driver.FindElement(By.TagName("body")).Click();
                    }
                    catch { /* Ignorar si falla el click inicial */ }
                    Thread.Sleep(500);

                    // 2. Enviar atajo Ctrl + Alt + N (Universal para Nuevo Chat)
                    actions.KeyDown(Keys.Control).KeyDown(Keys.Alt).SendKeys("n")
                           .KeyUp(Keys.Alt).KeyUp(Keys.Control).Perform();

                    progreso.Report("Esperando 3 segundos...");
                    Thread.Sleep(3000);
                    progreso.Report("3 segundos cumplidos");

                    // 3. Localizar el input de búsqueda - MISMOS SELECTORES QUE CHROME
                    IWebElement inputBusqueda = null;
                    var waitCorto = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

                    try
                    {
                        inputBusqueda = waitCorto.Until(d => {
                            var editables = d.FindElements(By.CssSelector("div[contenteditable='true'], input[type='text'], div[role='textbox']"));
                            return editables.FirstOrDefault(e => e.Displayed && e.Enabled);
                        });
                    }
                    catch (WebDriverTimeoutException)
                    {
                        throw new Exception("No se detectó el cuadro de búsqueda tras el atajo.");
                    }

                    if (inputBusqueda == null) throw new Exception("No se detectó el cuadro de búsqueda tras el atajo.");

                    progreso.Report("Cuadro de búsqueda localizado. Escribiendo número...");

                    // 4. REFUERZO DE FOCO: Clic físico y limpieza de campo
                    inputBusqueda.Click();
                    Thread.Sleep(300);
                    inputBusqueda.SendKeys(Keys.Control + "a");
                    inputBusqueda.SendKeys(Keys.Backspace);
                    Thread.Sleep(200);

                    // 5. ESCRIBIR NÚMERO DE UNA VEZ (no carácter por carácter)
                    inputBusqueda.SendKeys(cliente.Telefono);

                    progreso.Report($"Número {cliente.Telefono} ingresado.");


                    Thread.Sleep(2000); // Tiempo para que WhatsApp procese el filtro

                    // 6. ENTRAR AL CHAT
                    // Usamos Actions para el Enter porque es inmune a errores de 'Stale Element' si la lista se refresca
                    actions.SendKeys(Keys.Enter).Perform();

                    progreso.Report("Paso 2: Esperando apertura de chat");
                    Thread.Sleep(2000);

                    // 7. Localizar el cuadro de mensaje (el último editable visible)
                    var inputText = wait.Until(d => {
                        var editables = d.FindElements(By.CssSelector("div[contenteditable='true']"));
                        return editables.LastOrDefault(e => e.Displayed);
                    });

                    if (tieneMensaje)
                    {
                        progreso.Report("Paso 3: Escribiendo mensaje");
                        inputText.Click();
                        // Escribimos el mensaje usando Actions para mayor estabilidad en Firefox
                        actions.MoveToElement(inputText).Click().SendKeys(cliente.Mensaje).Perform();

                        if (!tieneArchivo)
                        {
                            inputText.SendKeys(Keys.Enter);
                            progreso.Report("Paso 5: Mensaje enviado.");
                        }
                    }

                    // 8. Lógica de Adjuntos (Si aplica)
                    if (tieneArchivo)
                    {
                        progreso.Report("Paso 4: Adjuntar archivo");

                        // Navegar al clip de adjuntos (Shift + Tab x2 desde el cuadro de mensaje)
                        actions.KeyDown(OpenQA.Selenium.Keys.Shift).SendKeys(OpenQA.Selenium.Keys.Tab).SendKeys(OpenQA.Selenium.Keys.Tab)
                               .KeyUp(OpenQA.Selenium.Keys.Shift).Perform();
                        Thread.Sleep(500);

                        actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();
                        Thread.Sleep(800);

                        // Seleccionar 'Documento' (Flecha abajo y Enter)
                        actions.SendKeys(OpenQA.Selenium.Keys.ArrowDown).Perform();
                        Thread.Sleep(400);
                        actions.SendKeys(OpenQA.Selenium.Keys.Enter).Perform();

                        // Esperar diálogo de Windows
                        Thread.Sleep(2500);

                        string rutaEscapada = archivoPath
                            .Replace("{", "{{}").Replace("}", "{}}").Replace("(", "{(}").Replace(")", "{)}")
                            .Replace("+", "{+}").Replace("^", "{^}").Replace("%", "{%}").Replace("~", "{~}");

                        WinForms.SendKeys.SendWait(rutaEscapada);
                        Thread.Sleep(500);
                        WinForms.SendKeys.SendWait("{ENTER}");

                        progreso.Report("Paso 5: Enviando archivo...");
                        Thread.Sleep(2000);

                        // Esperar y clickear el botón verde de enviar adjunto
                        try
                        {
                            var btnEnviar = wait.Until(ExpectedConditions.ElementToBeClickable(
                                By.XPath("//div[@aria-label='Enviar']")));
                            btnEnviar.Click();
                        }
                        catch
                        {
                            ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile("error_adjunto_" + DateTime.Now.Ticks + ".png");
                            throw new Exception("No se encontró el botón Enviar para el archivo adjunto.");
                        }
                    }

                    // 9. Confirmación de envío
                    progreso.Report("Paso 6: Confirmando envío");

                    if (tiempoConfirmacion == 0)
                    {
                        try
                        {
                            new WebDriverWait(driver, TimeSpan.FromSeconds(25))
                                .Until(d => d.FindElements(By.CssSelector(
                                    "span[data-icon='msg-check'], span[data-icon='msg-dblcheck'], span[data-icon='msg-time']")).Count > 0);
                            progreso.Report($"Confirmado envío a {cliente.Telefono}");
                        }
                        catch
                        {
                            progreso.Report("Envío en cola (sin confirmación visual)");
                        }
                    }
                    else
                    {
                        Thread.Sleep(tiempoConfirmacion * 1000);
                    }

                    // Limpiar pantalla
                    Thread.Sleep(1500);
                    actions.SendKeys(OpenQA.Selenium.Keys.Escape).Perform();

                }
                catch (Exception ex)
                {
                    cliente.Estado = $"ERROR: {ex.Message}";
                    progreso.Report($"Error en Firefox para {cliente.Telefono}: {ex.Message}");

                    // Intentar cerrar diálogos pendientes tras el error
                    try { actions.SendKeys(Keys.Escape).SendKeys(Keys.Escape).Perform(); } catch { }
                    throw;
                }
            }
        
        }
    }
}
