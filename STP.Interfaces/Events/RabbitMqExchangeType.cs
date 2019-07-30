using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace STP.Interfaces.Events
{
    public enum RabbitMqExchangeType
    {
        NotValidType,
        DirectExchange,
        FanoutExchange
    }
}
