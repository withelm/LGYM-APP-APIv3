using System.Reflection;
using LgymApi.Application.Coaching;
using LgymApi.Application.Coaching.Invitations.Accept;
using LgymApi.Application.Coaching.Invitations.Create;
using LgymApi.Application.Coaching.Invitations.CreateByEmail;
using LgymApi.Application.Coaching.Invitations.List;
using LgymApi.Application.Coaching.Invitations.ListPaginated;
using LgymApi.Application.Coaching.Invitations.PublicStatus;
using LgymApi.Application.Coaching.Invitations.Reject;
using LgymApi.Application.Coaching.Invitations.Revoke;
using Microsoft.Extensions.DependencyInjection;

namespace LgymApi.ArchitectureTests;

[TestFixture]
public sealed class CoachingInvitationSliceArchitectureTests
{
    private static readonly (Type Contract, string ImplementationName)[] Slices =
    [
        (typeof(ICreateInvitationUseCase), "LgymApi.Application.Coaching.Invitations.Create.CreateInvitationUseCase"),
        (typeof(ICreateInvitationByEmailUseCase), "LgymApi.Application.Coaching.Invitations.CreateByEmail.CreateInvitationByEmailUseCase"),
        (typeof(IListInvitationsUseCase), "LgymApi.Application.Coaching.Invitations.List.ListInvitationsUseCase"),
        (typeof(IListPaginatedInvitationsUseCase), "LgymApi.Application.Coaching.Invitations.ListPaginated.ListPaginatedInvitationsUseCase"),
        (typeof(IPublicInvitationStatusUseCase), "LgymApi.Application.Coaching.Invitations.PublicStatus.PublicInvitationStatusUseCase"),
        (typeof(IAcceptInvitationUseCase), "LgymApi.Application.Coaching.Invitations.Accept.AcceptInvitationUseCase"),
        (typeof(IRejectInvitationUseCase), "LgymApi.Application.Coaching.Invitations.Reject.RejectInvitationUseCase"),
        (typeof(IRevokeInvitationUseCase), "LgymApi.Application.Coaching.Invitations.Revoke.RevokeInvitationUseCase")
    ];

    [Test]
    public void InvitationSlices_ExposeOnlyPublicContractsWithInternalImplementations()
    {
        var assembly = typeof(ICreateInvitationUseCase).Assembly;

        foreach (var slice in Slices)
        {
            Assert.That(slice.Contract.IsPublic, Is.True);
            Assert.That(slice.Contract.GetMethods(BindingFlags.Public | BindingFlags.Instance), Has.Exactly(1).Items);
            Assert.That(assembly.GetType(slice.ImplementationName)!.IsNotPublic, Is.True);
        }
    }

    [Test]
    public void InvitationSlices_AreRegisteredExactlyOnceByTheCoachingModule()
    {
        var services = new ServiceCollection();
        services.AddCoachingModule();

        foreach (var slice in Slices)
        {
            Assert.That(services.Count(descriptor => descriptor.ServiceType == slice.Contract), Is.EqualTo(1));
        }
    }

    [Test]
    public void InvitationSlices_DoNotReintroduceLegacyUserOrRelationshipRepositories()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var invitationDirectory = Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations");
        var source = Directory.GetFiles(invitationDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        Assert.That((source).Any(text => text.Contains("IUserRepository", StringComparison.Ordinal)), Is.False);
        Assert.That((source).Any(text => text.Contains("ITrainerRelationshipRepository", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void InvitationCreateSlices_DelegateWriteModelTransformationToTheRegisteredMapper()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var createUseCases = new[]
        {
            Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "Create", "CreateInvitationUseCase.cs"),
            Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "CreateByEmail", "CreateInvitationByEmailUseCase.cs")
        };
        var mappingProfile = Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "InvitationMappingProfile.cs");

        foreach (var useCase in createUseCases)
        {
            var source = File.ReadAllText(useCase);
            Assert.That(source, Does.Not.Contain("new CoachingInvitationWriteModel("));
            Assert.That(source, Does.Contain("_mapper.Map<InvitationCreationSource, CoachingInvitationWriteModel>("));
            Assert.That(source, Does.Contain("_mapper.CreateContext()"));
        }

        Assert.That(File.ReadAllText(mappingProfile), Does.Contain("CreateMap<InvitationCreationSource, CoachingInvitationWriteModel>"));
    }

    [Test]
    public void InvitationLifecycleSlices_DelegateResponseUpdatesToTheRegisteredMapper()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var lifecycleUseCases = new[]
        {
            Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "Accept", "AcceptInvitationUseCase.cs"),
            Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "Reject", "RejectInvitationUseCase.cs"),
            Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "Revoke", "RevokeInvitationUseCase.cs")
        };
        var mappingProfile = Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "InvitationMappingProfile.cs");

        foreach (var useCase in lifecycleUseCases)
        {
            var source = File.ReadAllText(useCase);
            Assert.That(source, Does.Not.Contain("new CoachingInvitationResponseUpdateModel("));
            Assert.That(source, Does.Contain("_mapper.Map<InvitationResponseSource, CoachingInvitationResponseUpdateModel>("));
            Assert.That(source, Does.Contain("_mapper.CreateContext()"));
        }

        Assert.That(File.ReadAllText(mappingProfile), Does.Contain("CreateMap<InvitationResponseSource, CoachingInvitationResponseUpdateModel>"));
    }

    [Test]
    public void InvitationAcceptSlice_DelegatesActiveLinkWriteTransformationToTheRegisteredMapper()
    {
        var root = ArchitectureTestHelpers.ResolveRepositoryRoot();
        var useCase = Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "Accept", "AcceptInvitationUseCase.cs");
        var mappingProfile = Path.Combine(root, "LgymApi.Application", "Coaching", "Invitations", "InvitationMappingProfile.cs");
        var source = File.ReadAllText(useCase);

        Assert.That(source, Does.Not.Contain("new CoachingActiveLinkWriteModel("));
        Assert.That(source, Does.Contain("_mapper.Map<InvitationActiveLinkSource, CoachingActiveLinkWriteModel>("));
        Assert.That(source, Does.Contain("_mapper.CreateContext()"));
        Assert.That(File.ReadAllText(mappingProfile), Does.Contain("CreateMap<InvitationActiveLinkSource, CoachingActiveLinkWriteModel>"));
    }
}
