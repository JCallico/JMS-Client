using CommandLine;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Threading;
using WebLogic.Messaging;

namespace ObjectSharp.Demos.JMSClient
{
    class Program
    {
        private static string DefaultConnectionFactoryName = ConfigurationManager.AppSettings["DefaultConnectionFactoryName"];
        private static string DefaultTopicName = ConfigurationManager.AppSettings["DefaultTopicName"];
        private static string DefaultSubscriberName = ConfigurationManager.AppSettings["DefaultSubscriberName"];
        private static string DefaultClientId = ConfigurationManager.AppSettings["DefaultClientId"];
        private static string DefaultProviderUrl = ConfigurationManager.AppSettings["DefaultProviderUrl"];

        private static bool receivingThreadStop = false;

        static void Main(string[] args)
        {
            IContext context = null;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    try
                    {
                        context = CreateContext();

                        switch (options.Command)
                        {
                            case OptionsCommand.Send:

                                SendMessageToTopic(context, DefaultTopicName, options.Message);

                                break;

                            case OptionsCommand.Receive:

                                var receivingThread = new Thread(() =>
                                {
                                    ConsumeMessageFromTopicWithClientAcknowledge(context, DefaultTopicName);
                                });

                                receivingThread.Start();

                                Console.ReadKey();

                                receivingThreadStop = true;

                                receivingThread.Join();

                                break;
                        }
                    }
                    finally
                    {
                        // Close the context.  The CloseAll method closes the network
                        // connection and all related open connections, sessions, producers,
                        // and consumers.

                        context.CloseAll();
                    }
                })
                .WithNotParsed(errors =>
                {
                    string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                    string name = Path.GetFileName(codeBase);

                    Console.WriteLine($"Usage:");
                    Console.WriteLine($"{name} -c \"Send\" -m \"Message text\"");
                    Console.WriteLine($"{name} -c \"Receive\" ");
                });           
        }

        private static IContext CreateContext()
        {
            IDictionary<string, object> paramMap = new Dictionary<string, object>
            {
                { Constants.Context.PROVIDER_URL, DefaultProviderUrl }
            };

            return ContextFactory.CreateContext(paramMap);
        }

        private static void SendMessageToTopic(IContext context, string TopicName, string messageText)
        {
            IConnection connection = null;
            ISession producerSession = null;
            IMessageProducer producer = null;

            try
            {
                ITopic topic = (ITopic)context.LookupDestination(TopicName);

                IConnectionFactory cf = context.LookupConnectionFactory(DefaultConnectionFactoryName);

                connection = cf.CreateConnection();

                // --------------------------------------------
                // Assign a unique client-id to the connection:
                // --------------------------------------------
                // Durable subscribers must use a connection with an assigned
                // client-id.   Only one connection with a given client-id
                // can exist in a cluster at the same time.  An alternative
                // to using the API is to configure a client-id via connection
                // factory configuration.

                //connection.ClientID = DefaultClientId;

                connection.Start();

                producerSession = connection.CreateSession(Constants.SessionMode.CLIENT_ACKNOWLEDGE);

                producer = producerSession.CreateProducer(topic);

                producer.DeliveryMode = Constants.DeliveryMode.PERSISTENT;

                ITextMessage sendMessage = producerSession.CreateTextMessage(messageText);

                producer.Send(sendMessage);

                WriteMessage("Message sent:", sendMessage);
            }
            finally
            {
                producer?.Close();
                producerSession?.Close();
                connection?.Close();
            }
        }

        private static void ConsumeMessageFromTopicWithClientAcknowledge(IContext context, string TopicName)
        {
            IConnection connection = null;
            ISession consumerSession = null;
            IMessageConsumer consumer = null;

            try
            {
                // ------------------------------------------
                // Create the asynchronous consumer delegate.   
                // ------------------------------------------
                // Create a session and a consumer; also designate a delegate 
                // that listens for messages that arrive asynchronously.  
                //
                // Unlike queue consumers, topic consumers must be created
                // *before* a message is sent in order to receive the message!
                //
                // IMPORTANT:  Sessions are not thread-safe.   We use multiple sessions 
                // in order to run the producer and async consumer concurrently.  The
                // consumer session and any of its producers and consumers 
                // can no longer be used outside of the OnMessage
                // callback once OnMessage is designated as its event handler, as
                // messages for the event handler may arrive in another thread.

                ITopic topic = (ITopic)context.LookupDestination(TopicName);

                IConnectionFactory cf = context.LookupConnectionFactory(DefaultConnectionFactoryName);

                connection = cf.CreateConnection();

                // --------------------------------------------
                // Assign a unique client-id to the connection:
                // --------------------------------------------
                // Durable subscribers must use a connection with an assigned
                // client-id.   Only one connection with a given client-id
                // can exist in a cluster at the same time.  An alternative
                // to using the API is to configure a client-id via connection
                // factory configuration.

                while (!receivingThreadStop)
                {
                    try
                    {
                        connection.ClientID = DefaultClientId;

                        connection.Start();

                        Console.WriteLine("Connected and waiting for messages, press any key to end... \n");

                        consumerSession = connection.CreateSession(Constants.SessionMode.CLIENT_ACKNOWLEDGE);

                        // -----------------------------------------------
                        // Create a durable subscription and its consumer.
                        // -----------------------------------------------
                        // Only one consumer at a time can attach to the durable
                        // subscription for connection ID "MyConnectionID" and
                        // subscription ID "MySubscriberID.
                        //
                        // Unlike queue consumers, topic consumers must be created
                        // *before* a message is sent in order to receive the message!

                        consumer = consumerSession.CreateDurableSubscriber(topic, DefaultSubscriberName);

                        while (!receivingThreadStop)
                        {
                            // secondary loop: ends when there are no more mensages
                            while (true)
                            {
                                IMessage message = consumer.ReceiveNoWait();

                                if (message == null)
                                {
                                    break;
                                }

                                WriteMessage("Message received:", message);

                                // If the consumer's session is CLIENT_ACKNOWLEDGE, remember to
                                // call args.Message.Acknowledge() to prevent the message from
                                // getting redelivered, or consumer.Session.Recover() to force redelivery.
                                // Similarly, if the consumer's session is TRANSACTED, remember to
                                // call consumer.Session.Commit() to prevent the message from
                                // getting redeliverd, or consumer.Session.Rollback() to force redeivery.
                                message.Acknowledge();
                            }

                            Thread.Sleep(1000);
                        }
                    }
                    catch (InvalidClientIDException e)
                    {
                        Console.WriteLine("Another instance of the client is already running. Another attemp will be made...");

                        Thread.Sleep(10000);
                    }
                }
            }
            finally
            {
                consumer?.Close();
                consumerSession?.Close();
                connection?.Close();
            }
        }

        private static void WriteMessage(string header, IMessage msg)
        {
            string text;

            ITextMessage textMessage = msg as ITextMessage;
            if (msg != null)
                text = textMessage.Text + Environment.NewLine;
            else
                text = "Not available";

            Console.WriteLine(header + Environment.NewLine +
              " Message ID = " + msg.JMSMessageID + Environment.NewLine +
              " Text: " + text);
        }

        public class Options
        {
            [Option('c', "command", Required = true, HelpText = "Command to run. Expected: Send, Receive")]
            public OptionsCommand Command { get; set; }

            [Option('m', "message", Required = false, HelpText = "The message to send")]
            public string Message { get; set; }
        }

        public enum OptionsCommand
        {
            Send,
            Receive
        }
    }
}
