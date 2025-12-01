using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;

namespace FibocomMonitor.AT
{
    public class Data : ICloneable
    {
        public string RSSI { get; set; } = string.Empty;
        public string RSRP { get; set; } = string.Empty;
        public string RSRQ { get; set; } = string.Empty;
        public string SINR { get; set; } = string.Empty;
        public string RSCP { get; set; } = string.Empty;
        public string ECNO { get; set; } = string.Empty;
        public string Band { get; set; } = string.Empty;
        public string SignalStrength { get; set; } = string.Empty;
        public string Distance { get; set; } = string.Empty;
        public string StatusNetwork { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string UARFCN { get; set; } = string.Empty;
        public string EARFCN { get; set; } = string.Empty;
        public string TEMP { get; set; } = string.Empty;
        public List<string> BandList { get; set; } = new List<string>();

        public object Clone()
        {
            return new Data
            {
                RSSI = this.RSSI,
                RSRP = this.RSRP,
                RSRQ = this.RSRQ,
                SINR = this.SINR,
                RSCP = this.RSCP,
                ECNO = this.ECNO,
                Band = this.Band,
                SignalStrength = this.SignalStrength,
                Distance = this.Distance,
                StatusNetwork = this.StatusNetwork,
                Operator = this.Operator,
                UARFCN = this.UARFCN,
                EARFCN = this.EARFCN,
                TEMP = this.TEMP,
                BandList = new List<string>(this.BandList)
            };
        }
    }

    public partial class AtHost : IDisposable
    {
        private readonly SerialPort Port;
        private readonly StringBuilder SB;

        private bool disposed = false;
        private static readonly char[] SplitChars = new[] { ':', ',' };

        public bool IsL860 = false;

        private static readonly (int MaxChannel, string Band)[] LteBands =
            {
    (599,   "B1"),
    (1199,  "B2"),
    (1949,  "B3"),
    (2399,  "B4"),
    (2649,  "B5"),
    (2749,  "B6"),
    (3449,  "B7"),
    (3799,  "B8"),
    (4139,  "B9"),
    (4749,  "B10"),
    (4999,  "B11"),
    (5179,  "B12"),
    (5279,  "B13"),
    (5379,  "B14"),
    (5599,  "B17"),
    (5799,  "B18"),
    (5999,  "B19"),
    (6149,  "B20"),
    (6449,  "B21"),
    (6599,  "B22"),
    (6599,  "B23"),
    (7399,  "B24"),
    (7699,  "B25"),
    (8039,  "B26"),
    (8189,  "B27"),
    (8689,  "B28"),
    (10359, "B29"),
    (11059, "B30"),
    (11139, "B31"),
    (12687, "B32"),
    (13259, "B33"),
    (13959, "B34"),
    (14359, "B35"),
    (14759, "B36"),
    (15159, "B37"),
    (15559, "B38"),
    (16559, "B39"),
    (17359, "B40"),
    (18359, "B41"),
    (19959, "B42"),
    (20359, "B43"),
    (23279, "B44"),
    (23779, "B45"),
    (26279, "B46"),
    (26639, "B47"),
    (28159, "B48"),
    (34589, "B49"),
    (39649, "B50"),
    (41589, "B51"),
    (43589, "B52"),
    (45589, "B53"),
    (46589, "B54"),
    (49689, "B65"),
    (54539, "B66"),
    (55239, "B67"),
    (56739, "B68"),
    (58239, "B69"),
    (59039, "B70"),
    (59739, "B71")
};


        public bool IsOpen { get => Port.IsOpen; }
        public Data Result { get; private set; }

