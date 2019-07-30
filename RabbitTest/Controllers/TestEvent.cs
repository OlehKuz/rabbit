using STP.Interfaces.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ
{
    public class TestEvent:IMessage
    {
        public string name { get; set; } = "name of some event ";
    }
}
