using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.Api.Features.Trainer.Controllers;

[ApiController]
[AllowAnonymous]
[Route("dev/photos")]
public sealed class LocalPhotoDevelopmentController : ControllerBase
{
    private readonly LocalPhotoDevelopmentStore _store;
    private readonly IWebHostEnvironment _environment;

    public LocalPhotoDevelopmentController(LocalPhotoDevelopmentStore store, IWebHostEnvironment environment)
    {
        _store = store;
        _environment = environment;
    }

    [HttpPut("upload/{**storageKey}")]
    public async Task<IActionResult> Upload([FromRoute] string storageKey, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return BadRequest();
        }

        var decodedStorageKey = Uri.UnescapeDataString(storageKey);
        await _store.SaveAsync(decodedStorageKey, Request.Body, cancellationToken);
        return NoContent();
    }

    [HttpGet("read/{**storageKey}")]
    public async Task<IActionResult> Read([FromRoute] string storageKey, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return BadRequest();
        }

        var decodedStorageKey = Uri.UnescapeDataString(storageKey);
        var fileBytes = await _store.ReadAsync(decodedStorageKey, cancellationToken);
        if (fileBytes == null)
        {
            return NotFound();
        }

        return File(fileBytes, _store.ResolveContentType(decodedStorageKey));
    }
}
