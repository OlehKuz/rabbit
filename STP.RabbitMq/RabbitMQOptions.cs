using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace STP.RabbitMq
{
    public class RabbitMQOptions
    {
        //
        // Summary:
        //     Sets or gets the AMQP Uri to be used for connections.
        public Uri Uri { get; set; }
        //
        // Summary:
        //     Virtual host to access during this connection.
        public string VirtualHost { get; set; } = "/";
        //
        // Summary:
        //     Username to use when authenticating to the server.
        public string UserName { get; set; } = "oleh";
        //
        // Summary:
        //     Password to use when authenticating to the server.
        public string Password { get; set; } = "smuggler339";




        /// <summary> The topic exchange type. </summary>
        public const string ExchangeType = "direct";

        /// <summary>
        /// Topic exchange name when declare a topic exchange.
        /// </summary>
        public string ExchangeName { get; set; }

        /// <summary>
        /// The port to connect on.
        /// </summary>
        public int Port { get; set; } = -1;
    }
}
