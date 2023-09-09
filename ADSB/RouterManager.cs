using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace AirRoute.ADSB
{
    public class RouterManagerConfig
    {
        public RouterManagerConfigInputItem Input { get; set; }
        public List<RouterManagerConfigOutputItem> Outputs { get; set; }
    }

    public class RouterManagerConfigOutputItem
    {
        public string Hostname { get; set; }
        public int Port { get; set; }
    }

    public class RouterManagerConfigInputItem
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }


    public class RouterManager
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RouterManager> _logger;

        public List<TcpOutput> Outputs { get; private set; }
        public TcpInput Input { get; private set; }

        public RouterManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<RouterManager>();
            Outputs = new();
            Input = new(loggerFactory, IPAddress.Any, 30004);
            ReadFromConfig();
        }

        public void AddOutput(string hostname, int port, bool connect = false, CancellationToken stoppingToken = default)
        {
            var output = new TcpOutput(_loggerFactory, hostname, port);

            Outputs.Add(output);
            _logger.LogInformation($"Added {output} output");

            if (connect)
            {
                _ = output.ConnectAsync(stoppingToken);
            }
        }

        private void ReadFromConfig()
        {
            var text = File.ReadAllText("routerconfig.json"); // Changes this to be better (I dont know currently) lol
            var config = JsonSerializer.Deserialize<RouterManagerConfig>(text);

            Input = new(_loggerFactory, IPAddress.Parse(config.Input.Address), config.Input.Port); // Add checks for IPAddress, Port etc
            config.Outputs.ForEach(output => AddOutput(output.Hostname, output.Port, false));
        }

        private void WriteToConfig()
        {
            var config = new RouterManagerConfig
            {
                Outputs = new(),
                Input = new()
            };

            config.Input = new() { Address = Input.Address.ToString(), Port = Input.Port };
            Outputs.ForEach(output => config.Outputs.Add(new() { Hostname = output.Hostname, Port = output.Port }));

            var options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
            var text = JsonSerializer.Serialize(config);

            File.WriteAllText("routerconfig.json", text);
        }
    }
}
