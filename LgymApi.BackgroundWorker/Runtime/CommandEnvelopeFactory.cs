using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LgymApi.Application.Platform.Contracts.BackgroundCommands;
using LgymApi.Application.Platform.Contracts.Serialization;
using LgymApi.Domain.Entities;
using LgymApi.Domain.Enums;
using LgymApi.Domain.ValueObjects;
using ApplicationInvitationAcceptedCommand = LgymApi.Application.Coaching.Contracts.BackgroundCommands.InvitationAcceptedCommand;

namespace LgymApi.BackgroundWorker.Runtime;

internal static class CommandEnvelopeFactory
{
    public static CommandEnvelope Create<TCommand>(
        TCommand command,
        CommandContractRegistry commandContractRegistry)
        where TCommand : class, IActionCommand
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandType = typeof(TCommand);
        var descriptor = commandContractRegistry.DescribeForDispatch(commandType);
        var payloadJson = Serialize(command, commandType, descriptor.CanonicalId);

        return new CommandEnvelope
        {
            Id = Id<CommandEnvelope>.New(),
            CorrelationId = ComputeDeterministicCorrelationId(descriptor.CanonicalId, payloadJson),
            PayloadJson = payloadJson,
            CommandTypeFullName = descriptor.CanonicalId,
            Status = ActionExecutionStatus.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow
        };
    }

    public static Id<CorrelationScope> ComputeDeterministicCorrelationId(string canonicalCommandId, string payloadJson)
    {
        var input = $"{canonicalCommandId}|{payloadJson}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var correlationBytes = new byte[16];
        Array.Copy(hashBytes, correlationBytes, 16);
        return Id<CorrelationScope>.FromBytes(correlationBytes);
    }

    public static string Serialize(object command, Type commandType, string canonicalCommandId)
    {
        return canonicalCommandId == CommandContractRegistry.InvitationAcceptedCanonicalId
            ? SerializeInvitationAcceptedCommand(command)
            : JsonSerializer.Serialize(command, commandType, SharedSerializationOptions.Current);
    }

    private static string SerializeInvitationAcceptedCommand(object command)
    {
        var invitationId = command switch
        {
            ApplicationInvitationAcceptedCommand applicationCommand => applicationCommand.InvitationId,
            _ => throw new InvalidOperationException(
                $"Command '{command.GetType()}' is registered with the InvitationAccepted canonical ID but has incompatible metadata.")
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("invitationId");
            writer.WriteStringValue(invitationId.ToString());
            writer.WriteEndObject();
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
