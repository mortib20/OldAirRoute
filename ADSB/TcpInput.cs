using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace AirRoute.ADSB
{
    internal delegate Task TcpInputHandleClientAsync(TcpClient client, NetworkStream stream, CancellationToken stoppingToken = default);

    public enum TcpInputState
    {
        Stopped,
        Listening,
        Error
    }

    public class TcpInput : IDisposable
    {
        private readonly ILogger _logger;
        private readonly TcpListener _listener;

        public TcpInputState State { get; private set; }
        public IPAddress Address { get; }
        public int Port { get; }

        public TcpInput(ILoggerFactory loggerFactory, IPAddress address, int port)
        {
            State = TcpInputState.Stopped;
            Address = address;
            Port = port;
            _logger = loggerFactory.CreateLogger($"{GetType()} {this}");
            _listener = new(Address, Port);
        }

        /// <summary>
        /// Input starts listening
        /// </summary>
        public void Start()
        {
            try
            {
                _listener.Start();
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                State = TcpInputState.Error;
                throw new("Address or port already in use.");
            }

            _logger.LogInformation("Started listening");
            State = TcpInputState.Listening;
        }

        /// <summary>
        /// Input stops listening
        /// </summary>
        public void Stop()
        {
            _listener.Stop();
            _logger.LogInformation("Stopped listening");
            State = TcpInputState.Stopped;
        }

        /// <summary>
        /// Accepts new Clients and Handle them
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task HandleConnections(Func<TcpClient, NetworkStream, CancellationToken, Task> handleClient, CancellationToken stoppingToken = default)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    using var stream = client.GetStream();

                    _logger.LogInformation($"{client.Client.RemoteEndPoint} connected");
                    await handleClient(client, stream, stoppingToken);
                    _logger.LogInformation($"{client.Client.RemoteEndPoint} disconnected");
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _listener.Stop();
        }

        public override string ToString() => $"{Address}:{Port}";
    }
}
