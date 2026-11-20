using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class ReportRequestTests
{
    [Test]
    public void Constructor_DefaultsStatusToPending()
    {
        var request = new ReportRequest();

        Assert.That(request.Status, Is.EqualTo(ReportRequestStatus.Pending));
    }
}
