using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RssPushNotification.Helper;
using RssPushNotification.Model;

namespace RssPushNotification
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                    services.AddScoped<IScopedProcessingService, ScopedProcessingService>();

                    services.AddAutoMapper(System.Reflection.Assembly.GetExecutingAssembly());
                    services.AddDbContext<RssPushNotificationContext>(options =>
                        options.UseSqlite(hostContext.Configuration.GetConnectionString("SqlLiteConnection")));
                });
    }
}
