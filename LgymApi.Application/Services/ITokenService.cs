namespace LgymApi.Application.Services;

public interface ITokenService
{
    string CreateToken(Guid userId, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissionClaims);
}
