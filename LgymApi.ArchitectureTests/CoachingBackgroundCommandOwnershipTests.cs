using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingBackgroundCommandOwnershipTests
{
    private const string CommandsDirectory = "LgymApi.Application/Coaching/Contracts/BackgroundCommands";
    private const string CommandsNamespace = "LgymApi.Application.Coaching.Contracts.BackgroundCommands";

    [Test]
    public void Coaching_Background_Commands_Have_Exact_Owner_Path_Namespace_And_Declaration_Shape()
    {
        var repoRoot = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var commandsDirectory = Path.Combine(repoRoot, CommandsDirectory);
        var expected = ExpectedCommands;

        Assert.That(Directory.Exists(commandsDirectory), Is.True);
        Assert.That(Directory.GetFiles(commandsDirectory, "*.cs")
            .Select(Path.GetFileName)
            .OrderBy(fileName => fileName, StringComparer.Ordinal), Is.EqualTo(expected.Select(command => command.FileName).OrderBy(fileName => fileName, StringComparer.Ordinal)));

        foreach (var command in expected)
        {
            var sourcePath = Path.Combine(commandsDirectory, command.FileName);
            Assert.That(File.Exists(sourcePath), Is.True);

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetCompilationUnitRoot();
            Assert.That(root.Members.OfType<BaseNamespaceDeclarationSyntax>().Single().Name.ToString(), Is.EqualTo(CommandsNamespace));

            var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
            Assert.That(declaration.Identifier.ValueText, Is.EqualTo(command.TypeName));
            Assert.That(declaration.Modifiers.Select(modifier => modifier.ValueText), Is.EqualTo(new[] { "public", "sealed" }));
            Assert.That(declaration.BaseList!.Types.Select(baseType => baseType.Type.ToString()), Is.EqualTo(new[] { "IActionCommand" }));
            Assert.That(declaration.AttributeLists, Is.Empty);
            Assert.That(declaration.Members.OfType<ConstructorDeclarationSyntax>(), Is.Empty);
            var properties = declaration.Members.OfType<PropertyDeclarationSyntax>().ToArray();
            Assert.That(properties.Select(property => property.Identifier.ValueText), Is.EqualTo(command.Properties.Select(property => property.Name)));
            Assert.That(properties.Select(property => property.Type.ToString()), Is.EqualTo(command.Properties.Select(property => property.Type)));
            Assert.Multiple(() => { foreach (var accessors in properties.Select(property => property.AccessorList!.Accessors.Select(accessor => accessor.Keyword.ValueText))) { Assert.That(accessors, Is.EqualTo(new[] { "get", "init" })); } });
        }
    }

    private static readonly ExpectedCommand[] ExpectedCommands =
    [
        new("InvitationCreatedCommand.cs", "InvitationCreatedCommand", [new("InvitationId", "Id<TrainerInvitation>")]),
        new("InvitationAcceptedCommand.cs", "InvitationAcceptedCommand", [new("InvitationId", "Id<TrainerInvitation>")]),
        new("InvitationRevokedCommand.cs", "InvitationRevokedCommand", [new("InvitationId", "Id<TrainerInvitation>")]),
        new("RelationshipEndedReportCancellationCommand.cs", "RelationshipEndedReportCancellationCommand", [new("EpisodeId", "Id<CoachingEpisode>")]),
        new("TrainerInvitationCreatedInAppNotificationCommand.cs", "TrainerInvitationCreatedInAppNotificationCommand", [new("InvitationId", "Id<TrainerInvitation>"), new("TraineeId", "Id<User>"), new("TrainerId", "Id<User>")]),
        new("TrainerInvitationAcceptedInAppNotificationCommand.cs", "TrainerInvitationAcceptedInAppNotificationCommand", [new("InvitationId", "Id<TrainerInvitation>"), new("TrainerId", "Id<User>"), new("TraineeId", "Id<User>")]),
        new("TrainerInvitationRejectedInAppNotificationCommand.cs", "TrainerInvitationRejectedInAppNotificationCommand", [new("InvitationId", "Id<TrainerInvitation>"), new("TrainerId", "Id<User>"), new("TraineeId", "Id<User>")]),
        new("TrainerRelationshipEndedInAppNotificationCommand.cs", "TrainerRelationshipEndedInAppNotificationCommand", [new("TrainerId", "Id<User>"), new("TraineeId", "Id<User>")]),
        new("TraineeNoteUpdatedInAppNotificationCommand.cs", "TraineeNoteUpdatedInAppNotificationCommand", [new("TraineeNoteId", "Id<TraineeNote>"), new("TraineeId", "Id<User>"), new("TrainerId", "Id<User>"), new("NoteTitle", "string?"), new("TriggeredAt", "DateTimeOffset")])
    ];

    private sealed record ExpectedCommand(string FileName, string TypeName, ExpectedProperty[] Properties);

    private sealed record ExpectedProperty(string Name, string Type);
}
