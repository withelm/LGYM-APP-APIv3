using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.Mapping.Core;
using LgymApi.Application.WorkoutProgress.ExerciseMedia;
using LgymApi.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LgymApi.Api.Features.Exercise.Filters;

internal sealed class ExerciseMediaRequestFilter(IMapper mapper, ExerciseMediaRequestAdmission admission) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        http.RequestAborted.ThrowIfCancellationRequested();
        using var lease = admission.TryAcquire();
        if (!lease.IsAcquired) { context.Result = Failure(503); return; }
        var originalCancellation = http.RequestAborted;
        var originalBody = http.Request.Body;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(originalCancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        http.RequestAborted = deadline.Token;
        http.Request.Body = new BoundedReadStream(originalBody);
        try
        {
            var request = http.Request;
            if (request.ContentLength > ExerciseMediaLimits.MaxRequestBytes) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
            if (!request.HasFormContentType || !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)) { context.Result = Failure(400); return; }
            var form = await request.ReadFormAsync(deadline.Token);
            if (form.Count != 0 || form.Files.Count != 1 || form.Files[0].Name != "file" || form.Files[0].Length == 0) { context.Result = Failure(422); return; }
            if (form.Files[0].Length > ExerciseMediaLimits.MaxBytes) throw new BadHttpRequestException(Messages.ExerciseMediaInvalid, 413);
            var executed = await next();
            if (executed.Exception is OperationCanceledException cancelled) throw cancelled;
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode is 400 or 413) { context.Result = Failure(exception.StatusCode); }
        catch (InvalidDataException) { context.Result = Failure(400); }
        catch (IOException) when (!http.Response.HasStarted && !originalCancellation.IsCancellationRequested) { context.Result = Failure(400); }
        catch (OperationCanceledException) when (!originalCancellation.IsCancellationRequested) { context.Result = Failure(503); }
        finally
        {
            http.RequestAborted = originalCancellation;
            http.Request.Body = originalBody;
        }
    }

    private ObjectResult Failure(int status) => new(mapper.Map<string, ResponseMessageDto>(status == 503 ? Messages.ExerciseMediaUnavailable : Messages.ExerciseMediaInvalid)) { StatusCode = status };

    internal static async Task<byte[]> ReadSourceAsync(IFormFile file, CancellationToken cancellationToken)
    {
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
