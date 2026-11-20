using LgymApi.Api.Extensions;
using LgymApi.Api.Features.Common.Contracts;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

using Unit = LgymApi.Application.BuildingBlocks.Results.Unit;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ResultExtensionsTests
{
    [Test]
    public void ToActionResult_GenericSuccess_ReturnsOkObjectResultWithValue()
    {
        var result = Result<int, AppError>.Success(42);

        var actionResult = result.ToActionResult();

        Assert.That(actionResult, Is.TypeOf<OkObjectResult>());
        Assert.That(((OkObjectResult)actionResult).Value, Is.EqualTo(42));
    }

    [Test]
    public void ToActionResult_GenericFailureWithPayload_ReturnsPayloadAndStatusCode()
    {
        var payload = new { error = "validation", field = "name" };
        var result = Result<int, AppError>.Failure(new BadRequestError("ignored", payload));

        var actionResult = result.ToActionResult();

        Assert.That(actionResult, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)actionResult;
        Assert.That(objectResult.StatusCode, Is.EqualTo(400));
        Assert.That(objectResult.Value, Is.EqualTo(payload));
    }

    [Test]
    public void ToActionResult_GenericFailureWithoutPayload_ReturnsResponseMessageDtoAndStatusCode()
    {
        const string message = "not found";
        var result = Result<int, AppError>.Failure(new NotFoundError(message));

        var actionResult = result.ToActionResult();

        Assert.That(actionResult, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)actionResult;
        Assert.That(objectResult.StatusCode, Is.EqualTo(404));
        Assert.That(objectResult.Value, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)objectResult.Value!).Message, Is.EqualTo(message));
    }

    [Test]
    public void ToActionResult_UnitSuccess_ReturnsOkResult()
    {
        var result = Result<Unit, AppError>.Success(Unit.Value);

        var actionResult = result.ToActionResult();

        Assert.That(actionResult, Is.TypeOf<OkResult>());
    }

    [Test]
    public void ToActionResult_UnitFailureWithoutPayload_ReturnsResponseMessageDtoAndStatusCode()
    {
        const string message = "forbidden";
        var result = Result<Unit, AppError>.Failure(new ForbiddenError(message));

        var actionResult = result.ToActionResult();

        Assert.That(actionResult, Is.TypeOf<ObjectResult>());
        var objectResult = (ObjectResult)actionResult;
        Assert.That(objectResult.StatusCode, Is.EqualTo(403));
        Assert.That(objectResult.Value, Is.TypeOf<ResponseMessageDto>());
        Assert.That(((ResponseMessageDto)objectResult.Value!).Message, Is.EqualTo(message));
    }
}
