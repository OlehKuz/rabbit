using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ
{
    public interface IEventHandler<in T> where T : IEvent 
    {
        void Handle(T @event);
    }
}
