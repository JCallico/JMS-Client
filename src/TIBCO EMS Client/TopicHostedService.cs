using System.Text.Json;
using Callicode.JMSClient.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIBCO.EMS;

namespace Callicode.JMSClient.TibcoEmsClient
{
    public abstract class TopicHostedService : GenericTopicHostedService<TopicHostedService, Message>
    {
        protected TopicHostedService(
            ILogger<TopicHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IOptions<TopicSettings> settings) : base(logger, appLifetime)
        {
            Logger = logger;
            AppLifetime = appLifetime;
            Settings = settings;
        }

        protected IOptions<TopicSettings> Settings { get; set; }

        protected TopicConnection Connection { get; set; } = null;

        protected TopicSession Session { get; set; } = null;
        
        #region Implementation

        protected override void Connect(bool useClientId = true)
        {
            TopicConnectionFactory factory = useClientId
                ? new TopicConnectionFactory(Settings.Value.ProviderUrl, Settings.Value.ClientId)
                : new TopicConnectionFactory(Settings.Value.ProviderUrl);

            Connection = factory.CreateTopicConnection(Settings.Value.Username, Settings.Value.Password);

            Session = Connection.CreateTopicSession(false, TIBCO.EMS.Session.CLIENT_ACKNOWLEDGE);

            Connection.Start();

            Logger.LogDebug("Connected...");
        }

        protected override void Disconnect()
        {
            Session?.Close();
            Connection?.Close();

            Logger.LogDebug("Disconnected...");
        }

        protected override void LogMessage(string header, Message message)
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
