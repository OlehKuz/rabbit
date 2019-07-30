
namespace STP.Interfaces.Events
{
    public interface IMessageBus
    {
        void Publish(IMessage message, string exchangeName, RabbitMqExchangeType exchangeType);
        void Subscribe<TMessage, TMessageHandler>(string exchangeName, RabbitMqExchangeType exchangeType)
            where TMessage : IMessage
            where TMessageHandler : IMessageHandler;
        void Unsubscribe<TMessage, TMessageHandler>(string exchangeName)
            where TMessage : IMessage
            where TMessageHandler : IMessageHandler;
    }
}
