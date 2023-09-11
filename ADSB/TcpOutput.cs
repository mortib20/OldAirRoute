using Microsoft.Extensions.Logging;
using System.Diagnostics;
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

    public enum TcpOutputError
    {
        NoError,
        UnknownError,
        HostNotFound,
        Timeout,
        ConnectionRefused,
        ConnectionReset,
        HostUnreachable,
        NetworkUnreachable,
        InvalidArgument,
        AccessDenied,
        IOError
    }

    public class TcpOutput : IDisposable
    {
        private readonly int MAX_CONNECTION_TRIES = 3;
        private readonly TimeSpan INITIAL_TIMEOUT = TimeSpan.Zero;
        private readonly ILogger _logger;
        private TcpClient _client;
        private TcpOutputStatus _status = TcpOutputStatus.Disconnected;
        private TcpOutputError _error = TcpOutputError.NoError;
        private bool _disposed;

        public TcpOutputStatus Status
        {
            get => _status;
            private set
            {
                _status = value;

                switch (value)
                {
                    case TcpOutputStatus.Disconnected:
                        StatusMessage = "Disconnected";
                        break;
                    case TcpOutputStatus.Connected:
                        StatusMessage = "Connected";
                        break;
                    case TcpOutputStatus.Connecting:
                        StatusMessage = "Connecting";
                        break;
                    case TcpOutputStatus.Stopped:
                        StatusMessage = "Manual Stopped";
                        break;
                }

                _logger.LogInformation(StatusMessage);
            }
        }
        public TcpOutputError Error
        {
            get => _error;
            private set
            {
                _error = value;

                switch (value)
                {
                    case TcpOutputError.NoError:
                        ErrorMessage = "No Error";
                        break;
                    case TcpOutputError.UnknownError:
                        ErrorMessage = "Unknown Error";
                        break;
                    case TcpOutputError.HostNotFound:
                        ErrorMessage = "Host not found";
                        break;
                    case TcpOutputError.Timeout:
                        ErrorMessage = "Connection timed out";
                        break;
                    case TcpOutputError.ConnectionRefused:
                        ErrorMessage = "Connection refused";
                        break;
                    case TcpOutputError.ConnectionReset:
                        ErrorMessage = "Connection reset";
                        break;
                    case TcpOutputError.HostUnreachable:
                        ErrorMessage = "Host is unreachable";
                        break;
                    case TcpOutputError.NetworkUnreachable:
                        ErrorMessage = "Network is unreachable";
                        break;
                    case TcpOutputError.InvalidArgument:
                        ErrorMessage = "Invalid argument used for socket";
                        break;
                    case TcpOutputError.AccessDenied:
                        ErrorMessage = "Access Denied";
                        break;
                    case TcpOutputError.IOError:
                        ErrorMessage = "IO Error";
                        break;
                }

                if (Error == TcpOutputError.NoError)
                {
                    return;
                }

                _logger.LogError(ErrorMessage);
            }
        }
        public bool HasError => Error != TcpOutputError.NoError;
        public bool IsStopped => Status == TcpOutputStatus.Stopped;
        public bool IsDisconnected => Status != TcpOutputStatus.Disconnected;

        public string StatusMessage { get; private set; } = "";

        public string ErrorMessage { get; private set; } = "";

        public string Hostname { get; }
        public int Port { get; }

        public TcpOutput(ILoggerFactory loggerFactory, string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
            _client = new();
            _logger = loggerFactory.CreateLogger($"{GetType()} {this}");
            _status = TcpOutputStatus.Disconnected;
            _error = TcpOutputError.NoError;
        }

        /// <summary>
        /// Connects asynchronous to remote endpoint
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns><see cref="true"/> when connected, <see cref="false"/> when connection failed</returns>
        public async Task<bool> ConnectAsync(CancellationToken stoppingToken = default)
        {
            if (Status == TcpOutputStatus.Connecting || Status == TcpOutputStatus.Connected)
            {
                return true;
            }

            _client = new();

            int connectionTries = 0;
            TimeSpan timeout = INITIAL_TIMEOUT;
            while (connectionTries < MAX_CONNECTION_TRIES)
            {
                connectionTries++;
                timeout += TimeSpan.FromSeconds(5);

                try
                {
                    Status = TcpOutputStatus.Connecting;

                    await _client.ConnectAsync(Hostname, Port);

                    Status = TcpOutputStatus.Connected;
                    return true;
                }
                catch (OperationCanceledException) { }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
                {
                    Error = TcpOutputError.HostNotFound;
                    Status = TcpOutputStatus.Disconnected;
                    return false;
                }
                catch (SocketException ex)
                {
                    HandleSocketExceptionError(ex);

                    await Task.Delay(timeout, stoppingToken);
                }
            }

            _logger.LogInformation($"Failed to connect after {connectionTries}");
            Status = TcpOutputStatus.Disconnected;
            return false;
        }

        /// <summary>
        /// Handles the <see cref="SocketException"/>.SocketErrorCode
        /// </summary>
        /// <param name="ex">Exception to handle</param>
        private void HandleSocketExceptionError(SocketException ex)
        {
            switch (ex.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    Error = TcpOutputError.ConnectionRefused;
                    break;
                case SocketError.ConnectionReset:
                    Error = TcpOutputError.ConnectionReset;
                    break;
                case SocketError.TimedOut:
                    Error = TcpOutputError.Timeout;
                    break;
                case SocketError.HostUnreachable:
                    Error = TcpOutputError.HostUnreachable;
                    break;
                case SocketError.NetworkUnreachable:
                    Error = TcpOutputError.NetworkUnreachable;
                    break;
                case SocketError.InvalidArgument:
                    Error = TcpOutputError.InvalidArgument;
                    break;
                case SocketError.AccessDenied:
                    Error = TcpOutputError.AccessDenied;
                    break;
            }
        }

        /// <summary>
        /// Starts the output
        /// </summary>
        /// <param name="stoppingToken"></param>
        public void Start(CancellationToken stoppingToken = default)
        {
            if (Status == TcpOutputStatus.Connected || Status == TcpOutputStatus.Connecting)
            {
                return;
            }

            Status = TcpOutputStatus.Disconnected;
            Error = TcpOutputError.NoError;
            _ = ConnectAsync(stoppingToken);
        }

        /// <summary>
        /// Stops the output
        /// </summary>
        public void Stop()
        {
            if (Status == TcpOutputStatus.Stopped)
            {
                return;
            }

            Status = TcpOutputStatus.Stopped;
            Error = TcpOutputError.NoError;
            _client.Close();
        }

        /// <summary>
        /// Disconnect from remote endpoint
        /// </summary>
        public void Disconnect()
        {
            Status = TcpOutputStatus.Disconnected;
            Error = TcpOutputError.NoError;
            _client.Close();
        }

        /// <summary>
        /// Write bytes to remote endpoint
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task WriteAsync(byte[] buffer, int length, CancellationToken stoppingToken = default)
        {
            if (IsStopped || IsDisconnected)
            {
                return;
            }

            try
            {
                var stream = _client.GetStream();
                await stream.WriteAsync(buffer.AsMemory(0, length), stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex)
            {
                _logger.LogError($"{ex.SocketErrorCode}");
                Status = TcpOutputStatus.Disconnected;
                await ConnectAsync(stoppingToken);
            }
            catch (IOException ex)
            {
                Error = TcpOutputError.IOError;
                Status = TcpOutputStatus.Disconnected;
                _logger.LogError(ex.Message);
                await ConnectAsync(stoppingToken);
            }
        }

        public void Dispose()
        {
            if(!_disposed)
            {
            _client.Close();
                _disposed = true;
            }
        }

        public override string ToString() => $"{Hostname}:{Port}";
    }
}
