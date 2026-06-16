namespace Moe.SharedKernel.Domain;

public abstract class Entity<TId> where TId : notnull
{
    protected Entity(TId id) => Id = id;
    public TId Id { get; protected set; }
}
