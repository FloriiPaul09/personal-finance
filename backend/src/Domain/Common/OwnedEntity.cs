namespace PersonalFinance.Domain.Common;

/// <summary>
/// Base of every user-owned entity: identifier generated at creation (UUIDv7, known before saving), owner fixed at
/// creation, technical timestamps set by the SaveChanges interceptor (ADR-0010).
/// </summary>
public abstract class OwnedEntity : IOwnedByUser
{
    protected OwnedEntity(Guid userId)
        : this(userId, id: null)
    {
    }

    /// <summary>
    /// With an identifier chosen by the caller: a client that works offline (PWA) sends the same id again after a
    /// lost response, and the API recognizes the duplicate (ADR-0017). Null generates one.
    /// </summary>
    protected OwnedEntity(Guid userId, Guid? id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException(DomainErrors.UserRequired, "An owner is required.");
        }

        if (id == Guid.Empty)
        {
            throw new DomainException(DomainErrors.IdInvalid, "An identifier cannot be empty.");
        }

        Id = id ?? Guid.CreateVersion7();
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
