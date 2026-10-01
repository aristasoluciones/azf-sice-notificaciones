using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sice.Functions.Notificaciones.Models
{
    public class EmailQueueMessage
    {
        public string Destinatario { get; set; } = string.Empty;
        public string Asunto { get; set; } = string.Empty;
        public string CuerpoHtml { get; set; } = string.Empty;
        public string? NombreBlobAdjunto { get; set; } // Nombre del archivo en el Storage si existe
        // Renglón de una difusión: se registra el resultado del envío en la base.
        public int? IdDifusionDetalle { get; set; }
    }
}
