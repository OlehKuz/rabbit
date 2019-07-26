using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ
{
    public interface IEventBus
    {
        void Publish(IEvent @event);

        void Subscribe<TEvent>(IEventHandler<TEvent> eventHandler) where TEvent : IEvent;
           
    }
}
