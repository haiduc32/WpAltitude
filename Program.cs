using DEM.Net.Core;
using System.IO;
using System;
using Microsoft.Extensions.DependencyInjection;
using DEM.Net.glTF;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Configuration;
using DEM.Net.Core.Configuration;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Threading;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace WpAltitude
{
    class Program
    {
        private static IHost _host;

        static void Main(string[] args)
        {
            Console.WriteLine("WARNING: You are using this application on your own risk! No guarantees!");

            CreateHost(args);

                // Create a root command with some options
            var rootCommand = new RootCommand
            {
                new Option(
                    "-i",
                    "input *.mission file")
                {
                    
                    Argument = new Argument<FileInfo>()
                },
                new Option(
                    "-o",
                    "Output file name.")
                {
                    Argument = new Argument<FileInfo>()
                },
                new Option(
                    "-minAlt",
                    "Minimum altitude to avoid any obstacles between two waypoints (meters)")
                {
                    Argument = new Argument<int>()
                }
            };

            rootCommand.Description = "Takes a mission file and adjust the \"alt\" parameters to follow ground elevation + user provided alt in the waypoints." ;
            rootCommand.Handler = CommandHandler.Create<FileInfo, FileInfo, int>((i, o, minAlt) =>
            {
                var ca = _host.Services.GetService<ConsoleApp>();
                ca.Process(i, o, minAlt);
            });

            // Parse the incoming args and invoke the handler
            int result = rootCommand.InvokeAsync(args).Result;
        }

        private static void CreateHost(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", optional: false)
                        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                        .AddJsonFile("secrets.json", optional: true, reloadOnChange: false);
                })
                .ConfigureServices((context, builder) =>
                {
                    RegisterServices(context.Configuration, builder);
                });
            _host = hostBuilder.Build();
        }

        private static void RegisterServices(IConfiguration config, IServiceCollection services)
        {
            services.AddLogging(config =>
            {
                config.AddDebug(); // Log to debug (debug window in Visual Studio or any debugger attached)
                config.AddConsole(o =>
                {
                    o.IncludeScopes = false;
                    o.DisableColors = false;
                }); // Log to console (colored !)
            })
           .Configure<LoggerFilterOptions>(options =>
           {
               options.AddFilter<DebugLoggerProvider>(null /* category*/ , LogLevel.Trace /* min level */);
               options.AddFilter<ConsoleLoggerProvider>(null  /* category*/ , LogLevel.Trace /* min level */);

               // Comment this line to see all internal DEM.Net logs
               //options.AddFilter<ConsoleLoggerProvider>("DEM.Net", LogLevel.Information);
           })
           .Configure<AppSecrets>(config.GetSection(nameof(AppSecrets)))
           .Configure<DEMNetOptions>(config.GetSection(nameof(DEMNetOptions)))
           .AddDemNetCore()
           .AddDemNetglTF();

           services.AddTransient<ConsoleApp>();
        }
    }
}
