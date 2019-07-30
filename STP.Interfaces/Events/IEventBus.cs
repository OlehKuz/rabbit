
namespace STP.Interfaces.Events
{
    public interface IEventBus
    {
        void Publish(IEvent @event, string exchangeName);
        void Subscribe<TEvent, TEventHandler>(string exchangeName)
            where TEvent : IEvent
            where TEventHandler : IEventHandler;
        void Unsubscribe<TEvent, TEventHandler>(string exchangeName)
            where TEvent : IEvent
            where TEventHandler : IEventHandler;
        void CreateExchange(string exchangeName, string exchangeType);
    }
}
