using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ
{
    public class TestEventHandler : IEventHandler<TestEvent>
    {

        private static int num = 2;
      
        public async Task Handle(TestEvent @event)
        {
            //int n = num;
            //num = num + 5;
            Debug.WriteLine("Request from other srvice");
            Debug.WriteLine(num);
        }
    }
}
