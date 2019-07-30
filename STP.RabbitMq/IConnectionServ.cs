using RabbitMQ.Client;
using System;

namespace STP.RabbitMq
{
    public interface IConnectionServ : IDisposable
    {

        bool IsConnected { get; }

        bool TryConnect();

        IModel CreateModel();

    }
}
