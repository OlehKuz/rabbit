
namespace STP.Interfaces.Events
{
    public interface IEventBus
    {
        void Publish(IEvent @event, string exchangeName, string exchangeType);
        void Subscribe<TEvent, TEventHandler>(string exchangeName, string exchangeType)
            where TEvent : IEvent
            where TEventHandler : IEventHandler;
        void Unsubscribe<TEvent, TEventHandler>(string exchangeName)
            where TEvent : IEvent
            where TEventHandler : IEventHandler;
    }
}
