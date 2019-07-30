using System.Threading.Tasks;

namespace STP.Interfaces.Events
{
    public interface IMessageHandler
    {
        Task HandleAsync(IMessage message);
    }
}
