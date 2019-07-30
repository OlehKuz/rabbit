
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using STP.Interfaces.Events;

namespace STP.RabbitMq
{

    public class EventBus : IEventBus
    {
        private ConcurrentDictionary<string, Subscription> dictionary
            = new ConcurrentDictionary<string, Subscription>();

        private readonly IConnectionServ _persistentConnection;
        private IModel _consumerChannel;
        private readonly string _exchangeName;
        private readonly string _exchangeType;
        private readonly string _queuename;
        private readonly ILogger<EventBus> _logger;
        private readonly bool _durableQueue;
        public EventBus(IConnectionServ persistentConnection, ILogger<EventBus> logger, string exchangeType, string exchangeName, string queueName, bool durableQueue)
        {
            _persistentConnection = persistentConnection;
            _logger = logger;

            _queuename = string.IsNullOrEmpty(queueName) ? Assembly.GetCallingAssembly().GetName().Name : queueName;
            _durableQueue = durableQueue;
            _exchangeName = exchangeName;
            _exchangeType = exchangeType;
            _consumerChannel = CreateConsumerChannel();
        }

        private IModel CreateConsumerChannel()
        {
            _logger.LogInformation("Creating RabbitMQ consumer channel");
            var consumerChannel = ConnectAndGiveChannel();
            CreateExchange(consumerChannel, _exchangeName, _exchangeType);
            CreateQueue(consumerChannel, _queuename, _durableQueue);
            consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 5, global: false);
            StartConsumingEvents(consumerChannel);
            consumerChannel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning("Recreating RabbitMQ consumer channel");
                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
            };
            return consumerChannel ?? CreateConsumerChannel();
        }

        private IModel ConnectAndGiveChannel()
        {
            _logger.LogInformation("Connecting and returning channel");
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            return _persistentConnection.CreateModel();
        }
        private void CreateExchange(IModel channel, string exchangeName, string exchangeType)
        {
            _logger.LogInformation($"Creating exchange {exchangeName} of type {exchangeType} ");
            channel.ExchangeDeclare(exchange: exchangeName,
                                    type: exchangeType);
        }
        private void CreateQueue(IModel channel, string queuename, bool dureable)
        {
            _logger.LogInformation($"Creating {_queuename} queue");
            channel.QueueDeclare(queue: queuename,
                                 durable: dureable,
                                 exclusive: false,
                                 //auto delete set true or not?
                                 autoDelete: false,
                                 arguments: null);
        }

        private void StartConsumingEvents(IModel consumerChannel)
        {
            var consumer = new EventingBasicConsumer(consumerChannel);
            consumer.Received += async (model, ea) =>
            {
                var eventName = ea.RoutingKey;
                var message = Encoding.UTF8.GetString(ea.Body);
                _logger.LogInformation("Asking to handle the event {EventName} ", eventName);
                await HandleEvent(eventName, message);
                consumerChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                _logger.LogInformation("Event handled");
            };
            consumerChannel.BasicConsume(queue: _queuename,
                 autoAck: false,
                 consumer: consumer);
            _logger.LogInformation("Consumer channel is ready to handle events ");
        }
        private async Task HandleEvent(string eventName, string message)
        {
            if (!dictionary.ContainsKey(eventName))
            {
                return;
            }
            var eventType = dictionary[eventName].EventType;
            var @event = (IEvent)JsonConvert.DeserializeObject(message, eventType);
            var eventHandlers = dictionary[eventName].EventHandlers;
            foreach (var handler in eventHandlers)
            {
                await handler.HandleAsync(@event);
            }
        }
        public void Publish(IEvent @event)
        {
            _logger.LogInformation($"Creating RabbitMQ channel to publish event");
            using (var channel = ConnectAndGiveChannel())
            {
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = _durableQueue; // persistent, nonpersistent
                var routKey = @event.GetType().Name;
                _logger.LogInformation("Publishing event to RabbitMQ: {EventName}", routKey);
                channel.BasicPublish(exchange: _exchangeName,
                                      routingKey: routKey,
                                      basicProperties: properties,
                                      body: body);
            }
        }

        public void Subscribe<TEvent, TEventHandler>()
            where TEvent : IEvent
            where TEventHandler : IEventHandler
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(TEventHandler);
            var handlerInstance = Activator.CreateInstance(handlerType);

            AddToSubscriptionsDictionary(eventType, handlerType, (IEventHandler)handlerInstance);
            _logger.LogInformation("Subscribing to event {EventName} with { EventHandler}", eventType.Name, handlerType.Name);
            _consumerChannel.QueueBind(queue: _queuename,
                                      exchange: _exchangeName,
                                      routingKey: eventType.Name);
        }

        private void AddToSubscriptionsDictionary(Type eventType, Type handlerType, IEventHandler handlerInstance)
        {
            var subscription = dictionary.GetOrAdd(eventType.Name, new Subscription(eventType));
            subscription.AddEventHandler(handlerType, handlerInstance);
        }

        public void Unsubscribe<TEvent, TEventHandler>()
             where TEvent : IEvent
            where TEventHandler : IEventHandler
        {
            using (var channel = ConnectAndGiveChannel())
            {
                var eventName = typeof(TEvent).Name;
                var subscription = dictionary[eventName];
                if (subscription != null) subscription.RemoveEventHandler(typeof(TEventHandler));
                channel.QueueUnbind(
                    queue: _queuename,
                    exchange: _exchangeName,
                    routingKey: eventName
                );
            }
        }

        public void Dispose()
        {
            _logger.LogWarning("Disposing consumer channel ");
            _consumerChannel?.Dispose();
        }

    }
}

