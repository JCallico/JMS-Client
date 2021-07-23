using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public class TopicSenderHostedService : TopicHostedService
    {
        private readonly TopicSenderOptions _senderOptions;

        public TopicSenderHostedService(
            ILogger<TopicReceiverHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IOptions<TopicSettings> settings,
            TopicSenderOptions senderOptions) : base(logger, appLifetime, settings)
        {
            _senderOptions = senderOptions;
        }

        protected TopicPublisher Publisher { get; set; }

        protected override Task Execute(string topicName)
        {
            int numberOfMessagesSent = 0;

            while (numberOfMessagesSent < _senderOptions.NumberOfMessages)
            {
                try
                {
                    TopicConnectionFactory factory = new TopicConnectionFactory(Settings.Value.ProviderUrl);

                    Connection = factory.CreateTopicConnection(Settings.Value.Username, Settings.Value.Password);

                    Session = Connection.CreateTopicSession(false, TIBCO.EMS.Session.CLIENT_ACKNOWLEDGE);

                    Topic generalTopic = Session.CreateTopic(topicName);

                    Publisher = Session.CreatePublisher(generalTopic);

                    Connection.Start();

                    Logger.LogDebug("Connected and attempting to send message...");

                    while (numberOfMessagesSent < _senderOptions.NumberOfMessages)
                    {
                        TextMessage message = Session.CreateTextMessage(_senderOptions.MessageText);

                        Publisher.Publish(message);

                        numberOfMessagesSent++;

                        LogMessage($"Message sent: ", message);

                        if (_senderOptions.DelayBetweenMessages != 0)
                        {
                            Thread.Sleep(_senderOptions.DelayBetweenMessages);
                        }
                    }

                    // exiting sending loop
                    AppLifetime.StopApplication();
                }
                catch (Exception e)
                {
                    while (e.InnerException != null) e = e.InnerException;

                    Logger.LogError(e, $"An error just happened: {e.Message}");

                    CloseAll();

                    Logger.LogInformation("Sending will be resumed...");

                    Thread.Sleep(Settings.Value.ErrorAttemptInterval);
                }
            }

            return Task.CompletedTask;
        }

        protected override void CloseAll()
        {
            Publisher?.Close();

            base.CloseAll();
        }
    }
}
