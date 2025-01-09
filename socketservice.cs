using System;
using System.Net.Sockets;
using System.Text;

namespace RunServer
{
    public class SocketHandler : IDisposable
    {
        private readonly MySqlService _mySqlService;

        public SocketHandler(MySqlService mySqlService)
        {
            _mySqlService = mySqlService;
        }

        public void HandleClient(TcpClient client)
        {
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (receivedData.Trim() == "data")
                {
                    string jsonResponse = _mySqlService.GetAllItemsAsJson("YourTableName");
                    byte[] responseData = Encoding.UTF8.GetBytes(jsonResponse);
                    stream.Write(responseData, 0, responseData.Length);
                }
            }
        }

        public void Dispose()
        {
            _mySqlService.Dispose();
        }
    }
}
