using ADSB;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Tests.Mock;

namespace Tests
{
    [TestClass]
    public class TcpInputTest
    {
        private CancellationTokenSource tokenSource;
        private CancellationToken stoppingToken;
        private int randomPort { get; set; } = 3277;
        private TcpInput input;
        private string receivedData;

        [TestInitialize]
        public void Initializer()
        {
            tokenSource = new();
            stoppingToken = tokenSource.Token;
            randomPort = MockRandom.RandomPort;
            Debug.WriteLine($"randomPort = {randomPort}");

            input = new(MockLogger.Factory, IPAddress.Any, randomPort);

            Assert.AreEqual($"{IPAddress.Any}:{randomPort}", input.ToString());
            Assert.AreEqual(IPAddress.Any, input.Address);
            Assert.AreEqual(randomPort, input.Port);
            Assert.AreEqual(TcpInputState.Stopped, input.State);
        }


        [TestMethod]
        public void StartListeningTest()
        {
            input.Start();

            Assert.AreEqual(TcpInputState.Started, input.State);
        }

        [TestMethod]
        public void StopListeningTest()
        {
            input.Stop();

            Assert.AreEqual(TcpInputState.Stopped, input.State);
        }

        [TestMethod]
        public void HandleConnectionsTest()
        {
            Debug.WriteLine("Stopped Input");
            Assert.AreEqual(TcpInputState.Stopped, input.State);

            input.Start();
            Debug.WriteLine("Start Input");
            Assert.AreEqual(TcpInputState.Started, input.State);

            Debug.WriteLine("Create Simulation Thread");
            Thread t = new(SimulationClientThread);
            Debug.WriteLine("Start Simulation Thread");
            t.Start();

            Debug.WriteLine("Handle Clients");
            input.HandleConnections(HandleClient, stoppingToken).Wait();
            Assert.AreEqual(TcpInputState.Started, input.State);

            Debug.WriteLine("Received Data test");
            Assert.AreEqual("Hallo", receivedData);

            Debug.WriteLine("Wait for Simulation Thread to end");
            t.Join();

            Debug.WriteLine("Stopping Input again");
            input.Stop();
            Assert.AreEqual(TcpInputState.Stopped, input.State);
        }

        private void SimulationClientThread(object? obj)
        {
            SimulateClientConnection("localhost", randomPort);
        }

        private void SimulateClientConnection(string address, int port)
        {
            Thread.Sleep(2000);
            using TcpClient client = new();

            client.Connect(address, port);
            using var stream = client.GetStream();

            stream.Write(Encoding.UTF8.GetBytes("Hallo"));

            client.Close();
        }

        private async Task HandleClient(TcpClient client, NetworkStream stream, CancellationToken stoppingToken = default)
        {
            Debug.WriteLine("Handle Client");

            var buffer = new byte[2048];
            var length = await stream.ReadAsync(buffer, stoppingToken);

            if (length == 0)
            {
                Debug.WriteLine("Readed Length was ZERO");
                return;
            }

            receivedData = Encoding.UTF8.GetString(buffer.Take(length).ToArray());
            tokenSource.Cancel();
        }


    }
}