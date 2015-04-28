using System;
using System.Net;
using System.Net.Sockets;

namespace STProxyServer
{
    public sealed class HttpListener : Listener
    {
        public HttpListener(int Port) : this(IPAddress.Any, Port) { }
        public HttpListener(IPAddress Address, int Port) : base(Port, Address) { }
        public override void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket NewSocket = ListenSocket.EndAccept(ar);
                if (NewSocket != null)
                {
                    HttpClient NewClient = new HttpClient(NewSocket, new DestroyDelegate(this.RemoveClient));
                    AddClient(NewClient);
                    NewClient.StartHandshake();
                }
            }
            catch { }
            try
            {
                //Restart Listening
                ListenSocket.BeginAccept(new AsyncCallback(this.OnAccept), ListenSocket);
            }
            catch
            {
                Dispose();
            }
        }
        public override string ToString()
        {
            return "HTTP service on " + Address.ToString() + ":" + Port.ToString();
        }
        public override string ConstructString
        {
            get
            {
                return "host:" + Address.ToString() + ";int:" + Port.ToString();
            }
        }
    }
}
