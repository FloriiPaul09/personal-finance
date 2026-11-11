namespace PersonalFinance.Domain.Common;

/// <summary>
/// Base of every user-owned entity: identifier generated at creation (UUIDv7, known before saving), owner fixed at
/// creation, technical timestamps set by the SaveChanges interceptor (ADR-0010).
/// </summary>
public abstract class OwnedEntity : IOwnedByUser
{
    protected OwnedEntity(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(DomainErrors.UserRequired, "An owner is required.");
        }

        Id = Guid.CreateVersion7();
        UserId = userId;
    }

    /// <summary>Used by EF Core when materializing entities.</summary>
    protected OwnedEntity()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    protected void EnsureSameUser(IOwnedByUser other)
    {
        if (other.UserId != UserId)
        {
            throw new DomainException(DomainErrors.DifferentUser, "Data of different users cannot be combined.");
        }
    }
}
