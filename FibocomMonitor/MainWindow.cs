using FibocomMonitor.AT;
using System.IO.Ports;

namespace FibocomMonitor
{
    public partial class MainWindow : Form
    {
        private AtHost ATH;
        private bool FindCOM = true;
        private CancellationTokenSource UICts = new();

        public MainWindow()
        {
            InitializeComponent();
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

        private async Task UIUpdateAsync()
        {
            while (ATH.IsOpen && !UICts.IsCancellationRequested)
            {
                await ATH.SendCommand().WaitAsync(UICts.Token);
                Data data = ATH.Result;
                CarrierList.Items.Clear();
                CarrierList.Items.AddRange([.. data.BandList]);
                Operator.Text = data.Operator;
                StatusNetwork.Text = data.StatusNetwork;
                Temp.Text = data.TEMP;
                RSRP.Text = data.RSRP;
                if (data.StatusNetwork == "LTE") { FCN.Text = data.EARFCN; LB5.Text = "EARFCN:"; } else { FCN.Text = data.UARFCN; LB5.Text = "UARFCN:"; }
                Band.Text = data.Band;
                SINR.Text = data.SINR;
                RSSI.Text = data.RSSI;
                Signal.Text = data.SignalStrength;
                Distance.Text = data.Distance;
                await Task.Delay(1500, UICts.Token);
                data.Clear();
            }
            ClearDisplay();
        }

        private async void ButtonCon_Click(object sender, EventArgs e)
        {
            FindCOM = false;
            string ATPort = COMList.SelectedItem as string;
            if (ATPort != null || !string.IsNullOrEmpty(ATPort))
            {
                if (ATH != null && ATH.IsOpen == true)
                {
                    ATH.SetNewPort(ATPort);
                    return;
                }
                ATH = new AtHost(ATPort);
                if(ATH.IsGL860)
                {
                    Temp.Visible = true;
                    label5.Visible = true;
                }
                await UIUpdateAsync();
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
                if (AtHost.Check(ATPort, out SerialPort? serial))
                {
                    serial?.Close();
                    serial?.Dispose();
                }
            }
        }

        private async Task RefreshComListAsync()
        {
            while (FindCOM)
            {
                var ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    COMList.Items.Clear();
                    COMList.Items.AddRange(ports);
                    break;
                }
                await Task.Delay(500);
            }
        }
    }
}
