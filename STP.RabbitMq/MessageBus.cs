
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

    public class MessageBus : IMessageBus
    {
        private ConcurrentDictionary<string, Subscription> dictionary = new ConcurrentDictionary<string, Subscription>();

        private readonly IConnectionService _persistentConnection;
        private readonly ILogger<MessageBus> _logger;
        private IModel _consumerChannel;
        private readonly string _queuename;
        private readonly bool _durableQueue;
        public MessageBus(IConnectionService persistentConnection, ILogger<MessageBus> logger, string queueName, bool durableQueue)
        {
            _persistentConnection = persistentConnection;
            _logger = logger;
            _queuename = string.IsNullOrEmpty(queueName) ? Assembly.GetCallingAssembly().GetName().Name : queueName;
            _durableQueue = durableQueue;
            _consumerChannel = SetUpConsumerChannel();
        }
        private string ConvertToRabbitMqExchangeType(RabbitMqExchangeType customExchangeType)
        {
            switch (customExchangeType)
            {
                case RabbitMqExchangeType.DirectExchange:
                    return ExchangeType.Direct;
                case RabbitMqExchangeType.FanoutExchange:
                    return ExchangeType.Fanout;
                default:
                    throw new ArgumentException($"No corresponding RabbitMq exchange type exists for {customExchangeType}", nameof(RabbitMqExchangeType));
            }
            
        }
        private IModel SetUpConsumerChannel()
        {
            _logger.LogInformation("Starting setup of RabbitMQ consumer channel");
            var consumerChannel = CreateRabbitMqChannel();
            CreateQueue(consumerChannel, _queuename, _durableQueue);
            consumerChannel.BasicQos(prefetchSize: 0, prefetchCount: 5, global: false);
            StartConsumingMessages(consumerChannel);
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

        private void StartConsumingMessages(IModel consumerChannel)
        {
            var consumer = new EventingBasicConsumer(consumerChannel);
            consumer.Received += async (model, ea) =>
            {
                var messageName = ea.RoutingKey;
                var message = Encoding.UTF8.GetString(ea.Body);
                _logger.LogInformation("Asking to handle the message {MessageName} ", messageName);
                await HandleMessage(messageName, message);
                consumerChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                _logger.LogInformation("Message handled");
            };
            consumerChannel.BasicConsume(queue: _queuename,
                 autoAck: false,
                 consumer: consumer);
            _logger.LogInformation("Consumer channel is ready to handle messages ");
        }
        private async Task HandleMessage(string messageName, string message)
        {
            if (dictionary.TryGetValue(messageName, out Subscription subscription))
            {
                var messageType = subscription.MessageType;
                var messageToSend = (IMessage)JsonConvert.DeserializeObject(message, messageType);
                var messageHandlers = subscription.MessageHandlers;
                foreach (var handler in messageHandlers)
                {
                    await handler.HandleAsync(messageToSend);
                }
            }
        }
        public void Publish(IMessage messageToSend, string exchangeName, RabbitMqExchangeType exchangeType)
        {
            _logger.LogInformation($"Creating RabbitMQ channel to publish message");
            using (var channel = CreateRabbitMqChannel())
            {
                CreateExchange(exchangeName, exchangeType);
                var message = JsonConvert.SerializeObject(messageToSend);
                var body = Encoding.UTF8.GetBytes(message);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true; // persistent, nonpersistent
                var routKey = messageToSend.GetType().Name;
                _logger.LogInformation("Publishing message to RabbitMQ: {MessageName}", routKey);
                channel.BasicPublish(exchange: exchangeName,
                                      routingKey: routKey,
                                      basicProperties: properties,
                                      body: body);
            }
        }

        public void Subscribe<TMessage, TMessageHandler>(string exchangeName, RabbitMqExchangeType exchangeType)
            where TMessage : IMessage
            where TMessageHandler : IMessageHandler
        {
            CreateExchange(exchangeName, exchangeType);
            var messageType = typeof(TMessage);
            var handlerType = typeof(TMessageHandler);
            var handlerInstance = Activator.CreateInstance(handlerType);

            AddToSubscriptionsDictionary(messageType, handlerType, (IMessageHandler)handlerInstance);
            _logger.LogInformation("Subscribing to message {MessageName} with { MessageHandler}", messageType.Name, handlerType.Name);
            _consumerChannel.QueueBind(queue: _queuename,
                                      exchange: exchangeName,
                                      routingKey: messageType.Name);
        }

        private void AddToSubscriptionsDictionary(Type messageType, Type handlerType, IMessageHandler handlerInstance)
        {
            var subscription = dictionary.GetOrAdd(messageType.Name, new Subscription(messageType));
            subscription.AddMessageHandler(handlerType, handlerInstance);
        }

        public void Unsubscribe<TMessage, TMessageHandler>(string exchangeName)
             where TMessage : IMessage
            where TMessageHandler : IMessageHandler
        {
            using (var channel = CreateRabbitMqChannel())
            {
                var messageName = typeof(TMessage).Name;
                if (dictionary.TryGetValue(messageName, out Subscription subscription))
                {
                    subscription.RemoveMessageHandler(typeof(TMessageHandler));
                }
                channel.QueueUnbind(
                    queue: _queuename,
                    exchange: exchangeName,
                    routingKey: messageName);
            }
        }

        private void CreateExchange(string exchangeName, RabbitMqExchangeType localExchangeType)
        {
            using (var channel = CreateRabbitMqChannel())
            {
                var exchangeType = ConvertToRabbitMqExchangeType(localExchangeType);
                _logger.LogInformation($"Creating exchange {exchangeName} of type {exchangeType} ");
                channel.ExchangeDeclare(exchange: exchangeName,
                                        type: exchangeType,
                                        // do we want it to be durable?
                                        durable:true);
            }
        }
        public void Dispose()
        {
            _logger.LogWarning("Disposing consumer channel ");
            _consumerChannel?.Dispose();
        }
    }
}

