using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ
{
    public interface IEventBus
    {
        void Publish(IEvent @event);
        void Subscribe<TEvent, TEventHandler>()
            where TEvent : IEvent 
            where TEventHandler : IEventHandler;
        void Unsubscribe<TEvent, TEventHandler>()
            where TEvent : IEvent
            where TEventHandler : IEventHandler;
    }
}
