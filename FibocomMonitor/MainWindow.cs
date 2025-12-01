using FibocomMonitor.AT;
using System.IO.Ports;

namespace FibocomMonitor
{
    public partial class MainWindow : Form
    {
        private AtHost? ATH;
        private bool FindCOM = true;
        private readonly CancellationTokenSource UICts;

        public MainWindow()
        {
            InitializeComponent();
            UICts = new CancellationTokenSource();
            base.FormClosing += MainWindow_FormClosing;
            base.Load += MainWindow_FormLoad;
        }

        private async void MainWindow_FormLoad(object? sender, EventArgs e)
        {
            await RefreshComListAsync();
        }

        private void MainWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            UICts.Cancel();
            if (ATH != null)
            {
                if (ATH.IsOpen != false)
                {
                    ATH.ClosePort();
                }
                ATH.Dispose();
            }
        }

        private async void ButtonCon_Click(object sender, EventArgs e)
        {
            string? ATPort = (string?)COMList.SelectedItem;
            if (ATPort == null)
            {
                MessageBox.Show("No selected COM port!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            FindCOM = false;
            ATPort = ExtractComPort(ATPort);
            if (!string.IsNullOrEmpty(ATPort))
            {
                if (ATH != null && ATH.IsOpen == true)
                {
                    ATH.SetNewPort(ATPort);
                    return;
                }
                ATH = new AtHost(ATPort);
                if(ATH.IsL860)
                {
                    Temp.Visible = true;
                    label5.Visible = true;
                }
                try
                {
                    await UIUpdateAsync();
                }
                catch(OperationCanceledException) { }
                finally
                {
                    ClearDisplay();
                }
            }
        }

        private void PortClose_Click(object sender, EventArgs e)
        {
            if (COMList.SelectedItem is string ATPort)
            {
                if (ATH != null)
                {
                    if (ATH.IsOpen == true)
                    {
                        ATH.ClosePort();
                    }
                    ClearDisplay();
                    return;
                }
                string port = ExtractComPort(ATPort);
                if (AtHost.Check(port, out SerialPort? serial))
                {
                    serial?.Close();
                    serial?.Dispose();
                }
            }
        }

        private async Task RefreshComListAsync()
        {
            while (FindCOM && !UICts.IsCancellationRequested)
            {
                var ports = ComPortHelper.GetComPortList();
                if (ports.Count > 0)
                {

                    if (!this.IsHandleCreated || this.IsDisposed)
                        break;
                    this.Invoke(() =>
                    {
                        COMList.Items.Clear();
                        COMList.Items.AddRange(ports.ToArray());
                    });
                    break;
                }
                try
                {
                    await Task.Delay(500, UICts.Token);
                }
                catch(OperationCanceledException) { break; }
            }
        }

        private async Task UIUpdateAsync()
        {
#pragma warning disable CS8602
            while (ATH.IsOpen && !UICts.IsCancellationRequested)
#pragma warning restore CS8602
            {
                ATH.Start();
                Data? data = (Data)ATH.Result.Clone();
                CarrierList.Items.Clear();
                CarrierList.Items.AddRange(data.BandList.ToArray());
                Operator.Text = data.Operator;
                StatusNetwork.Text = data.StatusNetwork;
                if (data.StatusNetwork == "LTE")
                {
                    FCN.Text = data.EARFCN; LB5.Text = "EARFCN:";
                    Lrsrp.Text = "RSRP:";
                    Lsinr.Text = "SINR:";
                    SINR.Text = data.SINR;
                    RSRP.Text = data.RSRP;
                }
                else
                {
                    Ldistance.Visible = false;
                    FCN.Text = data.UARFCN; LB5.Text = "UARFCN:";
                    Lsinr.Text = "ECNO:";
                    SINR.Text = data.ECNO;
                    Lrsrp.Text = "RSCP:";
                    RSRP.Text = data.RSCP;
                }
                Temp.Text = data.TEMP;
                Band.Text = data.Band;
                RSSI.Text = data.RSSI;
                Signal.Text = data.SignalStrength;
                Distance.Text = data.Distance;
                await Task.Delay(1500, UICts.Token);
            }
        }

        private void ClearDisplay()
        {
            string s = "Unknown";
            CarrierList.Items.Clear();
            Operator.Text = s;
            FCN.Text = s;
            StatusNetwork.Text = s;
            RSRP.Text = s;
            Band.Text = s;
            SINR.Text = s;
            RSSI.Text = s;
            Signal.Text = s;
            Distance.Text = s;
        }

        private static string ExtractComPort(string text)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, @"\(COM\d+\)");
            if(m.Success)
            {
                return m.Value;
            }
            else
            {
                return string.Empty; 
            }
        }

    }
}
