using System.Reflection;
using LgymApi.BackgroundWorker.Common;
using LgymApi.BackgroundWorker.Common.Notifications;
using LgymApi.BackgroundWorker.Common.Notifications.Models;
using LgymApi.Domain.Entities;
using LgymApi.Domain.ValueObjects;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class EmailWireContractManifestTests
{
    private static readonly EmailPayloadContract[] ExpectedPayloads =
    [
        Contract<WelcomeEmailPayload>(
            ("UserId", typeof(Id<User>)),
            ("UserName", typeof(string)),
            ("RecipientEmail", typeof(string)),
            ("CultureName", typeof(string))),
        Contract<InvitationEmailPayload>(
            ("InvitationId", typeof(Id<TrainerInvitation>)),
            ("InvitationCode", typeof(string)),
            ("ExpiresAt", typeof(DateTimeOffset)),
            ("TrainerName", typeof(string)),
            ("RecipientEmail", typeof(string)),
            ("CultureName", typeof(string)),
            ("PreferredTimeZone", typeof(string))),
        Contract<InvitationAcceptedEmailPayload>(
            ("InvitationId", typeof(Id<TrainerInvitation>)),
            ("TrainerName", typeof(string)),
            ("TraineeName", typeof(string)),
            ("RecipientEmail", typeof(string)),
            ("CultureName", typeof(string)),
            ("PreferredTimeZone", typeof(string))),
        Contract<InvitationRevokedEmailPayload>(
            ("InvitationId", typeof(Id<TrainerInvitation>)),
            ("TrainerName", typeof(string)),
            ("RecipientEmail", typeof(string)),
            ("CultureName", typeof(string)),
            ("PreferredTimeZone", typeof(string))),
        Contract<TrainingCompletedEmailPayload>(
            ("UserId", typeof(Id<User>)),
            ("TrainingId", typeof(Id<Training>)),
            ("RecipientEmail", typeof(string)),
            ("CultureName", typeof(string)),
            ("PreferredTimeZone", typeof(string)),
            ("PlanDayName", typeof(string)),
            ("TrainingDate", typeof(DateTimeOffset)),
            ("Exercises", typeof(IReadOnlyList<TrainingExerciseSummary>))),
        Contract<PasswordRecoveryEmailPayload>(
            ("UserId", typeof(Id<User>)),
            ("TokenId", typeof(Id<PasswordResetToken>)),
            ("UserName", typeof(string)),
            ("RecipientEmail", typeof(string)),
            ("ResetToken", typeof(string)),
            ("ResetUrl", typeof(string)),
            ("CultureName", typeof(string)))
    ];

    [Test]
    public void CommonEmailWire_ContainsExactSixPayloadTypesAndSchemas()
    {
        var action = () => Validate(ExpectedPayloads);

        Assert.That(() => { action(); }, Throws.Nothing);
        Assert.That(typeof(IEmailPayload).Assembly.GetName().Name, Is.EqualTo("LgymApi.BackgroundWorker.Common"));
        foreach (var contract in ExpectedPayloads)
        {
            Assert.That(contract.PayloadType.Assembly, Is.SameAs(typeof(IEmailPayload).Assembly));
            Assert.That(contract.PayloadType.Namespace, Is.EqualTo("LgymApi.BackgroundWorker.Common.Notifications.Models"));
            Assert.That(contract.PayloadType.IsPublic, Is.True);
            Assert.That(contract.PayloadType.IsSealed, Is.True);
            Assert.That((contract.PayloadType).GetInterfaces(), Does.Contain(typeof(IEmailPayload)));
        }
    }

    [Test]
    public void CommonEmailWire_RejectsMissingAlteredTypeAndAlteredSchemaFixtures()
    {
        var missing = () => Validate(ExpectedPayloads[..^1]);
        var alteredType = () => Validate([
            .. ExpectedPayloads[..^1],
            ExpectedPayloads[^1] with { PayloadType = typeof(EmailMessage) }
        ]);
        var alteredSchema = () => Validate([
            ExpectedPayloads[0] with
            {
                WritableProperties =
                [
                    .. ExpectedPayloads[0].WritableProperties,
                    new PropertyContract("UnexpectedField", typeof(string))
                ]
            },
            .. ExpectedPayloads[1..]
        ]);

        Assert.That(() => { missing(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("The Common email wire must contain exactly six payload types.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(() => { alteredType(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("The Common email payload type manifest was altered.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
        Assert.That(() => { alteredSchema(); }, Throws.InstanceOf<InvalidOperationException>().With.Message.Matches("(?is)\\A" + System.Text.RegularExpressions.Regex.Escape("Common email payload schema mismatch for '*'.").Replace("\\*", ".*").Replace("\\?", ".") + "\\z"));
    }

    [Test]
    public void CommonEmailMessage_AndIdempotencyKeyRemainStable()
    {
        Assert.That(GetWritableProperties(typeof(EmailMessage)), Is.EquivalentTo(new[] { new PropertyContract("To", typeof(string)), new PropertyContract("Subject", typeof(string)), new PropertyContract("Body", typeof(string)), new PropertyContract("IsHtml", typeof(bool)) }));
        Assert.That(Id<CorrelationScope>.TryParse("11111111-1111-1111-1111-111111111111", out var correlationId), Is.True);
        Assert.That(IdempotencyKeyPolicy.CalculateKey(correlationId.ToString()), Is.EqualTo("11111111-1111-1111-1111-111111111111"));
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(
            "11111111-1111-1111-1111-111111111111",
            "11111111-1111-1111-1111-111111111111"), Is.True);
        Assert.That(IdempotencyKeyPolicy.AreKeysEqual(
            "11111111-1111-1111-1111-111111111111",
            "11111111-1111-1111-1111-11111111111A"), Is.False);
    }

    private static void Validate(IReadOnlyList<EmailPayloadContract> contracts)
    {
        if (contracts.Count != 6)
        {
            throw new InvalidOperationException("The Common email wire must contain exactly six payload types.");
        }

        if (!contracts.Select(contract => contract.PayloadType).ToHashSet()
            .SetEquals(ExpectedPayloads.Select(contract => contract.PayloadType)))
        {
            throw new InvalidOperationException("The Common email payload type manifest was altered.");
        }

        foreach (var contract in contracts)
        {
            if (!GetWritableProperties(contract.PayloadType).ToHashSet()
                .SetEquals(contract.WritableProperties))
            {
                throw new InvalidOperationException(
                    $"Common email payload schema mismatch for '{contract.PayloadType.FullName}'.");
            }
        }
    }

    private static EmailPayloadContract Contract<TPayload>(
        params (string Name, Type Type)[] properties) =>
        new(typeof(TPayload), properties.Select(property =>
            new PropertyContract(property.Name, property.Type)).ToArray());

    private static PropertyContract[] GetWritableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(property => property.SetMethod != null)
            .Select(property => new PropertyContract(property.Name, property.PropertyType))
            .ToArray();

    private sealed record EmailPayloadContract(
        Type PayloadType,
        IReadOnlyList<PropertyContract> WritableProperties);

    private sealed record PropertyContract(string Name, Type Type);
}
