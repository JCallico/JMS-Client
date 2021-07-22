namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public class TopicSenderOptions
    {
        public string MessageText { get; set; }

        public int NumberOfMessages { get; set; } = 1;

        public int DelayBetweenMessages { get; set; } = 0;
    }
}
