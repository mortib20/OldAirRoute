using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Sockets;
using System;

namespace AirRoute.ADSB
{
    public enum TcpOutputStatus
    {
        Disconnected,
        Connected,
        Reconnecting,
        Error
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
        private const int MAX_CONNECTION_TRIES = 3;
        private readonly TimeSpan INITIAL_TIMEOUT = TimeSpan.Zero;
        private readonly ILogger _logger;
        private TcpClient _client;
        private string _statusMessage = "Disconnected";
        private string _errorMessage = "No Error";

        public TcpOutputStatus Status { get; private set; } = TcpOutputStatus.Disconnected;
        public TcpOutputError Error { get; private set; } = TcpOutputError.NoError;
        public bool HasError => Error != TcpOutputError.NoError;

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                _logger.LogInformation(StatusMessage);
                Debug.WriteLine(StatusMessage);
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                _errorMessage = value;
                if (Error != TcpOutputError.NoError)
                {
                    _logger.LogError(ErrorMessage);
                    Debug.WriteLine(ErrorMessage);
                }
            }
        }

        public string Hostname { get; }
        public int Port { get; }

        public TcpOutput(ILoggerFactory loggerFactory, string hostname, int port)
        {
            Hostname = hostname;
            Port = port;
            _client = new();
            _logger = loggerFactory.CreateLogger($"{GetType()} {this}");
        }

        /// <summary>
        /// Connects asynchronous to remote endpoint
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns><see cref="true"/> when connected, <see cref="false"/> when connection failed</returns>
        public async Task<bool> ConnectAsync(CancellationToken stoppingToken = default)
        {
            if (Status == TcpOutputStatus.Reconnecting || Status == TcpOutputStatus.Connected)
            {
                return false;
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
                    SetStatus(TcpOutputStatus.Reconnecting, $"{connectionTries}. connection try");

                    await _client.ConnectAsync(Hostname, Port);

                    SetStatus(TcpOutputStatus.Connected, "Connected");
                    return true;
                }
                catch (OperationCanceledException) { }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
                {
                    SetError(TcpOutputError.HostNotFound, "Unable to find Hostname");
                    SetStatus(TcpOutputStatus.Disconnected, "Unable to find Hostname");
                    return false;
                }
                catch (SocketException ex)
                {
                    HandleSocketExceptionError(ex);

                    await Task.Delay(timeout, stoppingToken);
                }
            }

            SetStatus(TcpOutputStatus.Disconnected, "Max connection tries reached, failed to connect");
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
                    SetError(TcpOutputError.ConnectionRefused, "Connection refused");
                    break;
                case SocketError.ConnectionReset:
                    SetError(TcpOutputError.ConnectionReset, "Connection reset");
                    break;
                case SocketError.TimedOut:
                    SetError(TcpOutputError.Timeout, "Connection timeout");
                    break;
                case SocketError.HostUnreachable:
                    SetError(TcpOutputError.HostUnreachable, "Host Unreachable");
                    break;
                case SocketError.NetworkUnreachable:
                    SetError(TcpOutputError.NetworkUnreachable, "Network Unreachable");
                    break;
                case SocketError.InvalidArgument:
                    SetError(TcpOutputError.InvalidArgument, "Invalid Argument");
                    break;
                case SocketError.AccessDenied:
                    SetError(TcpOutputError.AccessDenied, "Access Denied");
                    break;
            }
        }

        /// <summary>
        /// Disconnect from remote endpoint
        /// </summary>
        public void Disconnect()
        {
            SetStatus(TcpOutputStatus.Disconnected, "Disconnected");
            SetError(TcpOutputError.NoError, "No Error");
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
            try
            {
                var stream = _client.GetStream();
                await stream.WriteAsync(buffer.AsMemory(0, length), stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex)
            {
                _logger.LogError($"{ex.SocketErrorCode}");
                SetStatus(TcpOutputStatus.Disconnected, "Disconnected");
                await ConnectAsync();
            }
            catch (IOException ex)
            {
                SetError(TcpOutputError.IOError, "Remotehost closed stream");
                SetStatus(TcpOutputStatus.Disconnected, "Disonnected");
                await ConnectAsync(stoppingToken);
            }
        }

        /// <summary>
        /// Set Status and StatusMessage
        /// </summary>
        /// <param name="status"></param>
        /// <param name="statusMessage"></param>
        private void SetStatus(TcpOutputStatus status, string statusMessage)
        {
            Status = status;
            StatusMessage = statusMessage;
        }

        /// <summary>
        /// Set Error and ErrorMessage
        /// </summary>
        /// <param name="error"></param>
        /// <param name="errorMessage"></param>
        private void SetError(TcpOutputError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage;
        }

        public void Dispose()
        {
            _client.Close();
        }

        public override string ToString() => $"{Hostname}:{Port}";
    }
}
