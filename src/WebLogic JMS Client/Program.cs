using CommandLine;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Threading;
using WebLogic.Messaging;

namespace ObjectSharp.Demos.JMSClient.WebLogicJMSClient
{
    class Program
    {
        private static readonly string DefaultConnectionFactoryName = ConfigurationManager.AppSettings["Default.ConnectionFactoryName"];
        private static readonly string DefaultTopicName = ConfigurationManager.AppSettings["Default.TopicName"];
        private static readonly string DefaultSubscriberName = ConfigurationManager.AppSettings["Default.SubscriberName"];
        private static readonly string DefaultClientId = ConfigurationManager.AppSettings["Default.ClientId"];
        private static readonly string DefaultProviderUrl = ConfigurationManager.AppSettings["Default.ProviderUrl"];
        private static readonly string DefaultUsername = ConfigurationManager.AppSettings["appSettings:Default.Username"] ?? string.Empty;
        private static readonly string DefaultPassword = ConfigurationManager.AppSettings["appSettings:Default.Password"] ?? string.Empty;

        private static readonly int ReceiveAttemptInterval = int.Parse(ConfigurationManager.AppSettings["Global.ReceiveAttemptInterval"] ?? "5000");
        private static readonly int ErrorAttemptInterval = int.Parse(ConfigurationManager.AppSettings["Global.ErrorAttemptInterval"] ?? "15000");

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
                    string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                    string name = Path.GetFileName(codeBase);

                    Console.WriteLine("Usage:");
                    Console.WriteLine($"{name} -c \"Send\" -m \"Message text\" -n 10 -d 100");
                    Console.WriteLine($"{name} -c \"Receive\" ");
                });           
        }

        private static IContext CreateContext()
        {
            // -------------------------------------------------
            // Obtain a network connection to a WebLogic Server:
            // -------------------------------------------------
            // It also represents a naming context, which consists of methods for obtaining
            // JNDI name-to-object bindings for JMS destinations and connection factories.

            IDictionary<string, object> paramMap = new Dictionary<string, object>
            {
                { Constants.Context.PROVIDER_URL, DefaultProviderUrl }
            };

            return ContextFactory.CreateContext(paramMap);
        }

        private static void SendMessageToTopic(string topicName, string messageText, int numberOfMessages = 1, int delayBetweenMessages = 0)
        {
            IContext context = null;

            int numberOfMessagesSent = 0;

            try
            {
                while (true)
                {
                    try
                    {
                        context = CreateContext();

                        ITopic topic = (ITopic)context.LookupDestination(topicName);

                        IConnectionFactory cf = context.LookupConnectionFactory(DefaultConnectionFactoryName);

                        IConnection connection = cf.CreateConnection(DefaultUsername, DefaultPassword);

                        connection.Start();

                        Console.WriteLine("Connected and attempting to send message...");

                        ISession producerSession = connection.CreateSession(Constants.SessionMode.CLIENT_ACKNOWLEDGE);

                        IMessageProducer producer = producerSession.CreateProducer(topic);

                        producer.DeliveryMode = Constants.DeliveryMode.PERSISTENT;

                        while (numberOfMessagesSent < numberOfMessages)
                        {
                            ITextMessage sendMessage = producerSession.CreateTextMessage(messageText);

                            producer.Send(sendMessage);

                            numberOfMessagesSent++;

                            WriteMessage("Message sent:", sendMessage);

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

                        // Close the context.  The CloseAll method closes the network
                        // connection and all related open connections, sessions, producers,
                        // and consumers.

                        context?.CloseAll();

                        Console.WriteLine("Sending will be resumed...");

                        Thread.Sleep(ErrorAttemptInterval);
                    }
                }
            }
            finally
            {
                // Close the context.  The CloseAll method closes the network
                // connection and all related open connections, sessions, producers,
                // and consumers.

                context?.CloseAll();
            }
        }

        private static void ReceiveMessagesFromTopic(string topicName)
        {
            IContext context = null;

            try
            {
                // first loop: assures that when a fatal error occurs the
                // connection is reestablish and the receiving process
                // is restarted
                while (true)
                {
                    try
                    {
                        context = CreateContext();

                        ITopic topic = (ITopic)context.LookupDestination(topicName);

                        IConnectionFactory cf = context.LookupConnectionFactory(DefaultConnectionFactoryName);

                        IConnection connection = cf.CreateConnection(DefaultUsername, DefaultPassword);

                        // --------------------------------------------
                        // Assign a unique client-id to the connection:
                        // --------------------------------------------
                        // Durable subscribers must use a connection with an assigned
                        // client-id.   Only one connection with a given client-id
                        // can exist in a cluster at the same time.  An alternative
                        // to using the API is to configure a client-id via connection
                        // factory configuration.

                        connection.ClientID = DefaultClientId;

                        connection.Start();

                        Console.WriteLine("Connected...");

                        ISession consumerSession = connection.CreateSession(Constants.SessionMode.CLIENT_ACKNOWLEDGE);

                        // -----------------------------------------------
                        // Create a durable subscription and its consumer.
                        // -----------------------------------------------
                        // Only one consumer at a time can attach to the durable
                        // subscription for the same connection ID and
                        // subscription ID.
                        //
                        // Unlike queue consumers, topic consumers must be created
                        // *before* a message is sent in order to receive the message!

                        IMessageConsumer consumer = consumerSession.CreateDurableSubscriber(topic, DefaultSubscriberName);

                        // second loop: continues to check for messages
                        // if none are available, sleeps for a while
                        // and then checks for messages again
                        while (true)
                        {
                            // third loop: continues to receive messages until there are no more available
                            while (true)
                            {
                                Console.WriteLine("Checking for new messages...");

                                IMessage message = null;

                                try
                                {
                                    message = consumer.ReceiveNoWait();

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
                                    // getting redelivered, or consumer.Session.Rollback() to force redelivery.
                                    message.Acknowledge();
                                }
                                catch (Exception e)
                                {
                                    while (e.InnerException != null) e = e.InnerException;

                                    if (message != null)
                                    {

                                        Console.WriteLine($"An error just happened handing message {message.JMSMessageID}: {e.Message}");

                                        // if something failed while handling the message
                                        // then forcing the message to be redelivered
                                        consumer.Session.Recover();

                                        Console.WriteLine($"Message {message.JMSMessageID} will be redelivered...");
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

                        // Close the context.  The CloseAll method closes the network
                        // connection and all related open connections, sessions, producers,
                        // and consumers.

                        context?.CloseAll();

                        Console.WriteLine("Receiving will be resumed...");

                        Thread.Sleep(ErrorAttemptInterval);
                    }
                }
            }
            finally
            {
                // Close the context.  The CloseAll method closes the network
                // connection and all related open connections, sessions, producers,
                // and consumers.

                context?.CloseAll();
            }
        }

        private static void WriteMessage(string header, IMessage message)
        {
            var text = message is ITextMessage textMessage ? textMessage.Text : "Not available";

            Console.WriteLine($"{header}\n- Message ID: {message.JMSMessageID}\n- Text: {text}");
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
