using Microsoft.Extensions.Hosting;
using System.Net.Sockets;

namespace AirRoute.ADSB
{
    public class RouterService : BackgroundService
    {
        private RouterManager RouterManager { get; }
        private TcpInput Input { get; }
        private List<TcpOutput> Outputs { get; }

        public RouterService(RouterManager routerManager)
        {
            RouterManager = routerManager;
            Input = RouterManager.Input;
            Outputs = RouterManager.Outputs;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            RouterManager.AddOutput("feed.adsb.lol", 30004);
            RouterManager.AddOutput("feed.adsb.fi", 30004);
            RouterManager.AddOutput("feed.adsb.one", 64004);
            RouterManager.AddOutput("feed.planespotters.net", 30004);
            RouterManager.AddOutput("feed1.adsbexchange.com", 30004);

            await ManageRouter(stoppingToken);
        }

        /// <summary>
        /// Manage the router
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async Task ManageRouter(CancellationToken stoppingToken)
        {
            try
            {
                Input.Start();
                await Input.HandleConnections(Input_HandleClient, stoppingToken);
                Input.Stop();

                RouterManager.DisconnectAll();
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// Write data to all connected outputs
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="stoppingToken"></param>
        private void WriteAll(byte[] buffer, int length, CancellationToken stoppingToken) =>
            RouterManager.WriteAll(buffer, length, stoppingToken);

        /// <summary>
        /// Method to handle clients
        /// </summary>
        /// <param name="client"></param>
        /// <param name="stream"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        private async Task Input_HandleClient(TcpClient client, NetworkStream stream, CancellationToken stoppingToken = default)
        {
            try
            {
                // New Client start all disconnected outputs
                RouterManager.StartAll();

                while (!stoppingToken.IsCancellationRequested)
                {
                    var buffer = new byte[2048];
                    var length = await stream.ReadAsync(buffer, stoppingToken);

                    if (length == 0)
                    {
                        break;
                    }

                    WriteAll(buffer, length, stoppingToken);
                }

                // No client so no data disconnect outputs
                RouterManager.DisconnectAll();
            }
            catch (OperationCanceledException) { }
        }
    }
}
