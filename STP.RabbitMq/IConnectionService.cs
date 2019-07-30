using RabbitMQ.Client;
using System;

namespace STP.RabbitMq
{
    public interface IConnectionService : IDisposable
    {

        bool IsConnected { get; }

        bool TryConnect();

        IModel CreateModel();

    }
}
