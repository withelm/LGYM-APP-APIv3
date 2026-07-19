using FluentAssertions;
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

        Directory.Exists(commandsDirectory).Should().BeTrue();
        Directory.GetFiles(commandsDirectory, "*.cs")
            .Select(Path.GetFileName)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .Should().Equal(expected.Select(command => command.FileName).OrderBy(fileName => fileName, StringComparer.Ordinal));

        foreach (var command in expected)
        {
            var sourcePath = Path.Combine(commandsDirectory, command.FileName);
            File.Exists(sourcePath).Should().BeTrue();

            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetCompilationUnitRoot();
            root.Members.OfType<BaseNamespaceDeclarationSyntax>().Single().Name.ToString().Should().Be(CommandsNamespace);

            var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
            declaration.Identifier.ValueText.Should().Be(command.TypeName);
            declaration.Modifiers.Select(modifier => modifier.ValueText).Should().Equal("public", "sealed");
            declaration.BaseList!.Types.Select(baseType => baseType.Type.ToString()).Should().Equal("IActionCommand");
            declaration.AttributeLists.Should().BeEmpty();
            declaration.Members.OfType<ConstructorDeclarationSyntax>().Should().BeEmpty();
            declaration.Members.OfType<PropertyDeclarationSyntax>().Select(property => property.Identifier.ValueText)
                .Should().Equal(command.PropertyNames);
        }
    }

    private static readonly ExpectedCommand[] ExpectedCommands =
    [
        new("InvitationCreatedCommand.cs", "InvitationCreatedCommand", ["InvitationId"]),
        new("InvitationAcceptedCommand.cs", "InvitationAcceptedCommand", ["InvitationId"]),
        new("InvitationRevokedCommand.cs", "InvitationRevokedCommand", ["InvitationId"]),
        new("TrainerInvitationCreatedInAppNotificationCommand.cs", "TrainerInvitationCreatedInAppNotificationCommand", ["InvitationId", "TraineeId", "TrainerId"]),
        new("TrainerInvitationAcceptedInAppNotificationCommand.cs", "TrainerInvitationAcceptedInAppNotificationCommand", ["InvitationId", "TrainerId", "TraineeId"]),
        new("TrainerInvitationRejectedInAppNotificationCommand.cs", "TrainerInvitationRejectedInAppNotificationCommand", ["InvitationId", "TrainerId", "TraineeId"]),
        new("TrainerRelationshipEndedInAppNotificationCommand.cs", "TrainerRelationshipEndedInAppNotificationCommand", ["TrainerId", "TraineeId"]),
        new("TraineeNoteUpdatedInAppNotificationCommand.cs", "TraineeNoteUpdatedInAppNotificationCommand", ["TraineeNoteId", "TraineeId", "TrainerId", "NoteTitle", "TriggeredAt"])
    ];

    private sealed record ExpectedCommand(string FileName, string TypeName, string[] PropertyNames);
}
