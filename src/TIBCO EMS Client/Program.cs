using CommandLine;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    class Program
    {
        private static readonly string DefaultTopicName = Configuration["appSettings:Default.TopicName"];
        private static readonly string DefaultSubscriberName = Configuration["appSettings:Default.SubscriberName"];
        private static readonly string DefaultClientId = Configuration["appSettings:Default.ClientId"];
        private static readonly string DefaultProviderUrl = Configuration["appSettings:Default.ProviderUrl"];

        private static readonly int ReceiveAttemptInterval = int.Parse(Configuration["appSettings:Global.ReceiveAttemptInterval"] ?? "5000");
        private static readonly int ErrorAttemptInterval = int.Parse(Configuration["appSettings:Global.ErrorAttemptInterval"] ?? "15000");

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    switch (options.Command)
                    {
                        case OptionsCommand.Send:

                            SendMessageToTopic(DefaultTopicName, options.Message, options.NumberOfMessages, options.DelayBetweenMessages);

                            break;

                        case OptionsCommand.Receive:

                            ReceiveMessagesFromTopic(DefaultTopicName);

                            break;
                    }
                })
                .WithNotParsed(errors =>
                {
                    string name = AppDomain.CurrentDomain.FriendlyName;

                    Console.WriteLine("Usage:");
                    Console.WriteLine($"{name} -c \"Send\" -m \"Message text\" -n 10 -d 100");
                    Console.WriteLine($"{name} -c \"Receive\" ");
                });
        }

        private static IConfiguration _configuration;
        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    _configuration = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", true, true)
                        .Build();
                }

                return _configuration;
            }
        }

        private static void SendMessageToTopic(string topicName, string messageText, int numberOfMessages = 1, int delayBetweenMessages = 0)
        {
            TopicConnection publisherConnection = null;
            TopicSession publisherSession = null;
            TopicPublisher publisher = null;

            int numberOfMessagesSent = 0;

            try
            {
                while (numberOfMessagesSent < numberOfMessages)
                {
                    try
                    {
                        TopicConnectionFactory factory = new TopicConnectionFactory(DefaultProviderUrl);

                        publisherConnection = factory.CreateTopicConnection("", ""); // Username, password
                        
                        publisherSession = publisherConnection.CreateTopicSession(false, Session.CLIENT_ACKNOWLEDGE);
                        
                        Topic generalTopic = publisherSession.CreateTopic(topicName);
                        
                        publisher = publisherSession.CreatePublisher(generalTopic);

                        publisherConnection.Start();

                        Console.WriteLine("Connected and attempting to send message...");

                        while (numberOfMessagesSent < numberOfMessages)
                        {
                            TextMessage message = publisherSession.CreateTextMessage(messageText);

                            publisher.Publish(message);

                            numberOfMessagesSent++;

                            WriteMessage("Message sent:", message);

                            if (delayBetweenMessages != 0)
                            {
                                Thread.Sleep(delayBetweenMessages);
                            }
                        }

                        // exiting sending loop
                        break;
                    }
                    catch (Exception e)
                    {
                        while (e.InnerException != null) e = e.InnerException;

                        Console.WriteLine($"An error just happened: {e.Message}");

                        // Closing all
                        publisher?.Close();
                        publisherSession?.Close();
                        publisherConnection?.Close();

                        Console.WriteLine("Sending will be resumed...");

                        Thread.Sleep(ErrorAttemptInterval);
                    }
                }
            }
            finally
            {
                // Closing all
                publisher?.Close();
                publisherSession?.Close();
                publisherConnection?.Close();
            }
        }

        private static void ReceiveMessagesFromTopic(string topicName)
        {
            TopicConnection subscriberConnection = null;
            TopicSession subscriberSession = null;
            TopicSubscriber subscriber = null;

            try
            {
                // first loop: assures that when a fatal error occurs the
                // connection is reestablished and the receiving process
                // is restarted
                while (true)
                {
                    try
                    {
                        TopicConnectionFactory factory = new TopicConnectionFactory(DefaultProviderUrl, DefaultClientId);

                        subscriberConnection = factory.CreateTopicConnection("", "");  // Username, password

                        subscriberConnection.Start();

                        Console.WriteLine("Connected...");

                        subscriberSession = subscriberConnection.CreateTopicSession(false, Session.CLIENT_ACKNOWLEDGE);
                        
                        Topic clientTopic = subscriberSession.CreateTopic(topicName);
                        
                        subscriber = subscriberSession.CreateDurableSubscriber(clientTopic, DefaultSubscriberName, string.Empty, true);

                        // second loop: continues to check for messages
                        // if none are available, sleeps for a while
                        // and then checks for messages again
                        while (true)
                        {
                            // third loop: continues to receive messages until there are no more available
                            while (true)
                            {
                                Console.WriteLine("Checking for new messages...");

                                Message message = null;

                                try
                                {
                                    message = subscriber.ReceiveNoWait();

                                    if (message == null)
                                    {
                                        break;
                                    }

                                    WriteMessage("Message received:", message);

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
                                        Console.WriteLine($"An error just happened handing message {message.MessageID}: {e.Message}");

                                        // if something failed while handling the message
                                        // then forcing the message to be redelivered
                                        subscriberSession?.Recover();

                                        Console.WriteLine($"Message {message.MessageID} will be redelivered...");
                                    }
                                    else
                                    { 
                                        // the exception is not related to message handling
                                        // rethrowing in order to force reconnection
                                        throw;
                                    }                                 
                                }
                            }

                            Thread.Sleep(ReceiveAttemptInterval);
                        }
                    }
                    catch (InvalidClientIDException)
                    {
                        Console.WriteLine("Another instance using the same Client ID is already running. Another connection attempt will be made...");

                        Thread.Sleep(ErrorAttemptInterval);
                    }
                    catch (Exception e)
                    {
                        while (e.InnerException != null) e = e.InnerException;

                        Console.WriteLine($"An error just happened: {e.Message}");

                        // Closing all
                        subscriber?.Close();
                        subscriberSession?.Close();
                        subscriberConnection?.Close();

                        Console.WriteLine("Receiving will be resumed...");

                        Thread.Sleep(ErrorAttemptInterval);
                    }
                }
            }
            finally
            {
                // Closing all
                subscriber?.Close();
                subscriberSession?.Close();
                subscriberConnection?.Close();
            }
        }

        private static void WriteMessage(string header, Message message)
        {
            var text = message is TextMessage textMessage ? textMessage.Text : "Not available";

            Console.WriteLine($"{header}\n- Message ID: {message.MessageID}\n- Text: {text}");
        }

        public class Options
        {
            [Option('c', "command", Required = true, HelpText = "Command to run. Expected: Send, Receive")]
            public OptionsCommand Command { get; set; }

            [Option('m', "message", Required = false, HelpText = "The message to send")]
            public string Message { get; set; }

            [Option('n', "numberOfMessages", Required = false, HelpText = "The number of message(s) to send", Default = 1)]
            public int NumberOfMessages { get; set; }

            [Option('d', "delayBetweenMessages", Required = false, HelpText = "The delay between message(s) in milliseconds", Default = 0)]
            public int DelayBetweenMessages { get; set; }
        }

        public enum OptionsCommand
        {
            Send,
            Receive
        }
    }
}
