using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public abstract class TopicHostedService : IHostedService
    {
        protected TopicHostedService(
            ILogger<TopicReceiverHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IOptions<TopicSettings> settings)
        {
            Logger = logger;
            AppLifetime = appLifetime;
            Settings = settings;
        }

        protected ILogger Logger { get; set; }

        protected IHostApplicationLifetime AppLifetime { get; set; }

        protected IOptions<TopicSettings> Settings { get; set; }

        protected TopicConnection Connection { get; set; } = null;

        protected TopicSession Session { get; set; } = null;
        
        #region App lifetime 

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} is starting");

            Task.Run(() => Execute(), cancellationToken);

            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} has started");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} is stopping");

            Disconnect();

            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} has stopped");

            return Task.CompletedTask;
        }

        #endregion

        #region Implementation

        protected abstract void Execute();

        protected virtual void Connect(bool useClientId = true)
        {
            TopicConnectionFactory factory = useClientId
                ? new TopicConnectionFactory(Settings.Value.ProviderUrl, Settings.Value.ClientId)
                : new TopicConnectionFactory(Settings.Value.ProviderUrl);

            Connection = factory.CreateTopicConnection(Settings.Value.Username, Settings.Value.Password);

            Session = Connection.CreateTopicSession(false, TIBCO.EMS.Session.CLIENT_ACKNOWLEDGE);

            Connection.Start();

            Logger.LogDebug("Connected...");
        }

        protected virtual void Disconnect()
        {
            Session?.Close();
            Connection?.Close();

            Logger.LogDebug("Disconnected...");
        }

        protected void LogMessage(string header, Message message)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(header +
                                JsonSerializer.Serialize(new
                                    {
                                        MessageId = message.MessageID,
                                        Text = message is TextMessage textMessage ? textMessage.Text : "Not available",
                                        message.Timestamp
                                    },
                                    new JsonSerializerOptions { WriteIndented = true }));

                return;
            }

            Logger.LogInformation(header);
        }

        #endregion
    }
}
