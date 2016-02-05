using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using NewRelic.Platform.Sdk;
using NewRelic.Platform.Sdk.Utils;
using Newtonsoft.Json;

namespace S203.NewRelic.NeuronEsb.Plugin
{
    public class EsbAgent : Agent
    {
        private static readonly Logger Logger = Logger.GetLogger("NeuronEsbLogger");
        private readonly Version _version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly string _name;
        private static string _host;
        private static int _port;
        private static string _instance;

        public EsbAgent(string name, string host, int port, string instance)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Name must be specified for the agent to initialize");
            if (string.IsNullOrEmpty(host))
                throw new ArgumentNullException(nameof(host), "Host must be specified for the agent to initialize");
            if (string.IsNullOrEmpty(instance))
                throw new ArgumentNullException(nameof(instance), "Instance must be specified for the agent to initialize");

            _name = name;
            _host = host;
            _port = port;
            _instance = instance;
        }

        public override string Guid => "E203.NewRelic.NeuronEsb";
        public override string Version => _version.Major + "." + _version.Minor + "." + _version.Build;

        public override string GetAgentName()
        {
            return _name;
        }

        public override void PollCycle()
        {
            // Assemble URIs
            var esbUri = $"http://{_host}:{_port}/neuronesb/api/v1/";
            var endpointHealth = $"endpointhealth/{_instance}";

            var serializer = new JsonSerializer();

            // Get endpoint health from Neuron
            var uri = esbUri + endpointHealth;
            Logger.Debug("Endpoint URI: " + esbUri);
            var client = new WebClient();
            client.Headers.Add("content-Type", "Application/json");

            Logger.Info("Getting Endpoint Health");
            var result = client.DownloadString(uri);

            Logger.Info("Deserializing Neuron Endpoint Health");
            var data = JsonConvert.DeserializeObject<List<EndpointHealth>>(result);
            Logger.Debug("Response received:\n" + data);

            Logger.Info("Sending Summary Metrics to New Relic");
            ReportMetric("Summary/Heartbeat", "count", data.Sum(d => d.Heartbeats));
            ReportMetric("Summary/Error", "count", data.Sum(d => d.Errors));
            ReportMetric("Summary/Warning", "count", data.Sum(d => d.Warnings));
            ReportMetric("Summary/MessageRate", "count", (float)data.Sum(d => d.MessageRate));
            ReportMetric("Summary/MessagesProcessed", "count", data.Sum(d => d.MessagesProcessed));

            Logger.Info("Sending Individual Metrics to New Relic");
            foreach (var endpoint in data)
            {
                ReportMetric("Heartbeat/" + endpoint.Name, "count", endpoint.Heartbeats);
                ReportMetric("Error/" + endpoint.Name, "count", endpoint.Errors);
                ReportMetric("Warning/" + endpoint.Name, "count", endpoint.Warnings);
                ReportMetric("MessageRate/" + endpoint.Name, "count/second", endpoint.Heartbeats);
                ReportMetric("MessagesProcessed/" + endpoint.Name, "count", endpoint.Heartbeats);
            }
        }
    }
}
