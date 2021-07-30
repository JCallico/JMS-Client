using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Callicode.JMSClient.Common
{
    public abstract class GenericTopicHostedService<T, TM> : IHostedService
    {
        protected GenericTopicHostedService(
            ILogger<T> logger,
            IHostApplicationLifetime appLifetime)
        {
            Logger = logger;
            AppLifetime = appLifetime;
        }

        protected ILogger Logger { get; set; }

        protected IHostApplicationLifetime AppLifetime { get; set; }

       
        #region App lifetime 

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"{nameof(T)} is starting");

            Task.Run(() => Execute(), cancellationToken);

            Logger.LogInformation($"{nameof(T)} has started");

            return Task.CompletedTask;
        }

        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"{nameof(T)} is stopping");

            Disconnect();

            Logger.LogInformation($"{nameof(T)} has stopped");

            return Task.CompletedTask;
        }

        #endregion

        #region Implementation

        protected abstract void Execute();

        protected abstract void Connect(bool useClientId = true);

        protected abstract void Disconnect();

        protected abstract void LogMessage(string header, TM message);

        #endregion
    }
}
