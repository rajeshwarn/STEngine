using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace STProxyServer
{
    public abstract class Listener : IDisposable
    {
        public Listener(int Port, IPAddress Address)
        {
            this.Port = Port;
            this.Address = Address;
        }
        ~Listener()
        {
            Dispose();
        }
        protected int Port
        {
            get
            {
                return m_Port;
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentException();
                m_Port = value;
                Restart();
            }
        }
        protected IPAddress Address
        {
            get
            {
                return m_Address;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                m_Address = value;
                Restart();
            }
        }
        protected Socket ListenSocket
        {
            get
            {
                return m_ListenSocket;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                m_ListenSocket = value;
            }
        }
        protected Dictionary<long, Client> Clients
        {
            get
            {
                return m_Clients;
            }
        }
        public bool IsDisposed
        {
            get
            {
                return m_IsDisposed;
            }
        }
        public void Start()
        {
            try
            {
                ListenSocket = new Socket(Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                ListenSocket.Bind(new IPEndPoint(Address, Port));
                ListenSocket.Listen(50);
                ListenSocket.BeginAccept(new AsyncCallback(this.OnAccept), ListenSocket);
            }
            catch
            {
                ListenSocket = null;
                throw new SocketException();
            }
        }
        protected void Restart()
        {
            //If we weren't listening, do nothing
            if (ListenSocket == null)
                return;
            ListenSocket.Close();
            Start();
        }
        public bool Listening
        {
            get
            {
                return ListenSocket != null;
            }
        }
        public void Dispose()
        {
            if (IsDisposed)
                return;
            while (Clients.Count > 0)
            {
                ((Client)Clients[0]).Dispose();
            }
            try
            {
                ListenSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            if (ListenSocket != null)
                ListenSocket.Close();
            m_IsDisposed = true;
        }
        protected void AddClient(Client client)
        {
            if (!Clients.ContainsKey(client.m_SN))
                Clients.Add(client.m_SN, client);
        }
        protected void RemoveClient(Client client)
        {
            Clients.Remove(client.m_SN);
        }
        public int GetClientCount()
        {
            return Clients.Count;
        }
        public Client GetClientAt(int Index)
        {
            if (Index < 0 || Index >= GetClientCount())
                return null;
            return (Client)Clients[Index];
        }

        public abstract void OnAccept(IAsyncResult ar);
        public override abstract string ToString();
        public abstract string ConstructString { get; }
        private int m_Port;
        private IPAddress m_Address;
        private Socket m_ListenSocket;
        private Dictionary<long, Client> m_Clients = new Dictionary<long, Client>();
        private bool m_IsDisposed = false;
    }
}
