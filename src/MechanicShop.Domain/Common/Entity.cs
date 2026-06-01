namespace MechanicShop.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; }

    private readonly List<DomainEvent> _domainEvent = [];
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvent.AsReadOnly();

    protected Entity() { }

    protected Entity(Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvent.Add(domainEvent);
    }

    public void RemoveDomainEvent(DomainEvent domainEvent)
    {
        _domainEvent.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvent.Clear();
    }
}
