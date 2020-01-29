using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RssPushNotification.Helper;
using RssPushNotification.Model;
using Serilog;

namespace RssPushNotification
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(@"C:\Users\fabri\AppData\Local\RssPushNotification\LogFile.txt")
                .CreateLogger();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<PushOverConfigurations>(hostContext.Configuration.GetSection("PushOverConfigurations"));
                    services.Configure<AppConfigurations>(hostContext.Configuration.GetSection("AppConfigurations"));
                    services.AddHostedService<Worker>();
                    services.AddScoped<IScopedProcessingService, ScopedProcessingService>();
                    services.AddAutoMapper(System.Reflection.Assembly.GetExecutingAssembly());
                    services.AddDbContext<RssPushNotificationContext>(options =>
                        options.UseSqlite(hostContext.Configuration.GetConnectionString("SqlLiteConnection")));
                })
                .UseSerilog();
    }
}
