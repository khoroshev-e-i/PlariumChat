using System.Net;

namespace Gateway.Settings
{
    public class GatewaySettings
    {
        public byte[] Host { get; set; }
        public int Port { get; set; }
        public int BufferSize { get; set; }
        public int HistorySize { get; set; }
    }
}
