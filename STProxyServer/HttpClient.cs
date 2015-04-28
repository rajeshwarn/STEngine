using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;

namespace STProxyServer
{
    public sealed class HttpClient : Client
    {
        public HttpClient(Socket ClientSocket, DestroyDelegate Destroyer) : base(ClientSocket, Destroyer) { }
        private StringDictionary HeaderFields
        {
            get
            {
                return m_HeaderFields;
            }
            set
            {
                m_HeaderFields = value;
            }
        }
        private string HttpVersion
        {
            get
            {
                return m_HttpVersion;
            }
            set
            {
                m_HttpVersion = value;
            }
        }
        private string HttpRequestType
        {
            get
            {
                return m_HttpRequestType;
            }
            set
            {
                m_HttpRequestType = value;
            }
        }
        public string RequestedPath
        {
            get
            {
                return m_RequestedPath;
            }
            set
            {
                m_RequestedPath = value;
            }
        }
        private string HttpQuery
        {
            get
            {
                return m_HttpQuery;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                m_HttpQuery = value;
            }
        }
        public override void StartHandshake()
        {
            try
            {
                ClientSocket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveQuery), ClientSocket);
            }
            catch
            {
                Dispose();
            }
        }
        private bool IsValidQuery(string Query)
        {
            int index = Query.IndexOf("\r\n\r\n");
            if (index == -1)
                return false;
            HeaderFields = ParseQuery(Query);
            if (HttpRequestType.ToUpper().Equals("POST"))
            {
                try
                {
                    int length = int.Parse((string)HeaderFields["Content-Length"]);
                    return Query.Length >= index + 6 + length;
                }
                catch
                {
                    SendBadRequest();
                    return true;
                }
            }
            else
            {
                return true;
            }
        }
        private void ProcessQuery(string Query)
        {
            HeaderFields = ParseQuery(Query);
            if (HeaderFields == null || !HeaderFields.ContainsKey("Host"))
            {
                SendBadRequest();
                return;
            }
            int Port;
            string Host;
            int Ret;
            if (HttpRequestType.ToUpper().Equals("CONNECT"))
            { //HTTPS
                Ret = RequestedPath.IndexOf(":");
                if (Ret >= 0)
                {
                    Host = RequestedPath.Substring(0, Ret);
                    if (RequestedPath.Length > Ret + 1)
                        Port = int.Parse(RequestedPath.Substring(Ret + 1));
                    else
                        Port = 443;
                }
                else
                {
                    Host = RequestedPath;
                    Port = 443;
                }
            }
            else
            { //Normal HTTP
                Ret = ((string)HeaderFields["Host"]).IndexOf(":");
                if (Ret > 0)
                {
                    Host = ((string)HeaderFields["Host"]).Substring(0, Ret);
                    Port = int.Parse(((string)HeaderFields["Host"]).Substring(Ret + 1));
                }
                else
                {
                    Host = (string)HeaderFields["Host"];
                    Port = 80;
                }
                if (HttpRequestType.ToUpper().Equals("POST"))
                {
                    int index = Query.IndexOf("\r\n\r\n");
                    m_HttpPost = Query.Substring(index + 4);
                }
            }
            try
            {
                IPEndPoint DestinationEndPoint = new IPEndPoint(Dns.Resolve(Host).AddressList[0], Port);
                DestinationSocket = new Socket(DestinationEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                if (HeaderFields.ContainsKey("Proxy-Connection") && HeaderFields["Proxy-Connection"].ToLower().Equals("keep-alive"))
                    DestinationSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
                DestinationSocket.BeginConnect(DestinationEndPoint, new AsyncCallback(this.OnConnected), DestinationSocket);
            }
            catch
            {
                SendBadRequest();
                return;
            }
        }
        private StringDictionary ParseQuery(string Query)
        {
            StringDictionary retdict = new StringDictionary();
            string[] Lines = Query.Replace("\r\n", "\n").Split('\n');
            int Cnt, Ret;
            //Extract requested URL
            if (Lines.Length > 0)
            {
                //Parse the Http Request Type
                Ret = Lines[0].IndexOf(' ');
                if (Ret > 0)
                {
                    HttpRequestType = Lines[0].Substring(0, Ret);
                    Lines[0] = Lines[0].Substring(Ret).Trim();
                }
                //Parse the Http Version and the Requested Path
                Ret = Lines[0].LastIndexOf(' ');
                if (Ret > 0)
                {
                    HttpVersion = Lines[0].Substring(Ret).Trim();
                    RequestedPath = Lines[0].Substring(0, Ret);
                }
                else
                {
                    RequestedPath = Lines[0];
                }
                //Remove http:// if present
                if (RequestedPath.Length >= 7 && RequestedPath.Substring(0, 7).ToLower().Equals("http://"))
                {
                    Ret = RequestedPath.IndexOf('/', 7);
                    if (Ret == -1)
                        RequestedPath = "/";
                    else
                        RequestedPath = RequestedPath.Substring(Ret);
                }
            }
            for (Cnt = 1; Cnt < Lines.Length; Cnt++)
            {
                Ret = Lines[Cnt].IndexOf(":");
                if (Ret > 0 && Ret < Lines[Cnt].Length - 1)
                {
                    try
                    {
                        retdict.Add(Lines[Cnt].Substring(0, Ret), Lines[Cnt].Substring(Ret + 1).Trim());
                    }
                    catch { }
                }
            }
            return retdict;
        }
        private void SendBadRequest()
        {
            string brs = "HTTP/1.1 400 Bad Request\r\nConnection: close\r\nContent-Type: text/html\r\n\r\n<html><head><title>400 Bad Request</title></head><body><div align=\"center\"><table border=\"0\" cellspacing=\"3\" cellpadding=\"3\" bgcolor=\"#C0C0C0\"><tr><td><table border=\"0\" width=\"500\" cellspacing=\"3\" cellpadding=\"3\"><tr><td bgcolor=\"#B2B2B2\"><p align=\"center\"><strong><font size=\"2\" face=\"Verdana\">400 Bad Request</font></strong></p></td></tr><tr><td bgcolor=\"#D1D1D1\"><font size=\"2\" face=\"Verdana\"> The proxy server could not understand the HTTP request!<br><br> Please contact your network administrator about this problem.</font></td></tr></table></center></td></tr></table></div></body></html>";
            try
            {
                ClientSocket.BeginSend(Encoding.ASCII.GetBytes(brs), 0, brs.Length, SocketFlags.None, new AsyncCallback(this.OnErrorSent), ClientSocket);
            }
            catch
            {
                Dispose();
            }
        }
        private string RebuildQuery()
        {
            string ret = HttpRequestType + " " + RequestedPath + " " + HttpVersion + "\r\n";
            if (HeaderFields != null)
            {
                foreach (string sc in HeaderFields.Keys)
                {
                    if (sc.Length < 6 || !sc.Substring(0, 6).Equals("proxy-"))
                        ret += System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sc) + ": " + (string)HeaderFields[sc] + "\r\n";
                }
                ret += "\r\n";
                if (m_HttpPost != null)
                    ret += m_HttpPost;
            }
            return ret;
        }
        public override string ToString()
        {
            return ToString(false);
        }
        public string ToString(bool WithUrl)
        {
            string Ret;
            try
            {
                if (DestinationSocket == null || DestinationSocket.RemoteEndPoint == null)
                    Ret = "Incoming HTTP connection from " + ((IPEndPoint)ClientSocket.RemoteEndPoint).Address.ToString();
                else
                    Ret = "HTTP connection from " + ((IPEndPoint)ClientSocket.RemoteEndPoint).Address.ToString() + " to " + ((IPEndPoint)DestinationSocket.RemoteEndPoint).Address.ToString() + " on port " + ((IPEndPoint)DestinationSocket.RemoteEndPoint).Port.ToString();
                if (HeaderFields != null && HeaderFields.ContainsKey("Host") && RequestedPath != null)
                    Ret += "\r\n" + " requested URL: http://" + HeaderFields["Host"] + RequestedPath;
            }
            catch
            {
                Ret = "HTTP Connection";
            }
            return Ret;
        }
        private void OnReceiveQuery(IAsyncResult ar)
        {
            int Ret;
            try
            {
                Ret = ClientSocket.EndReceive(ar);
            }
            catch
            {
                Ret = -1;
            }
            if (Ret <= 0)
            { //Connection is dead :(
                Dispose();
                return;
            }
            HttpQuery += Encoding.ASCII.GetString(Buffer, 0, Ret);
            //if received data is valid HTTP request...
            if (IsValidQuery(HttpQuery))
            {
                if(HttpQuery.StartsWith("GET http:"))
                {
                    HttpQuery = HttpQuery.Replace("GET http", "GET https");
                }
                ProcessQuery(HttpQuery);
                //else, keep listening
            }
            else
            {
                try
                {
                    ClientSocket.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveQuery), ClientSocket);
                }
                catch
                {
                    Dispose();
                }
            }
        }
        private void OnErrorSent(IAsyncResult ar)
        {
            try
            {
                ClientSocket.EndSend(ar);
            }
            catch { }
            Dispose();
        }
        private void OnConnected(IAsyncResult ar)
        {
            try
            {
                DestinationSocket.EndConnect(ar);
                string rq;
                if (HttpRequestType.ToUpper().Equals("CONNECT"))
                { //HTTPS
                    rq = HttpVersion + " 200 Connection established\r\nProxy-Agent: Mentalis Proxy Server\r\n\r\n";
                    ClientSocket.BeginSend(Encoding.ASCII.GetBytes(rq), 0, rq.Length, SocketFlags.None, new AsyncCallback(this.OnOkSent), ClientSocket);
                }
                else
                { //Normal HTTP
                    rq = RebuildQuery();
                    DestinationSocket.BeginSend(Encoding.ASCII.GetBytes(rq), 0, rq.Length, SocketFlags.None, new AsyncCallback(this.OnQuerySent), DestinationSocket);
                }
            }
            catch
            {
                Dispose();
            }
        }
        private void OnQuerySent(IAsyncResult ar)
        {
            try
            {
                if (DestinationSocket.EndSend(ar) == -1)
                {
                    Dispose();
                    return;
                }
                StartRelay();
            }
            catch
            {
                Dispose();
            }
        }
        private void OnOkSent(IAsyncResult ar)
        {
            try
            {
                if (ClientSocket.EndSend(ar) == -1)
                {
                    Dispose();
                    return;
                }
                StartRelay();
            }
            catch
            {
                Dispose();
            }
        }
        private string m_HttpQuery = "";
        private string m_RequestedPath = null;
        private StringDictionary m_HeaderFields = null;
        private string m_HttpVersion = "";
        private string m_HttpRequestType = "";
        private string m_HttpPost = null;
    }
}
