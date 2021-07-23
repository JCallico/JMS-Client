namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public class TopicSettings
    {
        public string TopicName { get; set; }
        
        public string SubscriberName { get; set; }

        public string ClientId { get; set; }
        
        public string ProviderUrl { get; set; }

        public string Username  { get; set; }
        
        public string Password { get; set; } = string.Empty;

        public int ReceiveAttemptInterval { get; set; } = 5000;

        public int ErrorAttemptInterval { get; set; } = 15000;
    }
}
