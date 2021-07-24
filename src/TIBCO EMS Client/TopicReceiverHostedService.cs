using System;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public class TopicReceiverHostedService : TopicHostedService
    {
        protected TopicSubscriber _subscriber;

        public TopicReceiverHostedService(
            ILogger<TopicReceiverHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IOptions<TopicSettings> settings) : base(logger, appLifetime, settings)
        {
        }
        
        protected override void Execute()
        {
            // first loop: assures that when a fatal error occurs the
            // connection is reestablished and the receiving process
            // is restarted
            while (true)
            {
                try
                {
                    Connect();

                    // second loop: continues to check for messages
                    // if none are available, sleeps for a while
                    // and then checks for messages again
                    while (true)
                    {
                        // third loop: continues to receive messages until there are no more available
                        while (true)
                        {
                            Logger.LogDebug("Checking for new messages...");

                            Message message = null;

                            try
                            {
                                message = _subscriber.ReceiveNoWait();

                                if (message == null)
                                {
                                    break;
                                }

                                LogMessage("Message received: ", message);

                                // todo: add handling logic here !!!!

                                // If the consumer's session is CLIENT_ACKNOWLEDGE, remember to
                                // call args.Message.Acknowledge() to prevent the message from
                                // getting redelivered, or consumer.Session.Recover() to force redelivery.
                                // Similarly, if the consumer's session is TRANSACTED, remember to
                                // call consumer.Session.Commit() to prevent the message from
                                // getting redelivered, or session.Rollback() to force redelivery.
                                message.Acknowledge();
                            }
                            catch (Exception e)
                            {
                                while (e.InnerException != null) e = e.InnerException;

                                if (message != null)
                                {
                                    Logger.LogError(e, $"An error just happened handing message {message.MessageID}: {e.Message}");

                                    // if something failed while handling the message
                                    // then forcing the message to be redelivered
                                    Session?.Recover();

                                    Logger.LogInformation($"Message {message.MessageID} will be redelivered...");
                                }
                                else
                                {
                                    // the exception is not related to message handling
                                    // rethrowing in order to force reconnection
                                    throw;
                                }
                            }
                        }

                        Thread.Sleep(Settings.Value.ReceiveAttemptInterval);
                    }
                }
                catch (InvalidClientIDException)
                {
                    Logger.LogWarning("Another instance using the same Client ID is already running. Another connection attempt will be made...");

                    Thread.Sleep(Settings.Value.ErrorAttemptInterval);
                }
                catch (Exception e)
                {
                    while (e.InnerException != null) e = e.InnerException;

                    Logger.LogError(e, $"An error just happened: {e.Message}");

                    Disconnect();

                    Logger.LogInformation("Receiving will be resumed...");

                    Thread.Sleep(Settings.Value.ErrorAttemptInterval);
                }
            }
        }

        protected override void Connect(bool useClientId = true)
        {
            base.Connect(true);

            Topic clientTopic = Session.CreateTopic(Settings.Value.TopicName);

            _subscriber = Session.CreateDurableSubscriber(clientTopic, Settings.Value.SubscriberName, string.Empty, true);

            Logger.LogDebug("Subscriber created...");

        }

        protected override void Disconnect()
        {
            _subscriber?.Close();

            base.Disconnect();
        }
    }
}
