using Serilog;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RabbitMQ
{
    
    public class EventBus1 :IEventBus
    {

        //TODO add acknowledgment(unack messages will be republished)
        //TODO close channel if it is idle
        //TODO subscribe method should take <TEVENT, TEventHandler> and dont take any parameter
       //TODO implement unsubscribe
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
            _logger.LogInformation($"Creating {_queuename} microservice queue");
            channel.QueueDeclare(queuename, dureable);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = dureable;
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            
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

            CreateExchange(consumerChannel,_exchangeName, _exchangeType);
            CreateQueue(consumerChannel,_queuename, _durableQueue);
            //TODO
            if (closeOpen % 3 == 0) _consumerChannel.Dispose();
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

        public void Publish(IEvent @event)
        {
            
            //TODO
            closeOpen++;
            _logger.LogInformation($"Creating RabbitMQ channel to publish event:");// {EventId} ({EventName})", @event.Id, eventName);
            var channel = ConnectAndGiveChannel();
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);
            if (closeOpen % 4 == 0) _consumerChannel.Dispose();
            //var properties = _consumerChannel.CreateBasicProperties();
            //properties.DeliveryMode = 2; // persistent
            var routKey = @event.GetType().Name;
            _logger.LogInformation("Publishing event to RabbitMQ: {EventId}");//, @event.Id);
           channel.BasicPublish(exchange: _exchangeName,
                                 routingKey: routKey,
                                 //basicProperties: properties,
                                 body: body);
        }

        public void Subscribe<TEvent> (IEventHandler<TEvent> eventHandler ) where TEvent:IEvent
        {
            if (closeOpen % 5 == 0) _consumerChannel.Dispose();
            var channel = ConnectAndGiveChannel();
            var routType = typeof(TEvent);
            var routKey = routType.Name;
            _logger.LogInformation("Subscribing to event {EventName}", routKey);//with {EventHandler}", , typeof(TH).GetGenericTypeName());
            channel.QueueBind(queue: _queuename,
                                  exchange: _exchangeName,
                                  routingKey: routKey);

            var consumer = new EventingBasicConsumer(_consumerChannel);
            consumer.Received += async(model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                var receivedEvent = JsonConvert.DeserializeObject(message, routType);
                var routingKey = ea.RoutingKey;
                _logger.LogInformation("Asking to handle the event ");// {EventName}", routKey);
                await eventHandler.Handle((TEvent)receivedEvent);
                _logger.LogInformation("Event handled");
            };
            if (closeOpen % 5 == 0) _consumerChannel.Dispose();
            channel.BasicConsume(queue: _queuename,
                                 autoAck: false,
                                 consumer: consumer);
        }
        public void Dispose()
        {
            _logger.LogWarning("Disposing consumer channel ");
            _consumerChannel?.Dispose();
        }


    }
}

