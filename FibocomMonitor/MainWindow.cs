using FibocomMonitor.AT;
using System.IO.Ports;

namespace FibocomMonitor
{
    public partial class MainWindow : Form
    {
        private ATHost? ATH;
        private bool FindCOM = true;

        public MainWindow()
        {
            InitializeComponent();
            base.FormClosing += MainWindow_FormClosing;
            base.Load += MainWindow_FormLoad;
        }

        private async void MainWindow_FormLoad(object? sender, EventArgs e)
        {
            string[] Ports = SerialPort.GetPortNames();
            if (Ports.Length >= 1)
            {
                COMList.Items.AddRange(Ports);
            }
            else
            {
                while (FindCOM)
                {
                    Ports = SerialPort.GetPortNames();
                    if (Ports.Length >= 1)
                    {
                        COMList.Items.AddRange(Ports);
                        break;
                    }
                    await Task.Delay(100);
                }
            }
        }

        private void MainWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (ATH != null)
            {
                if (ATH.IsOpen != false)
                {
                    ATH.ClosePort();
                }
                ATH.Dispose();
            }
        }

        private async Task UIUpdate()
        {
            while (ATH.IsOpen)
            {
                await ATH.SendCommand();
                Data data = ATH.Result;
                data.Clear();
                CarrierList.Items.Clear();
                CarrierList.Items.AddRange([.. data.BandList]);
                Operator.Text = data.Operator;
                StatusNetwork.Text = data.StatusNetwork;
                RSRP.Text = data.RSRP;
                if (data.StatusNetwork == "LTE") { FCN.Text = data.EARFCN; LB5.Text = "EARFCN:"; } else { FCN.Text = data.UARFCN; LB5.Text = "UARFCN:"; }
                Band.Text = data.Band;
                SINR.Text = data.SINR;
                RSSI.Text = data.RSSI;
                Signal.Text = data.SignalStrength;
                Distance.Text = data.Distance;
                await Task.Delay(1500);
            }
            ClearDisplay();
        }

        private async void ButtonCon_Click(object sender, EventArgs e)
        {
            if (COMList.SelectedItem is string ATPort)
            {
                if (ATH != null && ATH.IsOpen == true)
                {
                    ATH.SetNewPort(ATPort);
                    return;
                }
                ATH = new ATHost(ATPort);
                await UIUpdate();
            }
            FindCOM = false;
        }

        private void ClearDisplay()
        {
            var s = "Unknown";
            CarrierList.Items.Clear();
            Operator.Text = s;
            FCN.Text = s;
            StatusNetwork.Text = s;
            RSRP.Text = s;
            Band.Text = s;
            SINR.Text = s;
            RSSI.Text = s;
            Signal.Text = s;
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
                if (ATHost.Check(ATPort, out SerialPort? serial))
                {
                    serial?.Close();
                    serial?.Dispose();
                }
            }
        }
    }
}
