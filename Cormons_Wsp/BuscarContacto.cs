using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;


namespace GoriziaUtilidades
{
    public class BuscarContacto
    {
        
        public static bool ContactoValidos()
        {
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(120));

            var nuevoChat = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@aria-label='Nuevo chat']")));
            nuevoChat.Click();
            var inputBusqueda = wait.Until(ExpectedConditions.ElementIsVisible(By.XPath("//div[@contenteditable='true' and @role='textbox']")));
            inputBusqueda.Clear();
            inputBusqueda.SendKeys(cliente.Telefono);

            var resultados = driver.FindElements(By.XPath("//span[contains(text(), 'No se encontraron resultados')]"));
            return true;
        }
    }
}
