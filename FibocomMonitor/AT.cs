using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

        public void Clear()
        {
            RSSI = RSRP = RSRQ = SINR = RSCP = ECNO = Band = SignalStrength =
            Distance = StatusNetwork = Operator = UARFCN = EARFCN = TEMP = string.Empty;
            BandList.Clear();
        }

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
        public bool IsGL860 = false;

        public bool IsOpen { get; private set; } = false;
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
            Port.PinChanged += Port_PinChanged;
            try
            {
                Open();
            }
            catch (Exception ex)
            {
                Close(true);
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            CheckSerial();
        }

        ~AtHost()
        {
            Dispose(false);
        }

        private void Port_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            if (!IsOpen) return;

            switch (e.EventType)
            {
                case SerialPinChange.CDChanged:
                case SerialPinChange.DsrChanged:
                case SerialPinChange.CtsChanged:
                    Close(true);
                    break;
                default:
                    break;
            }
        }

        public void SendCommand()
        {
            try
            {
                CheckSIM();
                if (IsGL860)
                {
                    string mtsm = SendAndRead("AT+MTSM=1");
                    var mMtsm = MTSMRegex().Match(mtsm);
                    Result.TEMP = mMtsm.Success ? mMtsm.Value : string.Empty;
                }

                string cops = SendAndRead("AT+COPS?");
                DecoderCOPS(cops);

                string csq = SendAndRead("AT+CSQ?");
                DecoderCSQ(csq);

                string xccinfo = SendAndRead("AT+XCCINFO?");
                var mXcc = XCCINFORegex().Match(xccinfo);
                if (!mXcc.Success || !int.TryParse(mXcc.Groups["ta"].Value, out int ta))
                    throw new InvalidOperationException("Failed to parse TA from XCCINFO response.");

                string xmci = SendAndRead("AT+XMCI=1");

                Match ratMatch = XMCIFindRat().Match(xmci);
                if (!ratMatch.Groups["Type"].Success || !int.TryParse(ratMatch.Groups["Type"].Value, out int rat))
                {
                    throw new InvalidOperationException("XMCI return null.");
                }

                Debug.WriteLine("RAT:" + rat);

                if (rat < 2 || rat > 7)
                {
                    throw new InvalidOperationException("Unknown network type.");
                }

                int[] bwlist = Array.Empty<int>();
                if (rat == 3 || rat == 7)
                {
                    string xlec = SendAndRead("AT+XLEC?");
                    var mXlec = XLECRegex().Match(xlec);
                    if (!mXlec.Success) throw new InvalidOperationException("Failed to parse XLEC response.");
                    bwlist = mXlec.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s =>
                        {
                            if(s.Contains("+XLEC:"))
                            {
                               return int.Parse(s.Replace("+XLEC:","").Trim());
                            }
                            if(s.Contains("BAND_LTE_"))
                            {
                                return 0;
                            }
                            return int.Parse(s.Trim());
                        }).ToArray();
                    if (bwlist.Length == 0) throw new InvalidOperationException("No bandwidth codes found in XLEC.");
                }

                var xmciLines = GetXMCILines(xmci, rat);
                if (xmciLines == null || xmciLines.Count == 0)
                    throw new InvalidOperationException("XMCI lines not found.");

                DecoderXS(ta, rat, xmciLines[0]);
                XMCIDecoder(xmciLines, rat, bwlist);
            }
            catch (Exception ex)
            {
                Close(true);
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                Port.Close();
                Port.Dispose();
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Close(bool dispose)
        {
            IsOpen = false;
            Port.Close();
            if (dispose) { Dispose(); }
        }

        private void Open()
        {
            Port.Open();
            IsOpen = true;
        }

        #region Decoder
        private void XMCIDecoder(MatchCollection matches, int rat, int[] bwcodes)
        {
            string bandAccum = string.Empty;
            int idx = 0;

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (!match.Success)
                    continue;

                if (rat == 4 || rat == 7) // 4G
                {
                    if (!match.Groups["PCI"].Success ||
                        !match.Groups["EARFCN"].Success ||
                        !match.Groups["RSRP"].Success ||
                        !match.Groups["RSRQ"].Success)
                        continue;

                    int pci,earfcn;
                    try
                    {
                        pci = Convert.ToInt32(match.Groups["PCI"].Value, 16);
                        earfcn = Convert.ToInt32(match.Groups["EARFCN"].Value, 16);
                    }
                    catch { continue; }

                    if (!int.TryParse(match.Groups["RSRP"].Value, out int rawRsrp))
                        continue;
                    if (!int.TryParse(match.Groups["RSRQ"].Value, out int rawRsrq))
                        continue;

                    int rsrp = rawRsrp - 141;
                    int rsrq = rawRsrq / 2 - 20;
                    string band = GetBandLte(earfcn.ToString());

                    int ci = 0;
                    if (match.Groups["eNodeB"].Success &&
                        int.TryParse(match.Groups["eNodeB"].Value, NumberStyles.HexNumber, null, out int parsedEnodeB))
                    {
                        ci = (parsedEnodeB << 8) | pci;
                    }

                    string bandPart = band;
                    int bw = (idx < bwcodes.Length) ? bwcodes[idx] : bwcodes.LastOrDefault();
                    bandPart += $"|{GetBandwidthFrequency(bw)}MHz";
                    if (idx <= 3) bandAccum += bandPart + " ";

                    Result.BandList.Add($"Carrier {idx}: CI: {ci} PCI: {pci} Band (EARFCN): {band} ({earfcn}) RSRP: {rsrp}dBm  RSRQ: {rsrq}dB");
                }
                else if (rat != 3 && rat != 7) // 3G
                {
                    if (!match.Groups["PSC"].Success ||
                        !match.Groups["UARFCN"].Success ||
                        !match.Groups["RSCP"].Success ||
                        !match.Groups["ECNO"].Success ||
                        !match.Groups["CGI"].Success)
                        continue;

                    int psc = Convert.ToInt32(match.Groups["PSC"].Value, 16);
                    int uarfcn = Convert.ToInt32(match.Groups["UARFCN"].Value, 16);

                    if (!int.TryParse(match.Groups["RSCP"].Value, out int rawRscp))
                        continue;
                    if (!int.TryParse(match.Groups["ECNO"].Value, out int rawEcno))
                        continue;

                    long cgi = Convert.ToInt32(match.Groups["CGI"].Value, 16);
                    int ci = (int)(cgi & 0xFFFF);

                    int rscp = rawRscp - 120;
                    double ecno = (rawEcno / 2.0) - 24;

                    if (idx == 0)
                        Result.UARFCN = uarfcn.ToString();

                    string bandPart = $"{GetBandUMTS(uarfcn.ToString())}";
                    if (idx < match.Groups.Count)
                    {
                        bandAccum += bandPart + "|";
                    }
                    else
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
                Result.RSSI = $"{rssi:0} dbm";
            }
            else
            {
                throw new InvalidOperationException("Failed to parse CSQ response.");
            }
        }

        private void DecoderXS(int ta, int rat, Match xmciLine)
        {
            if(!xmciLine.Success)
            {
                throw new InvalidOperationException("Failed to parse XMCI response.");
            }
            if (rat == 4 || rat == 7) // 4G
            {
                int earf = Convert.ToInt32(xmciLine.Groups["EARFCN"].Value, 16);
                int rsrpVal = int.Parse(xmciLine.Groups["RSRP"].Value) - 141;
                int rsrqVal = int.Parse(xmciLine.Groups["RSRQ"].Value) / 2 - 20;
                int sinrVal = int.Parse(xmciLine.Groups["SINR"].Value) / 2;

                Result.RSRP = $"{rsrpVal} dBm";
                Result.RSRQ = $"{rsrqVal} dB";
                Result.SINR = $"{sinrVal} dB";
                Result.EARFCN = earf.ToString();
            }
            else if (rat != 3 && rat != 7) // 3G
            {
                int uarfcn = Convert.ToInt32(xmciLine.Groups["UARFCN"].Value, 16);
                int rscp = int.Parse(xmciLine.Groups["RSCP"].Value) - 121;
                int ecno = int.Parse(xmciLine.Groups["ECNO"].Value) / 2 - 24;

                Result.UARFCN = uarfcn.ToString();
                Result.RSCP = $"{rscp} dBm";
                Result.ECNO = $"{ecno} dB";
            }
            if (ta > 0)
                Result.Distance = $"{Math.Round((ta * 78.125) / 1000, 3)} km";
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

        [StringSyntax("Regex")]
        private const string XMCIR = @"\+XMCI:\s*\d,[^,]*,[^,]*,""(?<TA>0x[0-9A-Fa-f]+)"",""(?<eNodeB>0x[0-9A-Fa-f]+)"",""(?<PCI>0x[0-9A-Fa-f]+)"",""(?<EARFCN>0x[0-9A-Fa-f]+)"",""[^""]+"",""[^""]+"",(?<RSRP>\d+),(?<RSRQ>\d+),(?<SINR>\d+)";

        [StringSyntax("Regex")]
        private const string XMCIRUMTS = @"\+XMCI:\s*\d\s*,\s*[^,]*\s*,\s*[^,]*\s*,\s*""(?<CGI>0x[0-9A-Fa-f]+|\d+)""\s*,\s*""[^""]*""\s*,\s*""(?<PSC>0x[0-9A-Fa-f]+|\d+)""\s*,\s*""(?<UARFCN>0x[0-9A-Fa-f]+|\d+)""\s*,\s*""[^""]*""\s*,\s*""[^""]*""\s*,\s*(?<RSCP>-?\d+)\s*,\s*(?<ECNO>-?\d+)\s*,";


        [GeneratedRegex(@"\+XLEC:\s(\d*),(\d*),(\d*),\s*\D*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XLECRegex();

        [GeneratedRegex(@"\+XCCINFO:\s*\d+,\s*\d+,\s*\d+,\s*""[^""]*"",\s*\d+,\s*\d+,\s*""[^""]*"",\s*(?<ta>\d+),", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex XCCINFORegex();

        [GeneratedRegex(XMCIR, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XMCIRegexLte();

        [GeneratedRegex(XMCIRUMTS, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XMCIRegexUMTS();

        [GeneratedRegex(@"\+XMCI:\s*(?<Type>\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XMCIFindRat();

        [GeneratedRegex(@"\+COPS: \d+,\d+,""(?<Operator>[^""]+)"",(?<Type>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex COPSRegex();

        [GeneratedRegex(@"\+CSQ:\s(?<RSSI>\d+),(?<sg>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex CSQRegex();

        [GeneratedRegex(@"\+CPIN:\sSIM\sPIN\d", RegexOptions.None)]
        private static partial Regex CPINRegex();

        [GeneratedRegex(@"\+MTSM:\s\d+", RegexOptions.None)]
        private static partial Regex MTSMRegex();
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
                        txt.Contains("+CME ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine(txt);
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
                Close(false);
            }
            Result.Clear();
        }

        public void SetNewPort(string serialport)
        {
            Close(false);
            Port.PortName = serialport;
            Open();
        }

        public static double? ConvertRsrpToRssi(double? rsrp, int? bandwidthCode)
        {
            if (rsrp == null) throw new ArgumentNullException(nameof(rsrp));
            if (bandwidthCode == null) throw new ArgumentNullException(nameof(bandwidthCode));

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

        private static double GetBandwidthFrequency(int bwCode) => bwCode switch
        {
            0 => 1.4,
            1 => 20, // !! Старое значение: 3 !!
            2 => 5,
            3 => 10,
            4 => 15,
            5 => 20,
            _ => 0
        };

        private static MatchCollection GetXMCILines(string response, int rat)
        {
            if (rat == 3 || rat == 7) // 4G
            {
                var result = XMCIRegexLte().Matches(response);
                if (result == null || result.Count == 0)
                {
                    throw new InvalidOperationException("4G: Failed to parse XMCI response.");
                }
                return result;
            }
            else
            if (rat != 3 || rat != 7) // 3G
            {
                var result = XMCIRegexUMTS().Matches(response);
                if (result == null || result.Count == 0)
                {
                    throw new InvalidOperationException("3G: Failed to parse XMCI response.");
                }
                return result;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private static string GetBandUMTS(string uarfcn)
        {
            if (string.IsNullOrWhiteSpace(uarfcn))
                return "--";

            uarfcn = uarfcn.Trim().Trim('"');

            if (!int.TryParse(uarfcn, NumberStyles.Integer, CultureInfo.InvariantCulture, out int channel))
                return "--";

            if (channel is >= 10562 and <= 10838) return "B1";   // 2100 MHz
            if (channel is >= 9662 and <= 9938) return "B2";   // 1900 MHz
            if (channel is >= 1162 and <= 1513) return "B5";   // 850 MHz
            if (channel is >= 1537 and <= 1738) return "B8";   // 900 MHz
            if (channel is >= 412 and <= 587) return "B19";  // 800 MHz

            return "--";
        }


        private static string GetBandLte(string dluarfnc)
        {
            if (string.IsNullOrWhiteSpace(dluarfnc))
                return "--";

            dluarfnc = dluarfnc.Trim().Trim('"');

            int channel;

            if (!int.TryParse(dluarfnc, out channel))
                return "--";

            foreach (var (MaxChannel, Band) in LteBands)
            {
                if (channel <= MaxChannel)
                    return Band;
            }

            return "--";
        }

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
            if (!CPINRegex().Match(cpin).Success)
            {
                Dispose();
                IsOpen = false;
                throw new Exception("No SIM.");
            }
        }

        private void CheckSerial()
        {
            string mtsm = SendAndRead("AT+MTSM=1");
            if (!mtsm.Contains("ERROR"))
            {
                IsGL860 = true;
            }
        }

        private static readonly (int MaxChannel, string Band)[] LteBands =
        [
        (599, "B1"), (1199, "B2"), (1949, "B3"), (2399, "B4"),
        (2649, "B5"), (2749, "B6"), (3449, "B7"), (3799, "B8"),
        (4149, "B9"), (4749, "B10"), (4949, "B11"), (5009, "--"),
        (5179, "B12"), (5279, "B13"), (5379, "B14"), (5729, "--"),
        (5849, "B17"), (5999, "B18"), (6149, "B19"), (6449, "B20"),
        (6599, "B21"), (7399, "B22"), (7499, "--"), (7699, "B23"),
        (8039, "B24"), (8689, "B25"), (9039, "B26"), (9209, "B27"),
        (9659, "B28"), (9769, "B29"), (9869, "B30"), (9919, "B31"),
        (10399, "B32"), (35999, "--"), (36199, "B33"), (36349, "B34"),
        (36949, "B35"), (37549, "B36"), (37749, "B37"), (38249, "B38"),
        (38649, "B39"), (39649, "B40"), (41589, "B41"), (43589, "B42"),
        (45589, "B43"), (46589, "B44"), (46789, "B45"), (54539, "B46"),
        (55239, "B47"), (56739, "B48"), (58239, "B49"), (59089, "B50"),
        (59139, "B51"), (60139, "B52"), (60254, "B53"), (65535, "--"),
        (66435, "B65"), (67335, "B66"), (67535, "B67"), (67835, "B68"),
        (68335, "B69"), (68585, "B70"), (68935, "B71"), (68985, "B72"),
        (69035, "B73"), (69465, "B74"), (70315, "B75"), (70365, "B76"),
        (70545, "B85"), (70595, "B87"), (70645, "B88")
        ];

        #endregion
    }
}
