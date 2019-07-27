using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ
{
    public interface IEventBus
    {
        void Publish(IEvent @event);
        void Subscribe<TEvent, TEventHandler>()
            where TEvent : class,IEvent 
            where TEventHandler : class, IEventHandler;
        void Unsubscribe<TEvent, TEventHandler>()
            where TEvent : class, IEvent
            where TEventHandler : class, IEventHandler;
    }
}
