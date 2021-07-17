using CommandLine;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    class Program
    {
        private static string DefaultTopicName = "dizzyworldTopic"; // ConfigurationManager.AppSettings["Default.TopicName"];
        private static string DefaultSubscriberName = "Default"; //ConfigurationManager.AppSettings["Default.SubscriberName"];
        private static string DefaultClientId = "Default"; //ConfigurationManager.AppSettings["Default.ClientId"];
        private static string DefaultProviderUrl = "tcp://localhost:7222"; //ConfigurationManager.AppSettings["Default.ProviderUrl"];

        private static int ReceiveAttemptInterval = 5000; // int.Parse(ConfigurationManager.AppSettings["Global.ReceiveAttemptInterval"] ?? "5000");
        private static int ErrorAttemptInterval = 15000; // int.Parse(ConfigurationManager.AppSettings["Global.ErrorAttemptInterval"] ?? "15000");

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    switch (options.Command)
                    {
                        case OptionsCommand.Send:

                            SendMessageToTopic(DefaultTopicName, options.Message);

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

                    Console.WriteLine($"Usage:");
                    Console.WriteLine($"{name} -c \"Send\" -m \"Message text\"");
                    Console.WriteLine($"{name} -c \"Receive\" ");
                });
        }

        private static void SendMessageToTopic(string TopicName, string messageText)
        {
            TopicConnection publisherConnection = null;
            TopicSession publisherSession = null;
            TopicPublisher publisher = null;

            try
            {
                while (true)
                {
                    try
                    {
                        TopicConnectionFactory factory = new TopicConnectionFactory(DefaultProviderUrl);

                        publisherConnection = factory.CreateTopicConnection("", ""); // Username, password
                        
                        publisherSession = publisherConnection.CreateTopicSession(false, Session.CLIENT_ACKNOWLEDGE);
                        
                        Topic generalTopic = publisherSession.CreateTopic(TopicName);
                        
                        publisher = publisherSession.CreatePublisher(generalTopic);

                        publisherConnection.Start();

                        Console.WriteLine("Connected and attemping to send message...");

                        TextMessage message = publisherSession.CreateTextMessage();
                        message.Text = messageText;
                        
                        // any properties
                        //textMessage.SetStringProperty("propertyName", "propertyValue");
                        
                        publisher.Publish(message);

                        WriteMessage("Message sent:", message);

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

        private static void ReceiveMessagesFromTopic(string TopicName)
        {
            TopicConnection subscriberConnection = null;
            TopicSession subscriberSession = null;
            TopicSubscriber subscriber = null;

            try
            {
                // first loop: assures that when a fatal error occurs the
                // connection is restablished and the receiving process
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
                        
                        Topic clientTopic = subscriberSession.CreateTopic(TopicName);
                        
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
                                    // getting redeliverd, or session.Rollback() to force redeivery.
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
                                        Console.WriteLine($"An error just happened: {e.Message}");
                                    }                                 
                                }
                            }

                            Thread.Sleep(ReceiveAttemptInterval);
                        }
                    }
                    catch (InvalidClientIDException)
                    {
                        Console.WriteLine("Another instance using the same Client ID is already running. Another connection attemp will be made...");

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
            string text;

            TextMessage textMessage = message as TextMessage;
            if (textMessage != null)
                text = textMessage.Text;
            else
                text = "Not available";

            Console.WriteLine($"{header}\n- Message ID: {message.MessageID}\n- Text: {text}");
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
