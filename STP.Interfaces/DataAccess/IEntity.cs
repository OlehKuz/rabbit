namespace STP.Interfaces
{
    public interface IEntity<TKey>
    {
        TKey Id { get; }
    }
}
