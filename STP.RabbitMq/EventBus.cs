
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
        private readonly ILogger<EventBus> _logger;
        private IModel _consumerChannel;
        private readonly string _queuename;
        private readonly bool _durableQueue;
        public EventBus(IConnectionServ persistentConnection, ILogger<EventBus> logger, string queueName, bool durableQueue)
        {
            _persistentConnection = persistentConnection;
            _logger = logger;
            _queuename = string.IsNullOrEmpty(queueName) ? Assembly.GetCallingAssembly().GetName().Name : queueName;
            _durableQueue = durableQueue;
            _consumerChannel = SetUpConsumerChannel();
        }

        private IModel SetUpConsumerChannel()
        {
            _logger.LogInformation("Starting setup of RabbitMQ consumer channel");
            var consumerChannel = CreateRabbitMqChannel();
            CreateQueue(consumerChannel, _queuename, _durableQueue);
            consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 5, global: false);
            StartConsumingEvents(consumerChannel);
            consumerChannel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning("Recreating RabbitMQ consumer channel");
                _consumerChannel.Dispose();
                _consumerChannel = SetUpConsumerChannel();
            };
            return consumerChannel ?? SetUpConsumerChannel();
        }

        private IModel CreateRabbitMqChannel()
        {
            _logger.LogInformation("Creating RabbitMq consumer channel");
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            return _persistentConnection.CreateModel();
        }
        
        private void CreateQueue(IModel channel, string queuename, bool dureable)
        {
            _logger.LogInformation($"Creating {queuename} queue");
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
        public void Publish(IEvent @event, string exchangeName, string exchangeType)
        {
            _logger.LogInformation($"Creating RabbitMQ channel to publish event");
            using (var channel = CreateRabbitMqChannel())
            {
                CreateExchange(exchangeName, exchangeType);
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true; // persistent, nonpersistent
                var routKey = @event.GetType().Name;
                _logger.LogInformation("Publishing event to RabbitMQ: {EventName}", routKey);
                channel.BasicPublish(exchange: exchangeName,
                                      routingKey: routKey,
                                      basicProperties: properties,
                                      body: body);
            }
        }

        public void Subscribe<TEvent, TEventHandler>(string exchangeName, string exchangeType)
            where TEvent : IEvent
            where TEventHandler : IEventHandler
        {
            CreateExchange(exchangeName, exchangeType);
            var eventType = typeof(TEvent);
            var handlerType = typeof(TEventHandler);
            var handlerInstance = Activator.CreateInstance(handlerType);

            AddToSubscriptionsDictionary(eventType, handlerType, (IEventHandler)handlerInstance);
            _logger.LogInformation("Subscribing to event {EventName} with { EventHandler}", eventType.Name, handlerType.Name);
            _consumerChannel.QueueBind(queue: _queuename,
                                      exchange: exchangeName,
                                      routingKey: eventType.Name);
        }

        private void AddToSubscriptionsDictionary(Type eventType, Type handlerType, IEventHandler handlerInstance)
        {
            var subscription = dictionary.GetOrAdd(eventType.Name, new Subscription(eventType));
            subscription.AddEventHandler(handlerType, handlerInstance);
        }

        public void Unsubscribe<TEvent, TEventHandler>(string exchangeName)
             where TEvent : IEvent
            where TEventHandler : IEventHandler
        {
            using (var channel = CreateRabbitMqChannel())
            {
                var eventName = typeof(TEvent).Name;
                var subscription = dictionary[eventName];
                if (subscription != null) subscription.RemoveEventHandler(typeof(TEventHandler));
                channel.QueueUnbind(
                    queue: _queuename,
                    exchange: exchangeName,
                    routingKey: eventName
                );
            }
        }

        private void CreateExchange(string exchangeName, string exchangeType)
        {
            using (var channel = CreateRabbitMqChannel())
            {
                _logger.LogInformation($"Creating exchange {exchangeName} of type {exchangeType} ");
                channel.ExchangeDeclare(exchange: exchangeName,
                                        type: exchangeType);
            }
        }
        public void Dispose()
        {
            _logger.LogWarning("Disposing consumer channel ");
            _consumerChannel?.Dispose();
        }
    }
}

