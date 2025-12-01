using System.Management;

namespace FibocomMonitor
{
    public static class ComPortHelper
    {
        private static readonly List<string> list = new List<string>();

        public static List<string> GetComPortList()
        {
            using (var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_SerialPort"))
            {
                foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    if (list.Count > 0) { list.Clear(); }
                    list.Add($"{queryObj["Name"]?.ToString()}");
                }
            }

            return list;
        }
    }
}
