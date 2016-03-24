using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using NewRelic.Platform.Sdk;
using NewRelic.Platform.Sdk.Processors;
using NewRelic.Platform.Sdk.Utils;
using Newtonsoft.Json.Linq;

namespace S203.NewRelic.NeuronEsb
{
    public class EsbAgent : Agent
    {
        private static readonly Logger Logger = Logger.GetLogger("NeuronEsbLogger");
        private readonly Version _version = Assembly.GetExecutingAssembly().GetName().Version;
        private readonly string _name;
        private static string _host;
        private static int _port;
        private static string _instance;

        private readonly IDictionary<string, IDictionary<string, EpochProcessor>> _queueProcessors;
        private readonly IProcessor _heartbeats;
        private readonly IProcessor _errors;
        private readonly IProcessor _warnings;
        private readonly IProcessor _messagesProcessed;

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

            _queueProcessors = new Dictionary<string, IDictionary<string, EpochProcessor>>();
            _heartbeats = new EpochProcessor();
            _errors = new EpochProcessor();
            _warnings = new EpochProcessor();
            _messagesProcessed = new EpochProcessor();
        }

        public override string Guid => "com.203sol.newrelic.neuronesb";
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
            
            // Get endpoint health from Neuron
            var uri = esbUri + endpointHealth;
            Logger.Debug("Endpoint URI: " + esbUri);
            var client = new WebClient();
            client.Headers.Add("content-type", "application/json");

            Logger.Debug("Getting Endpoint Health");
            var json = JArray.Parse(client.DownloadString(uri));
            
            Logger.Debug("Response received:\n" + json);

            Logger.Debug("Sending Summary Metrics to New Relic");
            ReportMetric("Summary/Heartbeats", "Messages/Second", _heartbeats.Process(json.Sum(j=>j["heartbeats"].Value<float>())));
            ReportMetric("Summary/Errors", "Messages/Second", _errors.Process(json.Sum(j=>j["errors"].Value<float>())));
            ReportMetric("Summary/Warnings", "Messages/Second", _warnings.Process(json.Sum(j => j["warnings"].Value<float>())));
            ReportMetric("Summary/MessagesProcessed", "Messages/Second", _messagesProcessed.Process(json.Sum(j => j["messagesProcessed"].Value<float>())));

            Logger.Debug("Sending Individual Metrics to New Relic");
            foreach (var endpoint in json)
            {
                var name = endpoint["name"].Value<string>();
                // See if the processors exist
                if (!_queueProcessors.ContainsKey(name))
                {
                    // Add the processors
                    var metrics = new Dictionary<string, EpochProcessor>
                    {
                        {"Heartbeats", new EpochProcessor()},
                        {"Errors", new EpochProcessor()},
                        {"Warnings", new EpochProcessor()},
                        {"MessagesProcessed", new EpochProcessor()}
                    };
                    _queueProcessors.Add(name, metrics);
                }

                // Process Metrics
                var currentQueue = _queueProcessors.FirstOrDefault(k => k.Key == name);
                foreach (var metric in currentQueue.Value)
                {
                    Logger.Debug("Metric Name: " + metric.Key);
                    Logger.Debug("Metric Value: " + endpoint[LowercaseFirst(metric.Key)].Value<float>());
                    ReportMetric("Queues/" + metric.Key + "/" + name, "Messages/Second", metric.Value.Process(endpoint[LowercaseFirst(metric.Key)].Value<float>()));
                }
            }
        }

        private string LowercaseFirst(string s)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // Return char and concat substring.
            return char.ToLower(s[0]) + s.Substring(1);
        }
    }
}
