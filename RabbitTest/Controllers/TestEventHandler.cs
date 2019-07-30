using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STP.Interfaces.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ
{
    public class TestEventHandler : IMessageHandler
    {
        //private static string s = "abc";
        public async Task HandleAsync(IMessage @event)
        {
            //string ss = s;
           // s += s;
            Debug.WriteLine("Request starting");

            Debug.WriteLine("Writing letters ");// + ss);
        }
        

    }
}
