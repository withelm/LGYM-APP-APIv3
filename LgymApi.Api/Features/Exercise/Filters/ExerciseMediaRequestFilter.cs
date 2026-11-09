using LgymApi.Api.Features.Exercise.Contracts;
using LgymApi.Api.Middleware;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Application.WorkoutProgress.Errors;
using LgymApi.Domain.ValueObjects;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LgymApi.Api.Features.Exercise.Filters;

internal sealed class ExerciseMediaRequestFilter(IExerciseMediaService service, IMapper mapper) : IAsyncResourceFilter, IAsyncAlwaysRunResultFilter, IAsyncExceptionFilter
{
    internal const string OperationHeader = "X-Exercise-Media-Operation-Id";
    private static readonly SemaphoreSlim Intake = new(2);
    private static readonly HashSet<string> CreateFields = new(StringComparer.OrdinalIgnoreCase) { "operationId", "name", "bodyPart", "eloFormulaId", "description", "sourceSha256", "contentType", "sizeBytes" };
    private static readonly HashSet<string> ChangeFields = new(StringComparer.OrdinalIgnoreCase) { "operationId", "expectedImage", "remove", "restoreImage", "sourceSha256", "contentType", "sizeBytes" };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Path.Value?.EndsWith("/media/cleanup", StringComparison.Ordinal) == true) { await next(); return; }
        var operationHeader = http.Request.Headers[OperationHeader].ToString();
        if (!Id<ExerciseMediaOperationReference>.TryParse(operationHeader, out var operationId) || operationId.IsEmpty)
        {
            context.Result = Failure(operationHeader, 400, ExerciseMediaOperationError.Unresolved);
            return;
        }
        if (!await Intake.WaitAsync(0, http.RequestAborted))
        {
            context.Result = Failure(operationId.ToString(), 503, ExerciseMediaOperationError.Unresolved);
            return;
        }
        var originalCancellation = http.RequestAborted;
        var originalBody = http.Request.Body;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(originalCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        http.RequestAborted = deadline.Token;
        http.Request.Body = new BoundedReadStream(originalBody);
        try { await ExecuteBoundedAsync(context, next, operationId); }
        catch (BadHttpRequestException exception) when (exception.StatusCode is 400 or 413)
        { context.Result = await RejectionAsync(http, operationId, exception.StatusCode); }
        catch (InvalidDataException) { context.Result = await RejectionAsync(http, operationId, 400); }
        catch (IOException) when (!http.Response.HasStarted && !originalCancellation.IsCancellationRequested)
        { context.Result = await RejectionAsync(http, operationId, 400); }
        catch (OperationCanceledException) when (!originalCancellation.IsCancellationRequested)
        { context.Result = Failure(operationId.ToString(), 503, ExerciseMediaOperationError.Unresolved); }
        finally
        {
            http.RequestAborted = originalCancellation;
            http.Request.Body = originalBody;
            Intake.Release();
        }
    }

    private async Task ExecuteBoundedAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, Id<ExerciseMediaOperationReference> operationId)
    {
        var request = context.HttpContext.Request;
        if (request.ContentLength > ExerciseMediaLimits.MaxRequestBytes) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
        if (!request.HasFormContentType)
        {
            context.Result = await RejectionAsync(context.HttpContext, operationId, 400);
            return;
        }
        var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
        var change = HttpMethods.IsPatch(request.Method);
        var allowedFields = change ? ChangeFields : CreateFields;
        if (form.Any(field => !allowedFields.Contains(field.Key) || field.Value.Count != 1)
            || !Id<ExerciseMediaOperationReference>.TryParse(form["operationId"].ToString(), out var bodyId) || bodyId != operationId || change && !form.ContainsKey("expectedImage"))
        {
            context.Result = await RejectionAsync(context.HttpContext, operationId, 400);
            return;
        }
        var declaresSource = form.ContainsKey("sourceSha256") || form.ContainsKey("contentType") || form.ContainsKey("sizeBytes");
        if (declaresSource && (string.IsNullOrEmpty(form["sourceSha256"]) || string.IsNullOrEmpty(form["contentType"]) || string.IsNullOrEmpty(form["sizeBytes"])))
        {
            context.Result = await RejectionAsync(context.HttpContext, operationId, 400);
            return;
        }
        if (form.Files.Count > 1 || form.Files.Any(file => file.Name != "file"))
        {
            context.Result = await RejectionAsync(context.HttpContext, operationId, 422);
            return;
        }
        if (form.Files.Any(file => file.Length > ExerciseMediaLimits.MaxBytes)) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
        var executed = await next();
        if (executed.Exception is OperationCanceledException cancelled) throw cancelled;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is BadHttpRequestException { StatusCode: 400 or 413 } failure
            && Id<ExerciseMediaOperationReference>.TryParse(context.HttpContext.Request.Headers[OperationHeader].ToString(), out var operationId)
            && !operationId.IsEmpty)
        {
            context.Result = await RejectionAsync(context.HttpContext, operationId, failure.StatusCode);
            context.ExceptionHandled = true;
        }
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { StatusCode: 400 or 413 or 422 } rejection
            && !context.HttpContext.Response.HasStarted
            && Id<ExerciseMediaOperationReference>.TryParse(context.HttpContext.Request.Headers[OperationHeader].ToString(), out var operationId)
            && !operationId.IsEmpty)
            context.Result = await RejectionAsync(context.HttpContext, operationId, rejection.StatusCode.Value);
        await next();
    }

    private async Task<IActionResult> RejectionAsync(HttpContext http, Id<ExerciseMediaOperationReference> operationId, int statusCode)
    {
        var rejected = await service.RejectAsync(http.GetAuthenticatedAccountContext(), operationId, statusCode, http.RequestAborted);
        return Failure(operationId.ToString(), rejected ? statusCode : 503, rejected ? ExerciseMediaOperationError.Rejected : ExerciseMediaOperationError.Unresolved);
    }

    private IActionResult Failure(string operationId, int statusCode, string disposition) => new ObjectResult(
        mapper.Map<ExerciseMediaFailureResult, ExerciseMediaErrorDto>(new(operationId, statusCode == 503 ? Messages.ExerciseMediaRetry : Messages.ExerciseMediaInvalid, disposition))) { StatusCode = statusCode };

    internal static async Task<byte[]?> ReadSourceAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null) return null;
        if (file.Length is <= 0 or > ExerciseMediaLimits.MaxBytes) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > ExerciseMediaLimits.MaxBytes) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (output.Length != file.Length) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 400);
        return output.ToArray();
    }

    private sealed class BoundedReadStream(Stream source) : Stream
    {
        private long _received;
        private int Count(int read)
        {
            _received += read;
            if (_received > ExerciseMediaLimits.MaxRequestBytes) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
            return read;
        }
        public override int Read(byte[] buffer, int offset, int count) => Count(source.Read(buffer, offset, count));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Count(await source.ReadAsync(buffer, cancellationToken));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Count(await source.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
