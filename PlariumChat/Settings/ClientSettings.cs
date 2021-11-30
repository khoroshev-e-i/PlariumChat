using System.Net;

namespace PlariumChat.Settings
{
    public class ClientSettings
    {
        public byte[] GatewayHost { get; set; }
        public int Port { get; set; }
        public int BufferSize { get; set; }
    }
}
