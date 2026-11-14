using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Api.Features.User.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Identity.Contracts.AccountDeletion;
using LgymApi.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.User.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/account-deletion/recovery")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountRecoveryController(IAccountDeletionService deletion, IMapper mapper) : ControllerBase
{
    [HttpPost("email")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Email([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        await deletion.RequestRecoveryEmailAsync(request.Email, cancellationToken);
        return Ok(mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AccountRecoveryTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        var token = await deletion.SignInWithPasswordAsync(request.Name, request.Password, cancellationToken);
        return Ok(mapper.Map<string, AccountRecoveryTokenResponse>(token));
    }

    [HttpPost("cancel")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel([FromBody] AccountRecoveryTokenRequest request, CancellationToken cancellationToken = default)
    {
        await deletion.CancelAsync(request.RecoveryToken, cancellationToken);
        return Ok(mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ResponseMessageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] AccountRecoveryTokenRequest request, CancellationToken cancellationToken = default)
    {
        await deletion.LogoutRecoveryAsync(request.RecoveryToken, cancellationToken);
        return Ok(mapper.Map<string, ResponseMessageDto>(Messages.Updated));
    }
}
