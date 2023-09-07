using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace AirRoute.ADSB
{
    public class TcpOutput : IDisposable
    {
        private readonly ILogger _logger;
        private TcpClient _client;

        public string Hostname { get; }
        public int Port { get; }
        public bool Reconnecting { get; private set; } = false;

        public TcpOutput(ILoggerFactory loggerFactory, string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
            _logger = loggerFactory.CreateLogger($"{GetType()} {this}");
            _client = new();
        }

        public async Task ConnectAsync(CancellationToken stoppingToken = default)
        {
            _logger.LogInformation("Connecting...");

            try
            {
                await _client.ConnectAsync(Hostname, Port, stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex)
            {
                _logger.LogError("Connection failed");
                await ReconnectAsync(stoppingToken);
                return;
            }

            _logger.LogInformation("Connected");
        }

        public void Disconnect()
        {
            _logger.LogInformation("Disconnected");
            _client.Close();
        }

        public async Task WriteAsync(byte[] buffer, int length, CancellationToken stoppingToken = default)
        {
            try
            {
                if (!_client.Connected)
                {
                    await ReconnectAsync(stoppingToken);
                }

                if (Reconnecting)
                {
                    return;
                }

                var stream = _client.GetStream();
                await stream.WriteAsync(buffer, 0, length, stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex)
            {
                await ReconnectAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                await ReconnectAsync(stoppingToken);
            }
        }

        private async Task ReconnectAsync(CancellationToken stoppingToken = default)
        {
            if (Reconnecting)
            {
                return;
            }

            Reconnecting = true;
            TimeSpan timeout = TimeSpan.FromSeconds(1);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (timeout <= TimeSpan.FromHours(1))
                {
                    timeout += TimeSpan.FromSeconds(10);
                }

                _logger.LogInformation("Reconnecting...");

                try
                {
                    _client = new();
                    await _client.ConnectAsync(Hostname, Port, stoppingToken);
                    break;
                }
                catch (OperationCanceledException) { }
                catch (SocketException ex)
                {
                    _logger.LogError("Connection failed");
                    await Task.Delay(timeout, stoppingToken);
                }
            }

            _logger.LogInformation("Connected");
            Reconnecting = false;
        }

        public void Dispose()
        {
            _client.Close();
        }

        public override string? ToString() => $"{Hostname}:{Port}";
    }
}
