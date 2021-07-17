using System;
using System.Diagnostics;
using System.Threading;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Test started");
            new Program().Run();
            Console.ReadLine();
        }

        private void Run()
        {
            //StartEMSServer();
            CreateEMSServerTopicPublisher();
            CreateClientTopicSubscriber("Owner LIKE '%Javier Callico%'"); // Pass "" for no message selector
            EMSServerPublishThisMessage("Hello World", "Owner", "Javier Callico");
        }

        #region EMS Server
        private const string tibcoEMSPath = @"C:\tibco\ems\8.6\bin\";
        private readonly string tibcoEMSExecutable = tibcoEMSPath + "tibemsd.exe";
        private Process tibcoEMSProcess;
        public void StartEMSServer()
        {
            tibcoEMSProcess = new Process();
            ProcessStartInfo processStartInfo = new ProcessStartInfo(tibcoEMSExecutable);
            tibcoEMSProcess.StartInfo = processStartInfo;
            processStartInfo.WorkingDirectory = tibcoEMSPath;
            bool started = tibcoEMSProcess.Start();
            Thread.Sleep(500);
        }

        TopicConnection publisherConnection;
        TopicSession publisherSession;
        TopicPublisher emsServerPublisher;
        private void CreateEMSServerTopicPublisher()
        {
            TopicConnectionFactory factory = new TopicConnectionFactory("tcp://localhost:7222");
            publisherConnection = factory.CreateTopicConnection("", ""); // Username, password
            publisherSession = publisherConnection.CreateTopicSession(false, Session.CLIENT_ACKNOWLEDGE);
            Topic generalTopic = publisherSession.CreateTopic("dizzyworldTopic");
            emsServerPublisher = publisherSession.CreatePublisher(generalTopic);

            publisherConnection.Start();
        }

        internal void EMSServerPublishThisMessage(string message, string propertyName, string propertyValue)
        {
            TextMessage textMessage = publisherSession.CreateTextMessage();
            textMessage.Text = message;
            textMessage.SetStringProperty(propertyName, propertyValue);
            emsServerPublisher.Publish(textMessage);
            Console.WriteLine("EMS Publisher published message: " + message);
        }

        #endregion

        #region EMS Client
        TopicConnection subscriberConnection;
        TopicSession subscriberSession;
        private void CreateClientTopicSubscriber(string messageSelector)
        {
            TopicConnectionFactory factory = new TopicConnectionFactory("tcp://localhost:7222");
            subscriberConnection = factory.CreateTopicConnection("", "");  // Username, password
            subscriberConnection.Start();
            subscriberSession = subscriberConnection.CreateTopicSession(false, Session.CLIENT_ACKNOWLEDGE);
            Topic clientTopic = subscriberSession.CreateTopic("dizzyworldTopic");
            TopicSubscriber clientTopicSubscriber = subscriberSession.CreateSubscriber(clientTopic, messageSelector, true);
            clientTopicSubscriber.MessageHandler += new EMSMessageHandler(test_MessageHandler);
        }

        void test_MessageHandler(object sender, EMSMessageEventArgs args)
        {
            Console.WriteLine("EMS Client received message: " + args.Message.ToString());

            args.Message.Acknowledge();
        }

        #endregion
    }
}