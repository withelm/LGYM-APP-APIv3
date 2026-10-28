using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Payments.Contracts;
using LgymApi.Api.Idempotency;
using LgymApi.Api.Middleware;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Payments.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentCheckoutEntity = LgymApi.Domain.Entities.PaymentCheckout;

namespace LgymApi.Api.Features.Payments.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentsApi payments, IMapper mapper) : ControllerBase
{
    private const int MaximumWebhookBodyBytes = 256 * 1024;

    [HttpPost("checkout")]
    [Authorize]
    [ApiIdempotency("/api/payments/checkout", ApiIdempotencyScopeSource.AuthenticatedUser)]
    [ProducesResponseType(typeof(PaymentCheckoutResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCheckout([FromBody] CreatePaymentCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var account = HttpContext.GetAuthenticatedAccountContext()!;
        var result = await payments.CreateCheckoutAsync(
            new PaymentAccountReference(account.Id),
            Request.Headers[ApiIdempotencyHeaders.IdempotencyKey].ToString(),
            cancellationToken);
        return Created($"/api/payments/checkouts/{result.CheckoutId}", mapper.Map<PaymentCheckoutStartResult, PaymentCheckoutResponse>(result));
    }

    [HttpGet("checkouts/{checkoutId}")]
    [Authorize]
    [ProducesResponseType(typeof(PaymentCheckoutStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCheckoutStatus([FromRoute] string checkoutId, CancellationToken cancellationToken = default)
    {
        var query = mapper.Map<PaymentCheckoutStatusRequest, Id<PaymentCheckoutEntity>>(new PaymentCheckoutStatusRequest { CheckoutId = checkoutId });
        if (query.IsEmpty)
        {
            return NotFound();
        }

        var account = HttpContext.GetAuthenticatedAccountContext()!;
        var result = await payments.GetCheckoutStatusAsync(new PaymentAccountReference(account.Id), query, cancellationToken);
        return result is null ? NotFound() : Ok(mapper.Map<PaymentCheckoutStatusResult, PaymentCheckoutStatusResponse>(result));
    }

    [HttpPost("webhooks/stripe")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> HandleStripeWebhook(CancellationToken cancellationToken = default)
    {
        if (Request.ContentLength > MaximumWebhookBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = await ReadWebhookBodyAsync(cancellationToken);
        if (body is null)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var outcome = await payments.HandleWebhookAsync(body, Request.Headers["Stripe-Signature"].ToString(), cancellationToken);
        return outcome switch
        {
            PaymentWebhookResult.BadRequest => BadRequest(),
            PaymentWebhookResult.PayloadTooLarge => StatusCode(StatusCodes.Status413PayloadTooLarge),
            _ => Ok()
        };
    }

    private async Task<byte[]?> ReadWebhookBodyAsync(CancellationToken cancellationToken)
    {
        await using var body = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (body.Length + read > MaximumWebhookBodyBytes)
            {
                return null;
            }

            await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return body.ToArray();
    }
}
