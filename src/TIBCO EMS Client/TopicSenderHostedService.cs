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

        protected override void Execute()
        {
            int numberOfMessagesSent = 0;

            while (numberOfMessagesSent < _senderOptions.NumberOfMessages)
            {
                try
                {
                    Connect();

                    Logger.LogDebug("Attempting to send message...");

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

                    Disconnect();

                    Logger.LogInformation("Sending will be resumed...");

                    Thread.Sleep(Settings.Value.ErrorAttemptInterval);
                }
            }
        }

        protected override void Connect(bool useClientId = true)
        {
            base.Connect(false);

            Topic generalTopic = Session.CreateTopic(Settings.Value.TopicName);

            _publisher = Session.CreatePublisher(generalTopic);

            Logger.LogDebug("Publisher created...");
        }

        protected override void Disconnect()
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

            base.Disconnect();
        }
    }
}
