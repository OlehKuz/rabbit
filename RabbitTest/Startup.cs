using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ;
using Serilog;

namespace RabbitTest
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;

        }
        protected IHostingEnvironment HostingEnvironment { get; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            UseSerilog(services);
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddSingleton<IConnectionServ, ConnectionService>();
            services.AddSingleton<IEventBus, EventBus1>(sp =>
            {
                var connection = sp.GetRequiredService<IConnectionServ>();
                var logger = sp.GetRequiredService<ILogger<EventBus1>>();

                return new EventBus1(connection, logger, "direct",
                    "TestExchange", true);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog();

            app.UseMvc();
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();

            eventBus.Subscribe(new TestEventHandler());
            var testEvent = new TestEvent();
            for(int i = 0; i< 30; i++) eventBus.Publish(testEvent);
         
        }
        private void UseSerilog(IServiceCollection services)
        {
            var path = Path.Combine(HostingEnvironment.ContentRootPath, "Config", "serilogConfig.json");
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Config"))
            .AddJsonFile("serilogConfig.json")
            .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog());
        }
    }
}
