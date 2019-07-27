using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ
{
    public interface IEventHandler
    {
        Task Handle(IEvent @event);
    }
}