        public AtHost(string serialport)
        {
            Result = new Data();
            SB = new StringBuilder();
            Port = new SerialPort(serialport, 115200, Parity.None, 8, StopBits.One);
            Port.DtrEnable = true;
            Port.Handshake = Handshake.None;
            Port.RtsEnable = true;
            Port.NewLine = "\r";
            Port.WriteTimeout = 500;
            try
            {
                Port.Open();
            }
#if !DEBUG
            catch (Exception ex)
            {
                Dispose();
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#else
            catch (PlatformNotSupportedException) { }
#endif
            CheckSerial();
        }

        ~AtHost()
        {
            Dispose(false);
        }


        public void Start()
        {
            try
            {
                CheckSIM();

                if (IsL860)
                {
                    string mtsm = SendAndRead("AT+MTSM=1");
                    if (mtsm.Contains("+MTSM:"))
                        Result.TEMP = mtsm.Split("+MTSM:")[1].Trim();
                }

                DecoderCOPS(SendAndRead("AT+COPS?"));

                DecoderCSQ(SendAndRead("AT+CSQ?"));

                var mXcc = XCCINFORegex().Match(SendAndRead("AT+XCCINFO?"));
                if (!mXcc.Success)
                    throw new InvalidOperationException("Invalid XCCINFO response.");

                if (!int.TryParse(mXcc.Groups["ta"].Value, out int ta))
                    throw new InvalidOperationException("Failed to parse TA.");

                string xmci = SendAndRead("AT+XMCI=1");

                var ratMatch = XMCIFindRat().Match(xmci);
                if (!ratMatch.Success || !int.TryParse(ratMatch.Groups["Type"].Value, out int rat))
                    throw new InvalidOperationException("XMCI: RAT not found.");

                if (rat == 2 || rat == 7)
                    throw new InvalidOperationException("Invalid RAT type.");

                int[] bwlist = Array.Empty<int>();
                if (rat == 3 || rat == 7)
                {
                    var mXlec = XLECRegex().Match(SendAndRead("AT+XLEC?"));
                    if (!mXlec.Success)
                        throw new InvalidOperationException("Invalid XLEC response.");

                    bwlist = mXlec.Groups["bw"].Captures
                        .Select(c => int.Parse(c.Value))
                        .ToArray();

                    if (bwlist.Length == 0)
                        throw new InvalidOperationException("XLEC returned no BW values.");
                }

                var xmciLines = xmci
                    .Split("\n", StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => x.Contains("+XMCI:"))
                    .ToArray();

                if (xmciLines.Length == 0)
                    throw new InvalidOperationException("XMCI returned no carriers.");

                //DecoderXS(ta, rat, xmci);
                DecoderXS(rat, xmci);

                XMCIDecoder(xmciLines, rat, bwlist);
            }
#if !DEBUG
            catch (Exception ex)
            {
               Dispose();
               MessageBox.Show($"{ex.Message}\r\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
#else
            catch (PlatformNotSupportedException) { }
#endif
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                if (Port.IsOpen)
                {
                    Port.Close();
                }
                Port.Dispose();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region Decoder
        private void XMCIDecoder(string[] lines, int rat, int[] bwcodes) // Cell list
        {
            string bandAccum = string.Empty;
            int idx = 0;

            Result.BandList.Clear();

            for (int i = 0; i < lines.Length; i++)
            {
                Debug.WriteLine($"Line {i}: {lines[i]}");

                string[] line = lines[i].Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);

                if (line[0].StartsWith("+XMCI"))
                {
                    line[0] = line[0].Replace("+XMCI", "").Trim();
                }

                if (rat == 4 || rat == 5 || rat == 7) // 4G
                {
                    int earfcn = Convert.ToInt32(line[7].Trim('"'), 16);
                    int rsrp = int.Parse(line[10].Trim()) - 141;
                    int rsrq = int.Parse(line[11].Trim()) / 2 - 20;
                    int pci = Convert.ToInt32((line[6].Trim('"')), 16);

                    string band = GetBandLte(earfcn);

                    if (idx == 0)
                    {
                        Result.EARFCN = earfcn.ToString();
                    }

                    if (idx < 3)
                    {
                        bandAccum += band + " ";
                    }

                    Result.BandList.Add($"Carrier {idx}: EARFCN: {earfcn} PCI: {pci} Band: {band} RSRP: {rsrp}dBm  RSRQ: {rsrq}dB");
                }
                else // 3G
                {
                    Debug.WriteLine($"Line {i}: {lines[i]}");

                    int psc = Convert.ToInt32(line[5].Trim('"'), 16);
                    int uarfcn = Convert.ToInt32(line[6].Trim('"'), 16);
                    int rawRscp = int.Parse(line[9].Trim('"'));
                    int rawEcno = int.Parse(line[10].Trim('"'));
                    long cgi = Convert.ToInt32(line[4].Trim('"'), 16);

                    int ci = (int)(cgi & 0xFFFF);
                    int rscp = rawRscp - 120;
                    double ecno = (rawEcno / 2.0) - 24;

                    if (idx == 0)
                    {
                        Result.UARFCN = uarfcn.ToString();
                        Result.RSCP = rscp.ToString();
                        Result.ECNO = ecno.ToString();
                    }

                    string bandPart = $"{GetBandUMTS(uarfcn.ToString())}";
                    if (idx < 3)
                    {
                        bandAccum += bandPart + " ";
                    }
                    else if (idx == 3)
                    {
                        bandAccum += bandPart;
                    }

                    Result.BandList.Add($"Carrier {idx}: CI: {ci} PSC: {psc} Band (UARFCN): {GetBandUMTS(uarfcn.ToString())} ({uarfcn}) RSCP: {rscp:0}dBm  EcNo: {ecno:0}dB");
                }
                idx++;
            }

            Result.Band = bandAccum.Trim();
        }

        private void DecoderCSQ(string response)
        {
            var match = CSQRegex().Match(response);
            if (match.Success)
            {
                int sig = int.Parse(match.Groups["sg"].Value) * 100 / 31;
                Result.SignalStrength = sig.ToString("0") + " %";
                int rssi = int.Parse(match.Groups["RSSI"].Value) - 111;
                Result.RSSI = $"{rssi:0} dBm";
            }
            else
            {
                throw new InvalidOperationException("Failed to parse CSQ response.");
            }
        }

        private void DecoderXS(int rat, string xmciLine) // Main cell
        {
            ArgumentNullException.ThrowIfNull(xmciLine, nameof(xmciLine));

            string[] values = xmciLine.Split(',');

            if (rat == 4 || rat == 7) // 4G
            {
                int rsrpVal = int.Parse(values[9].Trim('"')) - 141;
                int rsrqVal = int.Parse(values[10].Trim('"')) / 2 - 20;
                int sinrVal = int.Parse(values[11].Trim('"')) / 2;
                int ta = Convert.ToInt32(values[12].Trim('"'), 16);

                Result.RSRP = $"{rsrpVal} dBm";
                Result.RSRQ = $"{rsrqVal} dB";
                Result.SINR = $"{sinrVal} dB";

                if (ta > 0)
                    Result.Distance = $"{Math.Round((ta * 78.125) / 1000, 3)} km";
            }
        }

        private void DecoderCOPS(string response)
        {
            var match = COPSRegex().Match(response);
            if (match.Success)
            {
                Result.Operator = match.Groups["Operator"].Value;
                Result.StatusNetwork = match.Groups["Type"].Value switch
                {
                    "2" => "UMTS",
                    "3" or "7" => "LTE",
                    "4" => "HSDPA",
                    "5" => "HSUPA",
                    "6" => "HSPA",
                    _ => "Unknown"
                };
            }
            else
            {
                throw new InvalidOperationException("Failed to parse COPS response.");
            }
        }
        #endregion

        #region Regex
        [GeneratedRegex(@"\+XLEC: (?:\d+),(?<no_of_cells>\d+),(?:(?<bw>\d+),*)+(?:BAND_LTE_(?:(?<band>\d+),*)+)?", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XLECRegex();

        [GeneratedRegex(@"\+XCCINFO:\s*\d+,\s*\d+,\s*\d+,\s*""[^""]*"",\s*\d+,\s*\d+,\s*""[^""]*"",\s*(?<ta>\d+),", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex XCCINFORegex();

        [GeneratedRegex(@"\+XMCI:\s*(?<Type>\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XMCIFindRat();

        [GeneratedRegex(@"\+COPS: \d+,\d+,""(?<Operator>[^""]+)"",(?<Type>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex COPSRegex();

        [GeneratedRegex(@"\+CSQ:\s(?<RSSI>\d+),(?<sg>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex CSQRegex();
        #endregion

        #region Functions
        private string SendAndRead(string command)
        {
            if (!Port.IsOpen)
            {
                return string.Empty;
            }

            SB.Clear();
            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();
            Port.Write(command + "\r");

            var deadline = DateTime.UtcNow.AddMilliseconds(3000);

            while (DateTime.UtcNow < deadline && IsOpen)
            {
                if (Port.BytesToRead > 0)
                {
                    SB.Append(Port.ReadExisting());
                    var txt = SB.ToString();

                    if (txt.Contains("\r\nOK\r\n") ||
                        txt.Contains("\r\nERROR\r\n") ||
                        txt.Contains("+CME ERROR"))
                    {
                        Debug.WriteLine("Response:\r\n" + txt);
                        break;
                    }
                }
                else
                {
                    try
                    {
                        Thread.Sleep(50);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }
            return SB.ToString().Trim();
        }

        public void ClosePort()
        {
            if (Port.IsOpen)
            {
                Port.Close();
            }
        }

        public void SetNewPort(string serialport)
        {
            Port.Close();
            Port.PortName = serialport;
            Port.Open();
        }

        private static double ConvertRsrpToRssi(double rsrp, int bandwidthCode)
        {

            int np = bandwidthCode switch
            {
                0 => 6,
                1 => 15,
                2 => 25,
                3 => 50,
                4 => 75,
                5 => 100,
                _ => 0
            };

            if (np > 0)
            {
                return rsrp + 10 * Math.Log10(12 * np);
            }

            return -113;
        }

        private static double GetFrequency(int bwCode) => bwCode switch
        {
            0 => 1.4,
            1 => 3,
            2 => 5,
            3 => 10,
            4 => 15,
            5 => 20,
            _ => 0
        };

        private static string GetBandUMTS(string uarfcn)
        {
            if (string.IsNullOrWhiteSpace(uarfcn))
                return "--";

            uarfcn = uarfcn.Trim().Trim('"');

            if (!int.TryParse(uarfcn, out int channel))
                return "--";

            if (channel is >= 10562 and <= 10838) return "B1";   // 2100 MHz
            if (channel is >= 9662 and <= 9938) return "B2";   // 1900 MHz
            if (channel is >= 1162 and <= 1513) return "B5";   // 850 MHz
            if (channel is >= 1537 and <= 1738) return "B8";   // 900 MHz
            if (channel is >= 412 and <= 587) return "B19";  // 800 MHz

            return "--";
        }


        private static string GetBandLte(int earfcn)
        {
            foreach (var (MaxChannel, Band) in LteBands)
            {
                if (earfcn <= MaxChannel)
                    return Band;
            }
            return "--";
        }

        /// <summary>
        ///   Проверка на открытый COM порт.
        /// </summary>
        /// <param name="AtPort"></param>
        /// <param name="serialPort"></param>
        /// <returns>true если порт открыт. false если он закрыт или прозошла ошибка.</returns>
        public static bool Check(string AtPort, out SerialPort? serialPort)
        {
            try
            {
                var serial = new SerialPort(AtPort);
                serial.Open();
                serialPort = serial;
                return serial.IsOpen;
            }
            catch
            {
                serialPort = null;
                return false;
            }
        }

        private void CheckSIM()
        {
            string cpin = SendAndRead("AT+CPIN?");
            if (!cpin.Contains("+CPIN: SIM PIN"))
            {
                throw new Exception("No SIM or port not work!");
            }
        }

        private void CheckSerial()
        {
            string mtsm = SendAndRead("AT+MTSM=1");
            if (!mtsm.Contains("ERROR"))
            {
                IsL860 = true;
                Debug.WriteLine("Modem: L860");
            }
        }

        #endregion
    }
}
