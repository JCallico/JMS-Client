using CommandLine;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public class ProgramOptions
    {
        [Option('c', "command", Required = true, HelpText = "Command to run. Expected: Send, Receive")]
        public ProgramOptionsCommand Command { get; set; }

        [Option('m', "message", Required = false, HelpText = "The message to send")]
        public string Message { get; set; }

        [Option('n', "numberOfMessages", Required = false, HelpText = "The number of message(s) to send", Default = 1)]
        public int NumberOfMessages { get; set; }

        [Option('d', "delayBetweenMessages", Required = false, HelpText = "The delay between message(s) in milliseconds", Default = 0)]
        public int DelayBetweenMessages { get; set; }
    }

    public enum ProgramOptionsCommand
    {
        Send,
        Receive
    }
}
