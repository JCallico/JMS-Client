using System;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    class Program
    {
        static Task Main(string[] args)
        {
            IHost host = null;

            Parser.Default.ParseArguments<ProgramOptions>(args)
                .WithParsed(options =>
                {
                    host = Host.CreateDefaultBuilder(args)
                        .ConfigureServices((context, services) =>
                        {
                            switch (options.Command)
                            {
                                case ProgramOptionsCommand.Send:

                                    services.AddHostedService<TopicSenderHostedService>();
                                    services.AddSingleton(new TopicSenderOptions
                                    {
                                        MessageText = options.Message,
                                        NumberOfMessages = options.NumberOfMessages,
                                        DelayBetweenMessages = options.DelayBetweenMessages
                                    });

                                    break;

                                case ProgramOptionsCommand.Receive:

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
                })
                .WithNotParsed(errors =>
                {
                    string name = AppDomain.CurrentDomain.FriendlyName;

                    Console.WriteLine("Usage:");
                    Console.WriteLine($"{name} -c \"Send\" -m \"Message text\" -n 10 -d 100");
                    Console.WriteLine($"{name} -c \"Receive\" ");
                });

            return host?.RunAsync() ?? Task.CompletedTask;
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
