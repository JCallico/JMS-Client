using System;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public class TopicSenderHostedService : TopicHostedService
    {
        private readonly TopicSenderOptions _senderOptions;

        private static readonly SemaphoreSlim _publisherLock = new SemaphoreSlim(1);

        private TopicPublisher _publisher;

        public TopicSenderHostedService(
            ILogger<TopicReceiverHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IOptions<TopicSettings> settings,
            TopicSenderOptions senderOptions) : base(logger, appLifetime, settings)
        {
            _senderOptions = senderOptions;
        }

        protected override void Execute(string topicName)
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

                    _publisher = Session.CreatePublisher(generalTopic);

                    Connection.Start();

                    Logger.LogDebug("Connected and attempting to send message...");

                    while (numberOfMessagesSent < _senderOptions.NumberOfMessages)
                    {
                        TextMessage message = Session.CreateTextMessage(_senderOptions.MessageText);

                        try
                        {
                            _publisherLock.Wait();

                            if (_publisher == null)
                            {
                                break;
                            }

                            _publisher.Publish(message);
                        }
                        finally
                        {
                            _publisherLock.Release();
                        }

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

                    CloseAllConnections();

                    Logger.LogInformation("Sending will be resumed...");

                    Thread.Sleep(Settings.Value.ErrorAttemptInterval);
                }
            }
        }

        protected override void CloseAllConnections()
        {
            try
            {
                _publisherLock.Wait();

                _publisher?.Close();

                _publisher = null;
            }
            finally
            {
                _publisherLock.Release();
            }

            base.CloseAllConnections();
        }
    }
}
