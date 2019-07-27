using Serilog;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabbitMQ
{
    
    public class EventBus1 :IEventBus
    {
        private ConcurrentDictionary<string, Subscription> dictionary 
            = new ConcurrentDictionary<string, Subscription>();

        //TODO add acknowledgment(unack messages will be republished)
        //TODO close channel if it is idle
        //TODO subscribe method should take <TEVENT, TEventHandler> and dont take any parameter
       //TODO implement unsubscribe
       //TODO prefetch 10
       //set autodelete for nondurable queues
        private static int closeOpen = 1;
        //private readonly ConcurrentDictionary<IEvent, List<IEventHandler>> _handlers = new Dictionary<IEvent, List<IEventHandler>>();
        //private ConcurrentDictionary<string, Exchange> _exchanges = new ConcurrentDictionary<string, Exchange>();
        private readonly IConnectionServ _persistentConnection;
        private  IModel _consumerChannel;
        private readonly string _exchangeName;
        private readonly string _exchangeType;
        private readonly string _queuename;
        private readonly ILogger<EventBus1> _logger;
        private readonly bool _durableQueue;
        public EventBus1(IConnectionServ persistentConnection, ILogger<EventBus1> logger, string exchangeType, string exchangeName, bool durableQueue)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection)); ;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _queuename = Assembly.GetCallingAssembly().GetName().Name;
            _durableQueue= durableQueue;
            _exchangeName = exchangeName;
            _exchangeType = exchangeType;
            _consumerChannel = CreateConsumerChannel();
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
        private IModel ConnectAndGiveChannel()
        {
            _logger.LogInformation("Connecting and returning channel");
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }
            //TODO
            if (closeOpen % 7 == 0) _consumerChannel.Close();
            return _persistentConnection.CreateModel();
        }
        private IModel CreateConsumerChannel()
        {
            _logger.LogInformation("Creating RabbitMQ consumer channel");
            var consumerChannel = ConnectAndGiveChannel();
            CreateExchange(consumerChannel, _exchangeName, _exchangeType);
            CreateQueue(consumerChannel, _queuename, _durableQueue);
            consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 5, global: false);
            StartConsumingEvents(consumerChannel);
            //TODO
            //if (closeOpen % 2 == 0) _consumerChannel.Dispose();
            consumerChannel.CallbackException += (sender, ea) =>
            {
                _logger.LogWarning("Recreating RabbitMQ consumer channel");
                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
            };
            return consumerChannel??CreateConsumerChannel();
        }

        private void CreateExchange(IModel channel, string exchangeName, string exchangeType)
        {
            _logger.LogInformation($"Creating exchange {exchangeName} of type {exchangeType} ");
            channel.ExchangeDeclare(exchange: exchangeName,
                                    type: exchangeType);
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
                await handler.Handle(@event);
             }
        }
        private void StartConsumingEvents(IModel consumerChannel)
        {
            var consumer = new AsyncEventingBasicConsumer(consumerChannel);
            consumer.Received += async (model, ea) =>
            {
                var eventName = ea.RoutingKey;
                var message = Encoding.UTF8.GetString(ea.Body);
                _logger.LogInformation("Asking to handle the event {EventName} ", eventName);
                await HandleEvent(eventName,message);
                consumerChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                _logger.LogInformation("Event handled");
            };
            consumerChannel.BasicConsume(queue: _queuename,
                 autoAck: false,
                 consumer: consumer);
            _logger.LogInformation("Consumer channel is ready to handle events ");
        }
        public void Publish(IEvent @event)
        {
            //TODO
            // if (closeOpen % 3 == 0) _consumerChannel.Dispose();
            //closeOpen++;
            _logger.LogInformation($"Creating RabbitMQ channel to publish event:");// {EventId} ({EventName})", @event.Id, eventName);
            using (var channel = ConnectAndGiveChannel())
            {
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);
                //TODO
                if (closeOpen % 4 == 0) _consumerChannel.Dispose();
                var properties = channel.CreateBasicProperties();
                properties.Persistent = _durableQueue; // persistent
                var routKey = @event.GetType().Name;
                _logger.LogInformation("Publishing event to RabbitMQ: {EventId}");//, @event.Id);
                channel.BasicPublish(exchange: _exchangeName,
                                      routingKey: routKey,
                                      basicProperties: properties,
                                      body: body);
            }  
        }

        private void AddToSubscriptionsDictionary(Type eventType, Type handlerType, IEventHandler handlerInstance)
            
        {
            var subscription = dictionary.GetOrAdd(eventType.Name, new Subscription(eventType));
            subscription.AddEventHandler(handlerType, handlerInstance);
        }

        public void Subscribe<TEvent, TEventHandler> ()
            where TEvent : class, IEvent
            where TEventHandler : class, IEventHandler<TEvent>
        {
            var eventType = typeof(TEvent);
            var handlerType = typeof(TEventHandler);
            IEventHandler<TEvent> handlerInstance = (IEventHandler<TEvent>) Activator.CreateInstance(handlerType);
            AddToSubscriptionsDictionary(eventType,handlerType, handlerInstance);
            _logger.LogInformation("Subscribing to event {EventName} with { EventHandler}", eventType.Name, handlerType.Name);
            _consumerChannel.QueueBind(queue: _queuename,
                                      exchange: _exchangeName,
                                      routingKey: eventType.Name); 
        }

        public void Dispose()
        {
            _logger.LogWarning("Disposing consumer channel ");
            _consumerChannel?.Dispose();
        }

        public void Unsubscribe<TEvent, TEventHandler>()
             where TEvent : class, IEvent
            where TEventHandler : class, IEventHandler<TEvent>
        {
            throw new NotImplementedException();
        }
    }
}

