using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace RabbitMQ
{
    public class EventBus
    {
        //private readonly ConcurrentDictionary<IEvent, List<IEventHandler>> _handlers = new Dictionary<IEvent, List<IEventHandler>>();
        //private ConcurrentDictionary<string, Exchange> _exchanges = new ConcurrentDictionary<string, Exchange>();
        private readonly IConnectionServ _persistentConnection;
        private readonly IModel _consumerChannel;
        private readonly string _exchangeName;
        private readonly string _quename;
      
        EventBus(IConnectionServ persistentConnection, string exchangeType, string exchangeName)
        {
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection)); ;
            _quename = Assembly.GetCallingAssembly().GetName().Name;
            _exchangeName = exchangeName;
            _consumerChannel = CreateConsumerChannel();
            CreateExchange(_consumerChannel, _exchangeName, exchangeType);
        }

        /* private class Exchange
         {
             private readonly string _exchangeName;
             //better enum
             private readonly string _exchangeType;
             public Exchange(string exchangeName, string exchangeType)
             {
                 _exchangeName = exchangeName;
                 _exchangeType = exchangeType;
             }

             public void Publish(IEvent)//IEvent<IE> @event) 
             {
                 if (!_connection.IsConnected)
                 {
                     _connection.TryConnect();
                 }
                 channel.BasicPublish(exchange: "direct_logs",
                                  routingKey: severity,
                                  basicProperties: null,
                                  body: body);
             }
         }*/

        public void CreateQueue(IModel channel, bool dureable)
        {
            channel.QueueDeclare(_quename, dureable);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = dureable;
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        private IModel CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            //_logger.LogTrace("Creating RabbitMQ consumer channel");
         
            var channel = _persistentConnection.CreateModel();
           
            return channel;
            //TODO channel.ExchangeDeclare
          
            //TODO channel.CallbackException "Recreating RabbitMQ consumer channel");
        }

        public void CreateExchange(IModel channel, string exchangeName, string exchangeType)
        {
            channel.ExchangeDeclare(exchange: exchangeName,
                                    type: exchangeType);
        }

        public void Publish(IModel channel, IEvent @event, string exchangeName)
        {
            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);


            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // persistent
            var routKey = @event.GetType().Name;
            //_logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);
            channel.BasicPublish(exchange: exchangeName,
                                 routingKey: routKey,
                                 basicProperties: null,
                                 body: body); ;
        }

        public void Subscribe(IEvent @event, string exchangeName)
        {
            var routType = @event.GetType();
            var routKey = routType.Name;
            _consumerChannel.QueueBind(queue: _quename,
                                  exchange: exchangeName,
                                  routingKey: routKey);


        Console.WriteLine(" [*] Waiting for messages.");

            var consumer = new EventingBasicConsumer(_consumerChannel);
        consumer.Received += (model, ea) =>
            {
                var routingKey = ea.RoutingKey;
                var body = ea.Body;
                var @Event = Encoding.UTF8.GetString(body);
                var receivedEvent = JsonConvert.DeserializeObject(@Event, routType);
               // HandleAsync(receivedEvent);
            };
            _consumerChannel.BasicConsume(queue: _quename,
                                 autoAck: false,
                                 consumer: consumer);
        }
    }
}

