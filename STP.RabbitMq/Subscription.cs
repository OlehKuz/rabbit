using STP.Interfaces.Events;
using System;
using System.Collections.Generic;

namespace STP.RabbitMq
{
    public sealed class Subscription
    {
        public Type MessageType { get; }
        private HashSet<Type> messageHandlerTypes = new HashSet<Type>();
        public List<IMessageHandler> MessageHandlers { get; } = new List<IMessageHandler>();

        public Subscription(Type messageType)
        {
            MessageType = messageType;
        }

        public void AddMessageHandler(Type handlerType, IMessageHandler handler)
        {
            if (messageHandlerTypes.Contains(handlerType)) return;
            messageHandlerTypes.Add(handlerType);
            MessageHandlers.Add(handler);
        }

        public void RemoveMessageHandler(Type handlerType)
        {
            if (messageHandlerTypes.Contains(handlerType))
            {
                MessageHandlers.RemoveAll(e => e.GetType() == handlerType);
                messageHandlerTypes.Remove(handlerType);
            }
        }
    }
}
