using System.Threading.Tasks;

namespace STP.Interfaces.Events
{
    public interface IEventHandler
    {
        Task HandleAsync(IEvent @event);
    }
}
