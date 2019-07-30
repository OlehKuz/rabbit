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
using STP.Interfaces.Events;
using STP.RabbitMq;

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
            services.AddSingleton<IConnectionService, ConnectionService>();
            services.AddSingleton<IMessageBus, MessageBus>(sp =>
            {
                var connection = sp.GetRequiredService<IConnectionService>();
                var logger = sp.GetRequiredService<ILogger<MessageBus>>();

                return new MessageBus(connection, logger,  "",false);
            });
            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog();

            app.UseMvc();
            var eventBus = app.ApplicationServices.GetRequiredService<IMessageBus>();
            //eventBus.Subscribe<TestEvent, TestEventHandler>("TestExchange", "direct");
            eventBus.Unsubscribe<TestEvent, TestEventHandler>("TestExchange");
            eventBus.Subscribe<TestEvent, TestEventHandler>("Exchange2", RabbitMqExchangeType.DirectExchange);
            var testEvent = new TestEvent();
            for(int i = 0; i< 20; i++) eventBus.Publish(testEvent, "TestExchange", RabbitMqExchangeType.DirectExchange);
            for (int i = 0; i < 20; i++) eventBus.Publish(testEvent, "Exchange2", RabbitMqExchangeType.DirectExchange);

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
