using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoriziaUtilidades
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource cts;
        public Form1()
        {
            InitializeComponent();
            btnStop.Enabled = false;
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            txtLog.Clear();

            cts = new CancellationTokenSource();
            var automation = new WhatsAppAutomation();
            var progress = new Progress<string>(s => Log(s));

            try
            {
                string basePath = txtBasePath.Text.Trim();
                string mensaje = string.IsNullOrWhiteSpace(txtMensaje.Text) ?
                                 "Hola, te envío tu comprobante adjunto. Saludos!" :
                                 txtMensaje.Text;

                await automation.RunAsync(basePath, mensaje, progress, cts.Token);
                Log("✅ Proceso finalizado.");
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
            finally
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            cts?.Cancel();
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Log(message)));
                return;
            }
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            // Configurar el cuadro de diálogo
            openFileDialog1.Title = "Seleccione un archivo de Excel";
            openFileDialog1.Filter = "Archivos de Excel|*.xlsx;*.xls|Todos los archivos|*.*";

            // Mostrar el cuadro de diálogo y verificar el resultado
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                // Mostrar la ruta en tu TextBox (o usarla directamente)
                txtBasePath.Text = openFileDialog1.FileName;
            }
        }
    }
}   
