using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    class Program
    {
        /// <summary>
        /// A simple JMS client.
        /// </summary>
        /// <param name="command">Command to run. Expected: Send, Receive</param>
        /// <param name="message"></param>
        /// <param name="numberOfMessages"></param>
        /// <param name="delayBetweenMessages"></param>
        static Task Main(ProgramCommands command = ProgramCommands.Receive, string message = null, int numberOfMessages = 1, int delayBetweenMessages = 0)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    switch (command)
                    {
                        case ProgramCommands.Send:

                            services.AddHostedService<TopicSenderHostedService>();
                            services.AddSingleton(new TopicSenderOptions
                            {
                                MessageText = message,
                                NumberOfMessages = numberOfMessages,
                                DelayBetweenMessages = delayBetweenMessages
                            });

                            break;

                        case ProgramCommands.Receive:

                            services.AddHostedService<TopicReceiverHostedService>();

                            break;
                    }

                    services.AddOptions<TopicSettings>().Bind(context.Configuration.GetSection("topicSettings"));
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation($"{AppDomain.CurrentDomain.FriendlyName} is starting");

            var appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            appLifetime.ApplicationStarted.Register(() => OnStarted(logger));
            appLifetime.ApplicationStopped.Register(() => OnStopped(logger));

            return host.RunAsync();
        }

        protected static void OnStarted(ILogger logger)
        {
            logger.LogInformation($"{AppDomain.CurrentDomain.FriendlyName} has started");
        }

        protected static void OnStopped(ILogger logger)
        {
            logger.LogInformation($"{AppDomain.CurrentDomain.FriendlyName} has stopped");
        }
    }
}
