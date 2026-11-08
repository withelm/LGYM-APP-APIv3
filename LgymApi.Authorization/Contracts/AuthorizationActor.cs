using LgymApi.Domain.ValueObjects;
using LgymApi.Platform.Contracts;

namespace LgymApi.Authorization.Contracts;

public sealed record AuthorizationActor
{
    public AuthorizationActor(Id<ActorReference> id)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("Authorization actors require a non-empty identifier.", nameof(id));
        }

        Id = id;
    }

    public Id<ActorReference> Id { get; }
}
