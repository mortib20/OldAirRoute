using ADSB;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Tests.Mock;

namespace Tests
{
    [TestClass]
    public class TcoOutputTest
    {
        private int randomPort;
        private TcpOutput output;
        private Thread listenerThread;

        [TestInitialize]
        public void Initialize()
        {
            randomPort = MockRandom.RandomPort;
            MockListener.Address = System.Net.IPAddress.Any;
            MockListener.Port = randomPort;
            listenerThread = MockListener.NewThread;

            output = new(MockLogger.Factory, "localhost", randomPort);

            Assert.AreEqual(TcpOutputStatus.Disconnected, output.Status);
            Assert.AreEqual("localhost", output.Hostname);
            Assert.AreEqual(randomPort, output.Port);
            Assert.AreEqual($"localhost:{randomPort}", output.ToString());
        }

        [TestMethod]
        public void ConnectTest()
        {
            listenerThread.Start(TimeSpan.Zero);

            output.ConnectAsync().Wait();
            Assert.AreEqual(TcpOutputStatus.Connected, output.Status);

            output.Disconnect();

            Assert.AreEqual(TcpOutputStatus.Disconnected, output.Status);

            listenerThread.Join();
        }

        [TestMethod]
        public void WriteTest()
        {
            var buffer = Encoding.UTF8.GetBytes("Hallo");

            listenerThread.Start(TimeSpan.Zero);

            output.ConnectAsync().Wait();
            Assert.AreEqual(TcpOutputStatus.Connected, output.Status);

            output.WriteAsync(buffer, buffer.Length).Wait();

            listenerThread.Join();

            Assert.AreEqual("Hallo", MockListener.lastReceivedString);
        }

        [TestMethod]
        public void ReconnectTest()
        {
            var buffer = Encoding.UTF8.GetBytes("Hallo");

            listenerThread.Start(TimeSpan.FromSeconds(20));

            output.ConnectAsync().Wait();
            Assert.AreEqual(TcpOutputStatus.Connected, output.Status);

            output.WriteAsync(buffer, buffer.Length).Wait();

            listenerThread.Join();

            Assert.AreEqual("Hallo", MockListener.lastReceivedString);
        }

        [TestMethod]
        public void MyTestMethod()
        {
            var buffer = Encoding.UTF8.GetBytes("Hallo");

            output.ConnectAsync().Wait();
            Assert.AreEqual(TcpOutputStatus.Connected, output.Status);

            output.WriteAsync(buffer, buffer.Length).Wait();

            Assert.AreEqual("Hallo", MockListener.lastReceivedString);
        }
    }
}
