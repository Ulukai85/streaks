namespace Api.Infrastructure;

public interface ICurrentUserProvider
{
    Guid UserId { get; }
}
