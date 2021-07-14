using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using WebLogic.Messaging;

namespace ObjectSharp.Demos.JMSClient
{
    class Program
    {
        private static  string host = "localhost";
        private static  int port = 7001;

        private static string cfName = "weblogic.jms.ConnectionFactory";
        private static string topicName = "dizzyworldTopic";

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

                                SendMessageToTopic(context, topicName, options.Message);

                                break;

                            case OptionsCommand.Receive:

                                var receivingThread = new Thread(() =>
                                {
                                    ConsumeMessageFromTopicWithAutoAcknowledge(context, topicName);
                                });

                                receivingThread.Start();

                                Console.WriteLine("Waiting for messages, press any key to end... \n");
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
            IDictionary<string, object> paramMap = new Dictionary<string, object>();

            paramMap[Constants.Context.PROVIDER_URL] = $"t3://{host}:{port}";

            return ContextFactory.CreateContext(paramMap);
        }

        private static void SendMessageToTopic(IContext context, string TopicName, string messageText)
        {
            // -------------
            // Send Message:
            // -------------
            // Create a producer and send a non-persistent message.  Note
            // that even if the message were sent as persistent, it would be
            // automatically downgraded to non-persistent, as there are only
            // non-durable consumers subscribing to the topic.

            IConnection connection = null;
            ISession producerSession = null;
            IMessageProducer producer = null;

            try
            {
                ITopic topic = (ITopic)context.LookupDestination(TopicName);

                IConnectionFactory cf = context.LookupConnectionFactory(cfName);

                connection = cf.CreateConnection();

                connection.Start();

                producerSession = connection.CreateSession(Constants.SessionMode.AUTO_ACKNOWLEDGE);

                producer = producerSession.CreateProducer(topic);

                producer.DeliveryMode = Constants.DeliveryMode.NON_PERSISTENT;

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

        private static void ConsumeMessageFromTopicWithAutoAcknowledge(IContext context, string TopicName)
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

                IConnectionFactory cf = context.LookupConnectionFactory(cfName);

                connection = cf.CreateConnection();

                connection.Start();

                consumerSession = connection.CreateSession(Constants.SessionMode.AUTO_ACKNOWLEDGE);

                consumer = consumerSession.CreateConsumer(topic);

                // Using delegate to receive asynchronously any new message.

                consumer.Message += new MessageEventHandler((cons, args) =>
                {
                    WriteMessage("Message received:", args.Message);

                    // -----------------------------------------------------------------
                    // If the consumer's session is CLIENT_ACKNOWLEDGE, remember to
                    // call args.Message.Acknowledge() to prevent the message from
                    // getting redelivered, or consumer.Session.Recover() to force redelivery.
                    // Similarly, if the consumer's session is TRANSACTED, remember to
                    // call consumer.Session.Commit() to prevent the message from
                    // getting redeliverd, or consumer.Session.Rollback() to force redeivery.
                });

                // wait for new messages

                while (!receivingThreadStop)
                {
                    Thread.Sleep(1000);
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
