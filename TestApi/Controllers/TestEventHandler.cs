using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace RabbitMQ
{
    public class TestEventHandler : IEventHandler<TestEvent>
    {

        private int num = 2;
        public void Handle(TestEvent @event) {
            int n = num;
            num = num + 5;
            Debug.WriteLine("Request from other srvice");
            Debug.WriteLine(n);
        }
       
        

    }
}
