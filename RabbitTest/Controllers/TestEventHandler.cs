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
        private string s = "abc";
        public void Handle(TestEvent @event)
        {
            string ss = s;
            s += s;
            Debug.WriteLine("Request starting");
           
            Debug.WriteLine("Writing letters " + ss);
        }
        

    }
}
