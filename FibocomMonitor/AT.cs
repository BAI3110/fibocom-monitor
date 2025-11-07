using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace FibocomMonitor.AT
{
    public class Data
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

    }

    public partial class AtHost : IDisposable
    {
        private readonly SerialPort Port;
        public bool IsGL860 = false;

        public bool IsOpen { get; private set; } = false;
        public Data Result { get; private set; }

        public AtHost(string serialport)
        {
            Result = new Data();
            Port = new SerialPort(serialport, 115200, Parity.None, 8, StopBits.One);
            Port.DtrEnable = true;
            Port.Handshake = Handshake.None;
            Port.RtsEnable = true;
            Port.NewLine = "\r";
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

        private void Port_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            Close(true);
        }

        public void SendCommand()
        {
            try
            {
                CheckSIM();
                if(IsGL860)
                {
                    string mtsm = SendAndRead("AT+MTSM=1");
                    Result.TEMP = MTSMRegex().Match(mtsm).Value;
                }
                string cops = SendAndRead("AT+COPS?");
                DecoderCOPS(cops);
                string csq = SendAndRead("AT+CSQ?");
                DecoderCSQ(csq);

                string response = SendAndRead("AT+XCCINFO?; +XLEC?; +XMCI=1");

                int ta = int.Parse(XCCINFORegex().Match(response).Groups["ta"].Value);

                var mXlec = XLECRegex().Match(response);

                int[] bwcodes = mXlec.Groups["bwlist"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                var ratMatch = XMCIFindRat().Match(response);
                if (!ratMatch.Success || 
                    !int.TryParse(ratMatch.Groups["Type"].Value, out int rat) ||
                    (rat != 2 && rat != 4))
                {
                    throw new Exception("Unknow network type.");
                }

                MatchCollection xmciLines = GetXMCILines(response, rat);
                DecoderXS(bwcodes[0], ta, rat, xmciLines[0]);
                XMCIDecoder(xmciLines, rat, bwcodes);
            }
            catch (Exception ex) { Close(true); MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        public void Dispose()
        {
            if (Port.IsOpen)
            {
                Port.Close();
                IsOpen = false;
            }
            Port.Dispose();
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
            int idx = 1;

            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (!match.Success)
                    continue;

                if (rat == 4) // 4G
                {
                    if (!match.Groups["PCI"].Success ||
                        !match.Groups["EARFCN"].Success ||
                        !match.Groups["RSRP"].Success ||
                        !match.Groups["RSRQ"].Success)
                        continue;

                    int pci = int.Parse(match.Groups["PCI"].Value, NumberStyles.HexNumber);
                    int earfcn = int.Parse(match.Groups["EARFCN"].Value, NumberStyles.HexNumber);

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
                else if (rat == 3) // 3G
                {
                    if (!match.Groups["PSC"].Success ||
                        !match.Groups["UARFCN"].Success ||
                        !match.Groups["RSCP"].Success ||
                        !match.Groups["ECNO"].Success ||
                        !match.Groups["CGI"].Success)
                        continue;

                    int psc = int.Parse(match.Groups["PSC"].Value, NumberStyles.HexNumber);
                    int uarfcn = int.Parse(match.Groups["UARFCN"].Value, NumberStyles.HexNumber);

                    if (!int.TryParse(match.Groups["RSCP"].Value, out int rawRscp))
                        continue;
                    if (!int.TryParse(match.Groups["ECNO"].Value, out int rawEcno))
                        continue;

                    long cgi = long.Parse(match.Groups["CGI"].Value, NumberStyles.HexNumber);
                    int ci = (int)(cgi & 0xFFFF);

                    int rscp = rawRscp - 120;
                    double ecno = (rawEcno / 2.0) - 24;

                    if (idx == 0)
                        Result.UARFCN = uarfcn.ToString();

                    string bandPart = $"{GetBandUMTS(uarfcn.ToString())}";
                    if (idx < bwcodes.Length)
                        bandAccum += bandPart + " ";

                    Result.BandList.Add($"Carrier {idx}: CI: {ci} PSC: {psc} Band (UARFCN): {GetBandUMTS(uarfcn.ToString())} ({uarfcn}) RSCP: {rscp}dBm  EcNo: {ecno:F1}dB");
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
            }
            else
            {
                throw new Exception("Regex: CSQ return null.");
            }
        }

        private void DecoderXS(int bwcode, int ta, int rat, Match xmciLine)
        {
            if (rat == 4) // 4G
            {
                int earf = Convert.ToInt32(xmciLine.Groups["EARFCN"].Value, 16);
                int rsrpVal = int.Parse(xmciLine.Groups["RSRP"].Value) - 141;
                int rsrqVal = int.Parse(xmciLine.Groups["RSRQ"].Value) / 2 - 20;
                int sinrVal = int.Parse(xmciLine.Groups["SINR"].Value) / 2;

                Result.RSRP = $"{rsrpVal} dBm";
                Result.RSRQ = $"{rsrqVal} dB";
                Result.SINR = $"{sinrVal} dB";
                Result.EARFCN = earf.ToString();
                var rssi = ConvertRsrpToRssi(rsrpVal, bwcode);
                Result.RSSI = rssi.HasValue ? $"{rssi:0} dBm" : "Unknown";
            }
            else if (rat == 3) // 3G
            {
                int uarfcn = int.Parse(xmciLine.Groups["UARFCN"].Value);
                int rssi = int.Parse(xmciLine.Groups["RSSI"].Value) - 111;
                int rscp = int.Parse(xmciLine.Groups["RSCP"].Value) - 121;
                int ecno = int.Parse(xmciLine.Groups["ECNO"].Value) / 2 - 24;

                Result.UARFCN = uarfcn.ToString();
                Result.RSSI = $"{rssi} dBm";
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
                    "0" => "EDGE",
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
                throw new Exception("Regex: COPS return null.");
            }
        }
        #endregion

        #region Regex
        private const string Regexstr = "Regex";

        [StringSyntax(Regexstr)]
        private const string XMCIR = @"\+XMCI:\s*[45],[^,]*,[^,]*,""(?<TA>0x[0-9A-Fa-f]+)"",""(?<eNodeB>0x[0-9A-Fa-f]+)"",""(?<PCI>0x[0-9A-Fa-f]+)"",""(?<EARFCN>0x[0-9A-Fa-f]+)"",""[^""]+"",""[^""]+"",(?<RSRP>\d+),(?<RSRQ>\d+),(?<SINR>\d+)";

        [StringSyntax(Regexstr)]
        private const string XMCIRUMTS = @"\+XMCI:\s*2,[^,]*,[^,]*,""(?<CGI>0x[0-9A-Fa-f]+)"",""[^""]+"",""(?<PSC>0x[0-9A-Fa-f]+)"",""(?<UARFCN>0x[0-9A-Fa-f]+)"",""[^""]+"",""[^""]+"",(?<RSCP>\d+),(?<ECNO>\d+),[^,]+,""[^""]+"",""[^""]+""";

        [GeneratedRegex(@"\+XLEC:\s*\d+,\s*\d+,\s*(?<bwlist>(?:\d+,?)*)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XLECRegex();

        [GeneratedRegex(@"\+XCCINFO:\s*\d+,\s*\d+,\s*\d+,\s*""[^""]*"",\s*\d+,\s*\d+,\s*""[^""]*"",\s*(?<ta>\d+),", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
        private static partial Regex XCCINFORegex();

        [GeneratedRegex(XMCIR, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XMCIRegexLte();

        [GeneratedRegex(XMCIRUMTS, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex XMCIRegexUMTS();

        [GeneratedRegex(@"\+XMCI:\s*(?<Type>[34])", RegexOptions.IgnoreCase)]
        private static partial Regex XMCIFindRat();

        [GeneratedRegex(@"\+COPS: \d+,\d+,""(?<Operator>[^""]+)"",(?<Type>\d+)", RegexOptions.IgnoreCase)]
        private static partial Regex COPSRegex();

        [GeneratedRegex(@"\+CSQ:\s(?<rssi>\d+),(?<sg>\d+)", RegexOptions.IgnoreCase)]
        private static partial Regex CSQRegex();

        [GeneratedRegex(@"\+CPIN:\sSIM\sPIN[0-9]+", RegexOptions.None)]
        private static partial Regex CPINRegex();

        [GeneratedRegex(@"\+MTSM:\s[0-9]+", RegexOptions.None)]
        private static partial Regex MTSMRegex();
        #endregion

        #region Functions
        private string SendAndRead(string command)
        {
            if (!Port.IsOpen)
            {
                return string.Empty;
            }

            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();
            Port.WriteTimeout = 500; // Write timeout
            Port.Write(command + "\r");

            var sb = new System.Text.StringBuilder();
            var deadline = DateTime.UtcNow.AddMilliseconds(3000); // Read timeout

            while (DateTime.UtcNow < deadline && IsOpen != true)
            {
                if (Port.BytesToRead > 0)
                {
                    sb.Append(Port.ReadExisting());
                    var txt = sb.ToString();

                    if (txt.Contains("\r\nOK\r\n") ||
                        txt.Contains("\r\nERROR\r\n") ||
                        txt.IndexOf("+CME ERROR", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }
                }
                else
                {
                    try
                    {
                        Task.Delay(50).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }

            return sb.ToString().Trim();
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
            ArgumentNullException.ThrowIfNullOrEmpty(nameof(rsrp));
            ArgumentNullException.ThrowIfNullOrEmpty(nameof(bandwidthCode));

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
            1 => 3,
            2 => 5,
            3 => 10,
            4 => 15,
            5 => 20,
            _ => 0
        };

        private MatchCollection GetXMCILines(string response, int rat)
        {
            if (rat == 4)
            {
                var result = XMCIRegexLte().Matches(response);
                if(result == null || result.Count == 0)
                {
                    throw new Exception("XMCI return null.");
                }
                return result;
            }
            else
            if (rat == 3)
            {
                var result = XMCIRegexUMTS().Matches(response);
                if (result == null || result.Count == 0)
                {
                    throw new Exception("XMCI return null.");
                }
                return result;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static string GetBandUMTS(string uarfcn)
        {
            if (string.IsNullOrWhiteSpace(uarfcn))
                return "--";

            uarfcn = uarfcn.Trim().Trim('"');

            int channel;

            if (uarfcn.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(uarfcn[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out channel))
                    return "--";
            }
            else
            {
                if (!int.TryParse(uarfcn, NumberStyles.Integer, CultureInfo.InvariantCulture, out channel))
                    return "--";
            }

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

            if (dluarfnc.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(dluarfnc[2..], NumberStyles.HexNumber, null, out channel))
                    return "--";
            }
            else
            {
                if (!int.TryParse(dluarfnc, out channel))
                    return "--";
            }

            if (channel < 600) return "B1";
            if (channel < 1200) return "B2";
            if (channel < 1950) return "B3";
            if (channel < 2400) return "B4";
            if (channel < 2650) return "B5";
            if (channel < 2750) return "B6";
            if (channel < 3450) return "B7";
            if (channel < 3800) return "B8";
            if (channel < 4150) return "B9";
            if (channel < 4750) return "B10";
            if (channel < 4950) return "B11";
            if (channel < 5010) return "--";
            if (channel < 5180) return "B12";
            if (channel < 5280) return "B13";
            if (channel < 5380) return "B14";
            if (channel < 5730) return "--";
            if (channel < 5850) return "B17";
            if (channel < 6000) return "B18";
            if (channel < 6150) return "B19";
            if (channel < 6450) return "B20";
            if (channel < 6600) return "B21";
            if (channel < 7400) return "B22";
            if (channel < 7500) return "--";
            if (channel < 7700) return "B23";
            if (channel < 8040) return "B24";
            if (channel < 8690) return "B25";
            if (channel < 9040) return "B26";
            if (channel < 9210) return "B27";
            if (channel < 9660) return "B28";
            if (channel < 9770) return "B29";
            if (channel < 9870) return "B30";
            if (channel < 9920) return "B31";
            if (channel < 10400) return "B32";
            if (channel < 36000) return "--";
            if (channel < 36200) return "B33";
            if (channel < 36350) return "B34";
            if (channel < 36950) return "B35";
            if (channel < 37550) return "B36";
            if (channel < 37750) return "B37";
            if (channel < 38250) return "B38";
            if (channel < 38650) return "B39";
            if (channel < 39650) return "B40";
            if (channel < 41590) return "B41";
            if (channel < 43590) return "B42";
            if (channel < 45590) return "B43";
            if (channel < 46590) return "B44";
            if (channel < 46790) return "B45";
            if (channel < 54540) return "B46";
            if (channel < 55240) return "B47";
            if (channel < 56740) return "B48";
            if (channel < 58240) return "B49";
            if (channel < 59090) return "B50";
            if (channel < 59140) return "B51";
            if (channel < 60140) return "B52";
            if (channel < 60255) return "B53";
            if (channel < 65536) return "--";
            if (channel < 66436) return "B65";
            if (channel < 67336) return "B66";
            if (channel < 67536) return "B67";
            if (channel < 67836) return "B68";
            if (channel < 68336) return "B69";
            if (channel < 68586) return "B70";
            if (channel < 68936) return "B71";
            if (channel < 68986) return "B72";
            if (channel < 69036) return "B73";
            if (channel < 69466) return "B74";
            if (channel < 70316) return "B75";
            if (channel < 70366) return "B76";
            if (channel < 70546) return "B85";
            if (channel < 70596) return "B87";
            if (channel < 70646) return "B88";

            return "--";
        }

        public static bool Check(string AtPort, out SerialPort? serialPort)
        {
            try
            {
                var serial = new SerialPort(AtPort);
                serialPort = serial;
                serial.Open();
                if (serial.IsOpen)
                {
                    serial.Close(); 
                    serial.Dispose(); 
                    return true; 
                }
                else
                { 
                    serial.Close(); 
                    serial.Dispose(); 
                    return false; 
                }
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
            if(!mtsm.Contains("ERROR"))
            {
                IsGL860 = true;
            }
        }

        #endregion
    }
}
