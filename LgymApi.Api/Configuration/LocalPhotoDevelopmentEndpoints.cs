using LgymApi.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LgymApi.Api.Configuration;

public static class LocalPhotoDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapLocalPhotoDevelopmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dev/photos");
        group.AllowAnonymous();
        group.MapPut("/upload/{**storageKey}", UploadAsync);
        group.MapGet("/read/{**storageKey}", ReadAsync);
        return endpoints;
    }

    public static async Task<Results<NotFound, BadRequest, NoContent>> UploadAsync(
        string storageKey,
        HttpRequest request,
        LocalPhotoDevelopmentStore store,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return TypedResults.BadRequest();
        }

        var decodedStorageKey = Uri.UnescapeDataString(storageKey);
        await store.SaveAsync(decodedStorageKey, request.Body, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Results<NotFound, BadRequest, FileContentHttpResult>> ReadAsync(
        string storageKey,
        LocalPhotoDevelopmentStore store,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return TypedResults.BadRequest();
        }

        var decodedStorageKey = Uri.UnescapeDataString(storageKey);
        var fileBytes = await store.ReadAsync(decodedStorageKey, cancellationToken);
        if (fileBytes == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(fileBytes, store.ResolveContentType(decodedStorageKey));
    }
}
