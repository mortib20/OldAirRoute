using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace AirRoute.ADSB
{
    public enum TcpOutputStatus
    {
        Disconnected,
        Connected,
        Connecting,
        Stopped
    }

    public class TcpOutput
    {
        private readonly int MaxConnectionTries = 3;
        private readonly ILogger _logger;
        private TcpClient _client;

        public TcpOutput(ILoggerFactory loggerFactory, string hostname, int port)
        {
            _client = new();
            Hostname = hostname;
            Port = port;
            Status = TcpOutputStatus.Disconnected;
            Error = SocketError.Success;
            _logger = loggerFactory.CreateLogger($"{nameof(TcpOutput)} {this}");
        }

        /// <summary>
        /// Public
        /// </summary>
        public string Hostname { get; init; }
        public int Port { get; init; }
        public TcpOutputStatus Status { get; private set; }
        public SocketError Error { get; private set; }

        /// <summary>
        /// Connection Status
        /// </summary>
        public bool HasError => Error != SocketError.Success;
        public bool Connected => Status == TcpOutputStatus.Connected;
        public bool Connecting => Status == TcpOutputStatus.Connecting;
        public bool Stopped => Status == TcpOutputStatus.Stopped;

        /// <summary>
        /// Hostname and Port combination
        /// </summary>
        /// <returns>Hostname:Port</returns>
        public override string ToString() => $"{Hostname}:{Port}";

        /// <summary>
        /// Starts a connection
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task<bool> Start(CancellationToken stoppingToken = default)
        {
            if (Connecting || Connected)
            {
                _logger.LogInformation("Already connected");
                return true;
            }

            _logger.LogInformation("Starting");

            int connectionTries = 0;
            while (connectionTries <= MaxConnectionTries)
            {
                connectionTries++;
                _client = new();

                try
                {
                    _logger.LogInformation($"Connection try {connectionTries}.");
                    Status = TcpOutputStatus.Connecting;
                    await _client.ConnectAsync(Hostname, Port, stoppingToken);

                    Error = SocketError.Success;
                    Status = TcpOutputStatus.Connected;
                    _logger.LogInformation("Connected");
                    return true;
                }
                catch (OperationCanceledException) { }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.HostNotFound)
                    {
                        _logger.LogError($"{Error}");
                        Error = SocketError.HostNotFound;
                        Status = TcpOutputStatus.Stopped;
                        return false;
                    }

                    Error = ex.SocketErrorCode;
                    Status = TcpOutputStatus.Disconnected;
                    _logger.LogError($"{Error}");
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }
            }

            return false;
        }

        /// <summary>
        /// Disconnects output
        /// </summary>
        public void Disconnect()
        {
            if (Stopped)
            {
                _logger.LogInformation("Already stopped");
                return;
            }

            _client.Close();
            _logger.LogInformation("Disconnected");
            Status = TcpOutputStatus.Disconnected;
        }

        /// <summary>
        /// Disconnects and stops output
        /// </summary>
        public void Stop()
        {
            Disconnect();   

            Error = SocketError.Success;
            Status = TcpOutputStatus.Stopped;
            _logger.LogInformation("Stopped");
        }

        /// <summary>
        /// Writes data to remote
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task WriteAsync(byte[] buffer, int length, CancellationToken stoppingToken = default)
        {
            if(Stopped)
            {
                return;
            }

            if (!Connected)
            {
                await Start(stoppingToken);
                return;
            }

            try
            {
                var stream = _client.GetStream();
                await stream.WriteAsync(buffer.AsMemory(0, length), stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch(IOException ex)
            {
                _logger.LogError(ex.Message);
                Error = SocketError.IOPending;
                Stop();
                await Task.Delay(10000, stoppingToken);
                await Start(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                Status = TcpOutputStatus.Disconnected;
            }
        }
    }
}
