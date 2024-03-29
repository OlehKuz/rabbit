﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RabbitMQ
{
    public sealed class Subscription <TEvent> where TEvent:IEvent
    {
        public Type EventType { get; }
        private HashSet<Type> handlerTypes  = new HashSet<Type>();
        public List<IEventHandler<TEvent>> EventHandlers { get; } = new List<IEventHandler<TEvent>>();

        public Subscription(Type eventType)
        {
            EventType = eventType;
        }

        public void AddEventHandler<TEvent>(Type handlerType, IEventHandler<TEvent> handler) where TEvent:IEvent
        {
            //if (handlerTypes.Any(tp=>tp == handlerType)) return;
            if (handlerTypes.Contains(handlerType)) return;
            handlerTypes.Add(handlerType);
            EventHandlers.Add(handler);
        }

        public void RemoveEventHandler(Type handlerType)
        {
            if (handlerTypes.Contains(handlerType))
            {
                EventHandlers.RemoveAll(e=>e.GetType()==handlerType);
                handlerTypes.Remove(handlerType);
            }
        }
    }
}
