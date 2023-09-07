using Microsoft.Extensions.Hosting;
using System.Net.Sockets;

namespace AirRoute.ADSB
{
    public class RouterManagerService : BackgroundService
    {
        private readonly RouterManager _routerManager;

        public RouterManagerService(RouterManager routerManager)
        {
            _routerManager = routerManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _routerManager.Outputs.ForEach(output => _ = output.ConnectAsync(stoppingToken));

            await HandleConnections(stoppingToken);
        }

        private async Task HandleConnections(CancellationToken stoppingToken)
        {
            try
            {
                _routerManager.Input.Start();
                await _routerManager.Input.HandleConnections(Input_HandleClient, stoppingToken);
                _routerManager.Input.Stop();

                DisconnectOutputs();
            }
            catch (OperationCanceledException) { }
        }

        private void WriteToOutputs(byte[] buffer, int length, CancellationToken stoppingToken)
        {
            _routerManager.Outputs.ForEach(output => _ = output.WriteAsync(buffer, length, stoppingToken));
        }

        private void DisconnectOutputs()
        {
            _routerManager.Outputs.ForEach(output => output.Disconnect());
        }

        private async Task Input_HandleClient(TcpClient client, NetworkStream stream, CancellationToken stoppingToken = default)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var buffer = new byte[2048];
                    var length = await stream.ReadAsync(buffer, stoppingToken);

                    if (length == 0)
                    {
                        break;
                    }

                    WriteToOutputs(buffer, length, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}
