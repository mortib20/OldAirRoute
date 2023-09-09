using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mock
{
    internal static class MockListener
    {
        private static TcpListener _listener;
        public static IPAddress Address { get; set; }
        public static byte[] lastReceivedBytes { get; private set; }
        public static string lastReceivedString { get; private set; }
        public static int Port { get; set; }

        public static Thread NewThread => new(ListenerThread);

        private static void ListenerThread(object? param)
        {
            if (param is not TimeSpan timeout)
            {
                Debug.WriteLine("MockListener: Param was not TimeSpan");
                return;
            }

            Debug.WriteLine("MockListener: Initialized");
            _listener = new(Address, Port);

            Debug.WriteLine($"MockListener: Waiting {timeout:dd\\.hh\\:mm\\:ss}");
            Thread.Sleep(timeout);

            Debug.WriteLine($"MockListener: Starting on {Address}:{Port}");
            _listener.Start();

            using var client = _listener.AcceptTcpClient();
            using var stream = client.GetStream();
            Debug.WriteLine("MockListener: Client connected");

            var buffer = new byte[2048];
            var length = stream.Read(buffer);

            if (length == 0)
            {
                Debug.WriteLine("MockListener: Received Length was ZERO");
            }

            lastReceivedBytes = buffer;
            lastReceivedString = Encoding.UTF8.GetString(buffer.Take(length).ToArray());

            Debug.WriteLine($"MockListener: Received Length: {length}");
            Debug.WriteLine($"MockListener: Data: {lastReceivedString}");

            Debug.WriteLine("MockListener: Close client connection");
            client.Close();

            Debug.WriteLine("MockListener: Shutdown");
            _listener.Server.Close();
            _listener.Stop();
        }
    }
}
