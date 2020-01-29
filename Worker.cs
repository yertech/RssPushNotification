using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ServiceModel.Syndication;
using System.Xml;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NPushover;
using NPushover.RequestObjects;
using RssPushNotification.Helper;
using RssPushNotification.Model;

namespace RssPushNotification
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        public IServiceProvider Services { get; }

        public Worker(IServiceProvider services, ILogger<Worker> logger)
        {
            Services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await DoWork(stoppingToken); 
           
        }

        private async Task DoWork(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Consume Scoped Service Hosted Service is working.");

            using (var scope = Services.CreateScope())
            {
                var scopedProcessingService =
                    scope.ServiceProvider
                        .GetRequiredService<IScopedProcessingService>();
                await scopedProcessingService.DoWork(stoppingToken);
            }
        }

       
    }
}
