﻿using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIBCO.EMS;

namespace ObjectSharp.Demos.JMSClient.TibcoEmsClient
{
    public abstract class TopicHostedService : IHostedService
    {
        protected TopicHostedService(
            ILogger<TopicReceiverHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IOptions<TopicSettings> settings)
        {
            Logger = logger;
            AppLifetime = appLifetime;
            Settings = settings;
        }

        protected ILogger Logger { get; set; }

        protected IHostApplicationLifetime AppLifetime { get; set; }

        protected IOptions<TopicSettings> Settings { get; set; }

        protected TopicConnection Connection { get; set; } = null;

        protected TopicSession Session { get; set; } = null;
        
        #region App lifetime 

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} is starting");

            Task.Run(() => Execute(Settings.Value.TopicName), cancellationToken);

            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} has started");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} is stopping");

            CloseAll();

            Logger.LogInformation($"{nameof(TopicReceiverHostedService)} has stopped");

            return Task.CompletedTask;
        }

        #endregion

        #region Implementation

        protected abstract Task Execute(string topicName);

        protected virtual void CloseAll()
        {
            Session?.Close();
            Connection?.Close();

            Logger.LogDebug("All connections have been closed");
        }

        protected void LogMessage(string header, Message message)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug(header +
                                JsonSerializer.Serialize(new
                                    {
                                        MessageId = message.MessageID,
                                        Text = message is TextMessage textMessage ? textMessage.Text : "Not available",
                                        message.Timestamp
                                    },
                                    new JsonSerializerOptions { WriteIndented = true }));

                return;
            }

            Logger.LogInformation(header);
        }

        #endregion
    }
}