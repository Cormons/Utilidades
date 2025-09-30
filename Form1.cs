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
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            txtLog.Clear();
            progressBar1.Value = 0; // reinicio al empezar

            cts = new CancellationTokenSource();
            var automation = new WhatsAppAutomation();

            // Progress para el log
            var progressText = new Progress<string>(s => Log(s));

            // Progress para la barra
            var progressBar = new Progress<int>(value =>
            {
                progressBar1.Value = Math.Min(progressBar1.Maximum, value);
            });

            try
            {
                string basePath = txtBasePath.Text.Trim();
                string mensaje = string.IsNullOrWhiteSpace(txtMensaje.Text)
                                 ? "Hola, te envío tu comprobante adjunto. Saludos!"
                                 : txtMensaje.Text;

                await automation.RunAsync(basePath, progressText, progressBar, cts.Token);
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
            openFileDialog1.Title = "Seleccione el archivo de clientes";
            openFileDialog1.Filter = "CSV|*.csv|Todos los archivos|*.*";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                txtBasePath.Text = openFileDialog1.FileName; // ruta completa del CSV
            }
        }
    }
}
