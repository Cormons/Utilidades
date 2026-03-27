using System;

namespace GoriziaEnviadorUnitario
{
    public class ContactoInfo
    {   
            // public string Nombre { get; set; }
            public string Telefono { get; set; }

            // Importado desde CSV. Actualmente NO usado en el flujo de envío de mensajes.
            // Mantener para uso futuro (ej.: reemplazo de {importe} en el mensaje).
            public string Importe { get; set; }

            public string Mensaje { get; set; }
            public string Archivo { get; set; }
            //public string LinkPago { get; set; }
            public string Estado { get; set; }
            //public string Error { get; set; }
            // public string FechaEnvio { get; set; }
    }
}
