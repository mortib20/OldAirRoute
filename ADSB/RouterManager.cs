using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AirRoute.ADSB
{
    public class RouterManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly List<TcpOutput> _outputs;
        private readonly TcpInput _input;

        public RouterManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RouterManager>();
            _input = new(loggerFactory, IPAddress.Any, 30004);
            _outputs = new();
        }

        public List<TcpOutput> Outputs => _outputs;
        public TcpInput Input => _input;

        public void AddOutput(string hostname, int port)
        {
            var output = new TcpOutput(_loggerFactory, hostname, port);

            if (_outputs.Contains(output))
            {
                Error("Output not added, already added");
                return;
            }

            Info($"Added {output}");
            _outputs.Add(output);
        }

        public void RemoveOutput(TcpOutput output)
        {
            var found = Outputs.Find(o => o == output);

            if (found is not null)
            {
                Outputs.Remove(found);
            }

            Info($"Removed {output}");
        }

        public void RemoveOutputAt()
        {

        }

        /// <summary>
        /// Starts all disconnected outputs
        /// </summary>
        public void StartAll()
        {
            Info("Starting all disconnected outputs");

            Outputs.FindAll(s => s.Status == TcpOutputStatus.Disconnected)
            .ToList()
            .ForEach(output => output.Start());
        }

        /// <summary>
        /// Write buffer of length to all connected outputs
        /// </summary>
        /// <param name="buffer">Buffer to write</param>
        /// <param name="length">Length to write</param>
        /// <param name="stoppingToken"></param>
        public void WriteAll(byte[] buffer, int length, CancellationToken stoppingToken) =>
            Outputs.FindAll(s => !s.IsStopped || s.IsDisconnected)
            .ToList()
            .ForEach(output => _ = output.WriteAsync(buffer, length, stoppingToken));

        /// <summary>
        /// Disconnects all connected outputs
        /// </summary>
        public void DisconnectAll()
        {
            Info("Disconnecting all connected clients");

            Outputs.FindAll(s => s.Status == TcpOutputStatus.Connected)
            .ToList()
            .ForEach(output => output.Disconnect());
        }

        /// <summary>
        /// Stops output if in list
        /// </summary>
        /// <param name="output"></param>
        public void StopOutput(TcpOutput output)
        {
            var found = Outputs.Find(m => m == output);

            if (found is not null)
            {
                Info($"Stopping {found}");
                found.Stop();
            }
        }

        /// <summary>
        /// Starts a output if in list
        /// </summary>
        /// <param name="output"></param>
        public void StartOutput(TcpOutput output)
        {
            var found = Outputs.Find(m => m == output);

            if (found is not null)
            {
                Info($"Starting manually stopped {found}");
                found.Start();
            }
        }

        private void Info(string text) => _logger.LogInformation(text);
        private void Error(string text) => _logger.LogError(text);
    }
}
