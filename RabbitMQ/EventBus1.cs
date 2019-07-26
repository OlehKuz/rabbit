using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace RabbitMQ
{
    public class EventBus1 :IEventBus
    {
        //private readonly ConcurrentDictionary<IEvent, List<IEventHandler>> _handlers = new Dictionary<IEvent, List<IEventHandler>>();
        //private ConcurrentDictionary<string, Exchange> _exchanges = new ConcurrentDictionary<string, Exchange>();
        private readonly IConnectionServ _persistentConnection;
        private  IModel _consumerChannel;
        private readonly string _exchangeName;
        private readonly string _quename;
        private readonly ILogger _logger;
      
        public EventBus1(IConnectionServ persistentConnection, ILogger<EventBus1> logger, string exchangeType, string exchangeName, bool durable)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection)); ;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _quename = Assembly.GetCallingAssembly().GetName().Name;         
            _exchangeName = exchangeName;
            _consumerChannel = CreateConsumerChannel();
            CreateQueue( durable);
            CreateExchange(_exchangeName, exchangeType);
        }
        public void CreateQueue(bool dureable)
        {
            _consumerChannel.QueueDeclare(_quename, dureable);
            var properties = _consumerChannel.CreateBasicProperties();
            properties.Persistent = dureable;
            _consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        private IModel CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            _logger.LogTrace("Creating RabbitMQ consumer channel");
         
            var channel = _persistentConnection.CreateModel();
            channel.CallbackException += (sender, ea) =>
            {

                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
            };
            return channel;
            //TODO channel.ExchangeDeclare
          
            //TODO channel.CallbackException "Recreating RabbitMQ consumer channel");
        }

        public void CreateExchange(string exchangeName, string exchangeType)
        {
            _consumerChannel.ExchangeDeclare(exchange: exchangeName,
                                    type: exchangeType);
        }

        public void Publish(IEvent @event)
        {
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);
            //var properties = _consumerChannel.CreateBasicProperties();
            //properties.DeliveryMode = 2; // persistent
            var routKey = @event.GetType().Name;
            //_logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);
            _consumerChannel.BasicPublish(exchange: _exchangeName,
                                 routingKey: routKey,
                                 //basicProperties: properties,
                                 body: body); ;
        }

        public void Subscribe<TEvent> (IEventHandler<TEvent> eventHandler ) where TEvent:IEvent
        {
         
            var routType = typeof(TEvent);
            var routKey = routType.Name;
          
            _consumerChannel.QueueBind(queue: _quename,
                                  exchange: _exchangeName,
                                  routingKey: routKey);


    

            var consumer = new EventingBasicConsumer(_consumerChannel);
        consumer.Received += (model, ea) =>
            {
                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body);
                var receivedEvent = JsonConvert.DeserializeObject(message, routType);
                var routingKey = ea.RoutingKey;
                eventHandler.Handle((TEvent)receivedEvent);

            };
            _consumerChannel.BasicConsume(queue: _quename,
                                 autoAck: false,
                                 consumer: consumer);
        }


    }
}

