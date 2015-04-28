using System;
using System.Diagnostics;
using System.Collections;
using System.Net;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace STProxyServer
{
    public struct ListenEntry
    {
        public Listener listener;
        public Guid guid;
        public override bool Equals(object obj)
        {
            return ((ListenEntry)obj).guid.Equals(guid);
        }
    }
    class ProxyServer
    {
        [DllImport("wininet.dll")]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
        public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public const int INTERNET_OPTION_REFRESH = 37;
        static string CurrentServer;
        static int CurrentEnable;
        const string userRoot = "HKEY_CURRENT_USER";
        const string subkey = "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";
        const string keyName = userRoot + "\\" + subkey;

        static void setProxy(string proxyhost, int proxyEnabled)
        {
            if (proxyhost != null)
                Registry.SetValue(keyName, "ProxyServer", proxyhost);
            Registry.SetValue(keyName, "ProxyEnable", proxyEnabled);

            // These lines implement the Interface in the beginning of program 
            // They cause the OS to refresh the settings, causing IP to realy update
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                setProxy(CurrentServer, 0);
            }
            return false;
        }
        static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        static void Main(string[] args)
        {
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);

            CurrentServer = (string)Registry.GetValue(keyName, "ProxyServer", null);
            CurrentEnable = (int)Registry.GetValue(keyName, "ProxyEnable", 0);
            setProxy("127.0.0.1:80", 1);
            ProxyServer server = new ProxyServer();
            server.Start();
            setProxy(CurrentServer, 0);
        }

        public void Start()
        {
            Console.WriteLine("Start ProxySever");
            object[] param = new object[2];
            param[0] = IPAddress.Any;
            param[1] = 80;
            //Listener listenr = (Listener)Activator.CreateInstance(Type.GetType("HttpListener"), param);
            Listener listenr = new HttpListener(IPAddress.Any, 80);
            AddListener(listenr);
            listenr.Start();

            var url = "http://www.kh62.com";

            using (var process = new Process())
            {
                process.StartInfo.FileName = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
                process.StartInfo.Arguments = url + " --incognito";
                //process.StartInfo.Arguments = url;

                process.Start();
            }

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo cki = Console.ReadKey(true);
                    cki = Console.ReadKey(true);
                    if (cki.Key == ConsoleKey.Escape)
                        break;
                }
            }
            Stop();
            Console.WriteLine("Goodbye...");
        }

        public void AddListener(Listener newItem)
        {
            if (newItem == null)
                throw new ArgumentNullException();
            ListenEntry le = new ListenEntry();
            le.listener = newItem;
            le.guid = Guid.NewGuid();
            while (Listeners.Contains(le))
            {
                le.guid = Guid.NewGuid();
            }
            Listeners.Add(le);
            Console.WriteLine(newItem.ToString() + " started.");
        }
        public void Stop()
        {
            // Stop listening and clear the Listener list
            for (int i = 0; i < ListenerCount; i++)
            {
                Console.WriteLine(this[i].ToString() + " stopped.");
                this[i].Dispose();
            }
            Listeners.Clear();
        }
        internal virtual Listener this[int index]
        {
            get
            {
                return ((ListenEntry)Listeners[index]).listener;
            }
        }
        internal int ListenerCount
        {
            get
            {
                return Listeners.Count;
            }
        }
        protected ArrayList Listeners
        {
            get
            {
                return m_Listeners;
            }
        }
        private DateTime m_StartTime;
        private ArrayList m_Listeners = new ArrayList();
    }
}
