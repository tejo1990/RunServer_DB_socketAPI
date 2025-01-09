using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;
using System.Net.Sockets;
using LitJson;

namespace RunServer
{
    public class mClientInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Zone { get; set; }
        public string Position { get; set; }
        public double Angle { get; set; }
        public double Scale { get; set; }
    }

    public interface mIStateObject
    {
        Socket RefSocket { get; }
        int ID { get; }
        string Name { get; }
        int BufferSize { get; }
        byte[] Buffer { get; }
        string Text { get; }

        void Reset();
        void Append(string sText);
    }

    public class mStateObject : mIStateObject
    {
        private readonly Socket m_kSocket;
        private readonly int m_iID;
        private readonly string m_sName;

        private const int m_iBufferSize = 1024 * 1024;
        private readonly byte[] m_abBuffer = new byte[m_iBufferSize];

        private StringBuilder m_kStringBuilder;

        public mStateObject(Socket kSocket)
        {
            m_kSocket = kSocket;
            m_iID = kSocket.RemoteEndPoint.ToString().GetHashCode();
            m_sName = kSocket.RemoteEndPoint.ToString();
            Reset();
        }

        public mStateObject(Socket kSocket, int iID, string sName)
        {
            m_kSocket = kSocket;
            m_iID = iID;
            m_sName = sName;
            Reset();
        }

        public Socket RefSocket
        {
            get { return m_kSocket; }
        }

        public int ID
        {
            get { return m_iID; }
        }

        public string Name
        {
            get { return m_sName; }
        }

        public int BufferSize
        {
            get { return m_iBufferSize; }
        }

        public byte[] Buffer
        {
            get { return m_abBuffer; }
        }

        public string Text
        {
            get { return m_kStringBuilder.ToString(); }
        }

        public void Reset()
        {
            m_kStringBuilder = new StringBuilder();
        }

        public void Append(string sText)
        {
            m_kStringBuilder.Append(sText);
        }
    }

    public class Net
    {
        private static Net m_kInstance = null;

        private bool m_bEnableNetwork = true;
        private bool m_bSocketReady = false;

        private string m_sHost = "127.0.0.1";
        private int m_iPort = 3060;
        private Socket m_kSocket = null;
        private mStateObject m_kStateObject = null;

        private mClientInfo m_kClientInfo = new mClientInfo();

        public List<string> m_Messages = new List<string>();
        public static object m_kLockNetMessages = new object();


        public bool isTryConnect = true;

        public string m_ZONE_CODE;
        public string m_SZONE_CODE;
        public string m_DEF_LOC_CODE;

        public Net()
        {
            m_kInstance = this;
        }

        public static Net Instance
        {
            get
            {
                return m_kInstance;
            }
        }

        public bool IsConnected()
        {
            return m_bSocketReady;
        }

        private static bool IsSocketConnected(Socket s)
        {
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);
        }

        private static bool IsWriteSocketConnected(Socket s)
        {
            try
            {
                return (s.Poll(1000, SelectMode.SelectWrite) && s.Connected);
            }
            catch (NullReferenceException e)
            {
                Console.WriteLine("--------------------------------------------- ");
                Console.WriteLine(" IsWriteSocketConnected exception : " + e);
                Console.WriteLine("--------------------------------------------- ");
                return false;
            }
        }

        public void Initialize(string name, string type)
        {
            m_kInstance = this;
            if (m_bEnableNetwork)
            {
                m_kClientInfo.Name = name;
                m_kClientInfo.Type = type;
                m_kClientInfo.Position = "";
                m_kClientInfo.Scale = 0.0f;
                m_kClientInfo.Zone = "";
                m_kClientInfo.Angle = 0.0f;
            }
        }

        public void SetNetAddr(string ip, int port)
        {
            m_sHost = ip;
            m_iPort = port;
        }

        public void TryConnect_Tick(object sender, EventArgs s)
        {
            if (isTryConnect) OpenSocket(m_sHost, m_iPort);
        }

        private void ReStart()
        {
            if (m_bEnableNetwork) OpenSocket(m_sHost, m_iPort);
        }

        public void Update()
        {
            if (isTryConnect) OpenSocket(m_sHost, m_iPort);
            ProcessMessages();
        }

        private void OnDestroy()
        {
            Disconnect(true);
        }

        private void SetServerInfo(XmlNode kNode)
        {
            foreach (XmlNode kChildNode in kNode.ChildNodes)
            {
                try
                {
                    if ("Host" == kChildNode.Name)
                    {
                        m_sHost = kChildNode.InnerText;
                    }
                    else if ("Port" == kChildNode.Name)
                    {
                        m_iPort = Convert.ToInt32(kChildNode.InnerText);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void OpenSocket(string host, int port)
        {
            try
            {
                m_kSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                m_kSocket.BeginConnect(new IPEndPoint(IPAddress.Parse(host), port), new AsyncCallback(OnConnect), null);
                isTryConnect = false;
                // Console.WriteLine("host:" + host + " port:" + port.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                m_bSocketReady = false;
                isTryConnect = true;
            }
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {
                m_kSocket.EndConnect(ar);

                m_kStateObject = new mStateObject(m_kSocket);
                m_kSocket.BeginReceive(m_kStateObject.Buffer, 0, m_kStateObject.BufferSize, SocketFlags.None, new AsyncCallback(OnReceive), m_kStateObject);
                m_bSocketReady = true;
                isTryConnect = false;

                var LocalePacket = new { IF_CODE = "Device", Client = m_kClientInfo.Name, Type = m_kClientInfo.Type };
                WriteSocket(JsonMapper.ToJson(LocalePacket) + "<EOP>");

                Console.WriteLine("<SYSTEM> Connected");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                m_bSocketReady = false;
                isTryConnect = true;
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            mStateObject kStateObject = (mStateObject)ar.AsyncState;
            Socket kSocket = kStateObject.RefSocket;

            if (!kSocket.Connected)
            {
                isTryConnect = true;
                return;
            }

            try
            {
                int iReadBytes = kSocket.EndReceive(ar);
                if (iReadBytes > 0)
                {
                    kStateObject.Append(Encoding.UTF8.GetString(kStateObject.Buffer, 0, iReadBytes));
                    string sDataPacket = kStateObject.Text;
                    kStateObject.Reset();

                    string[] datas = sDataPacket.Split(new string[] { "<EOP>" }, StringSplitOptions.None);
                    for (int i = 0; i < datas.Length - 1; ++i)
                    {
                        AddMessage(datas[i]);
                    }
                    kStateObject.Append(datas[datas.Length - 1]);

                    kSocket.BeginReceive(kStateObject.Buffer, 0, kStateObject.BufferSize, 0, new AsyncCallback(OnReceive), kStateObject);
                }
                else
                {
                    Disconnect(false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void Disconnect(bool bShutdown)
        {
            if (m_bSocketReady)
            {
                if (bShutdown)
                {
                    m_kSocket.Shutdown(SocketShutdown.Both);
                }
                else
                {
                    m_kSocket.Disconnect(false);
                }

                if (m_kSocket != null)
                {
                    m_kSocket.Close();
                    m_kSocket = null;
                }

                m_bSocketReady = false;
                isTryConnect = true;
            }
        }

        public void Close()
        {
            Disconnect(true);
        }

        public void WriteSocket(string sMessage)
        {
            if (m_bSocketReady)
            {
                try
                {
                    m_kSocket.Send(Encoding.UTF8.GetBytes(sMessage));
                }
                catch
                {
                    Console.WriteLine("Fail to Send...");
                }
            }
        }

        public void Log(string sMessage)
        {
            var sPacket = new { IF_CODE = "LOG", SENTENCE = sMessage };
            WriteSocket(JsonMapper.ToJson(sPacket) + "<EOP>");
        }

        private void AddMessage(string msg)
        {
            lock (m_kLockNetMessages)
            {
                m_Messages.Add(msg);
            }
        }

        private void ProcessMessages()
        {
            lock (m_kLockNetMessages)
            {
                foreach (string msg in m_Messages)
                {
                    ParseMessage(msg);
                }
                m_Messages.Clear();
            }
        }

        private void ParseMessage(string msg)
        {
            //JsonData kDataPacket = JsonMapper.ToObject(msg);
            //string sIF_CODE = (string)kDataPacket["IF_CODE"];


            //if (sIF_CODE.Equals("Test"))
            //{
            //}
        }

        public void SendMsg(string code, string msg)
        {
            if (!m_bSocketReady) return;
            if (m_kSocket == null)
            {
                return;
            }
            else
                if (!IsWriteSocketConnected(m_kSocket))
            {
                Disconnect(true);
                return;
            }
            var Packet = new { IF_CODE = code, MSG = msg };
            WriteSocket(JsonMapper.ToJson(Packet) + "<EOP>");
        }

        public void SendMsgAnim(string code, string name, string msg)
        {
            if (!m_bSocketReady) return;
            if (m_kSocket == null)
            {
                return;
            }
            else
                if (!IsWriteSocketConnected(m_kSocket))
            {
                Disconnect(true);
                return;
            }
            var Packet = new { IF_CODE = code, NAME = name, MOTION = msg };
            WriteSocket(JsonMapper.ToJson(Packet) + "<EOP>");
        }

        public void SendMsgLook(string code, string name, string msg)
        {
            if (!m_bSocketReady) return;
            if (m_kSocket == null)
            {
                return;
            }
            else
                if (!IsWriteSocketConnected(m_kSocket))
            {
                Disconnect(true);
                return;
            }
            var Packet = new { IF_CODE = code, NAME = name, DIR = msg };
            WriteSocket(JsonMapper.ToJson(Packet) + "<EOP>");
        }

        public void SendMsgKinectHead(string code, string option, string x, string y, string z)
        {
            if (!m_bSocketReady) return;
            if (m_kSocket == null)
            {
                return;
            }
            else
                if (!IsWriteSocketConnected(m_kSocket))
            {
                Disconnect(true);
                return;
            }
            var Packet = new { IF_CODE = code, OPTION = option, X = x, Y = y, Z = z };
            WriteSocket(JsonMapper.ToJson(Packet) + "<EOP>");
        }
    }
}
