using LgymApi.Application.BuildingBlocks.Errors;
using NUnit.Framework;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class AppErrorTests
{
    [Test]
    public void NotFoundError_ExposesMessageAnd404()
    {
        var error = new NotFoundError("missing");

        Assert.That(error.Message, Is.EqualTo("missing"));
        Assert.That(error.HttpStatusCode, Is.EqualTo(404));
        Assert.That(error.GetPayload(), Is.Null);
    }

    [Test]
    public void BadRequestError_ExposesMessageStatusAndPayload()
    {
        var payload = new { Field = "name" };
        var error = new BadRequestError("invalid", payload);

        Assert.That(error.Message, Is.EqualTo("invalid"));
        Assert.That(error.HttpStatusCode, Is.EqualTo(400));
        Assert.That(error.GetPayload(), Is.EqualTo(payload));
    }

    [Test]
    public void RemainingBuiltInErrors_ExposeExpectedStatusCodes()
    {
        Assert.That(new UnauthorizedError("unauthorized").HttpStatusCode, Is.EqualTo(401));
        Assert.That(new ForbiddenError("forbidden").HttpStatusCode, Is.EqualTo(403));
        Assert.That(new ConflictError("conflict").HttpStatusCode, Is.EqualTo(409));
        Assert.That(new UnprocessableEntityError("unprocessable", new { Code = 1 }).HttpStatusCode, Is.EqualTo(422));
        Assert.That(new InternalServerError("boom").HttpStatusCode, Is.EqualTo(500));
    }
}
