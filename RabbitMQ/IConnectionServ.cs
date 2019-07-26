using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ
{
    public interface IConnectionServ : IDisposable
    {
       
        bool IsConnected { get; }

        bool TryConnect();

        IModel CreateModel();
        
    }
}
