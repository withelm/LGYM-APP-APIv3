using System.Reflection;
using LgymApi.Api;
using LgymApi.Application.BuildingBlocks.Errors;
using LgymApi.Application.BuildingBlocks.Results;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Api.Features.Role.Controllers;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.Role.Models;
using LgymApi.Application.Identity.ApiAdapters;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
using LgymApi.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RoleControllerTests
{
    private static readonly List<string> ManageUserRolesClaim = ["users.roles.manage"];

    [Test]
    public async Task CreateRole_ReturnsMappedRoleDto()
    {
        var roleId = Id<RoleReference>.New();
        var fakeService = new StubRoleService
        {
            CreateRoleHandler = (_, _, _) => Task.FromResult(Result<RoleProjection, AppError>.Success(new RoleProjection(roleId, "Coach", "desc", ManageUserRolesClaim)))
        };
        var controller = new RoleController(fakeService, BuildMapper());

        var action = await controller.CreateRole(new UpsertRoleRequest
        {
            Name = "Coach",
            Description = "desc",
            PermissionClaims = ManageUserRolesClaim
        });

        var ok = action as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var dto = ok!.Value as RoleDto;

        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Id, Is.EqualTo($"{roleId:N}"));
        Assert.That(dto.Name, Is.EqualTo("Coach"));
        Assert.That(dto.PermissionClaims, Is.EquivalentTo(ManageUserRolesClaim).UsingPropertiesComparer());
    }

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(LgymApi.Api.Mapping.MappingAssemblyMarkers.All);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
    }

    [Test]
    public async Task UpdateUserRoles_DelegatesParsedRequestAndCancellationTokenToRoleService()
    {
        var userId = Id<AccountReference>.New();
        var request = new UpdateUserRolesRequest { Roles = ["Coach", "Analyst"] };
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var observedUserId = Id<AccountReference>.Empty;
        IReadOnlyCollection<string>? observedRoleNames = null;
        var observedCancellationToken = default(CancellationToken);
        var fakeService = new StubRoleService
        {
            UpdateUserRolesHandler = (receivedUserId, receivedRoleNames, receivedCancellationToken) =>
            {
                observedUserId = receivedUserId;
                observedRoleNames = receivedRoleNames;
                observedCancellationToken = receivedCancellationToken;
                return Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));
            }
        };
        var controller = new RoleController(fakeService, BuildMapper());

        var action = await controller.UpdateUserRoles($"{userId:N}", request, cancellationToken);

        Assert.That(action, Is.TypeOf<OkObjectResult>());
        Assert.That(observedUserId, Is.EqualTo(userId));
        Assert.That(observedRoleNames, Is.EqualTo(request.Roles));
        Assert.That(observedCancellationToken, Is.EqualTo(cancellationToken));
    }

    [Test]
    public void Routes_UseGetAndPostOnly_ForRoleManagement()
    {
        var type = typeof(RoleController);
        var update = type.GetMethod(nameof(RoleController.UpdateRole), BindingFlags.Public | BindingFlags.Instance);
        var delete = type.GetMethod(nameof(RoleController.DeleteRole), BindingFlags.Public | BindingFlags.Instance);
        var updateUserRoles = type.GetMethod(nameof(RoleController.UpdateUserRoles), BindingFlags.Public | BindingFlags.Instance);

        Assert.That(update, Is.Not.Null);
        Assert.That(delete, Is.Not.Null);
        Assert.That(updateUserRoles, Is.Not.Null);

        var controllerRoute = type.GetCustomAttribute<RouteAttribute>();
        var updateHttpPost = update!.GetCustomAttribute<HttpPostAttribute>();
        var deleteHttpPost = delete!.GetCustomAttribute<HttpPostAttribute>();
        var updateUserRolesHttpPost = updateUserRoles!.GetCustomAttribute<HttpPostAttribute>();

        Assert.That(controllerRoute, Is.Not.Null);
        Assert.That(controllerRoute!.Template, Is.EqualTo("api/roles"));
        Assert.That(updateHttpPost, Is.Not.Null);
        Assert.That(updateHttpPost!.Template, Is.EqualTo("{id}/update"));
        Assert.That(deleteHttpPost, Is.Not.Null);
        Assert.That(deleteHttpPost!.Template, Is.EqualTo("{id}/delete"));
        Assert.That(updateUserRolesHttpPost, Is.Not.Null);
        Assert.That(updateUserRolesHttpPost!.Template, Is.EqualTo("users/{id}/roles"));

        Assert.That(update.GetCustomAttribute<HttpPutAttribute>(), Is.Null);
        Assert.That(delete.GetCustomAttribute<HttpDeleteAttribute>(), Is.Null);
        Assert.That(updateUserRoles.GetCustomAttribute<HttpPutAttribute>(), Is.Null);
    }

    private sealed class StubRoleService : IRoleManagementApiAdapter
    {
        public Func<string, string?, IReadOnlyCollection<string>, Task<Result<RoleProjection, AppError>>>? CreateRoleHandler { get; init; }
        public Func<Id<AccountReference>, IReadOnlyCollection<string>, CancellationToken, Task<Result<Unit, AppError>>>? UpdateUserRolesHandler { get; init; }

        public Task<Result<IReadOnlyList<RoleProjection>, AppError>> GetRolesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<RoleProjection>, AppError>.Success([]));

        public Task<Result<RoleProjection, AppError>> GetRoleAsync(Id<RoleReference> roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<RoleProjection, AppError>.Success(new RoleProjection(roleId, string.Empty, null, [])));

        public Task<Result<RoleProjection, AppError>> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
            => CreateRoleHandler?.Invoke(name, description, permissionClaims)
               ?? Task.FromResult(Result<RoleProjection, AppError>.Success(new RoleProjection(Id<RoleReference>.New(), name, description, permissionClaims.ToList())));

        public Task<Result<Unit, AppError>> UpdateRoleAsync(Id<RoleReference> roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Unit, AppError>> DeleteRoleAsync(Id<RoleReference> roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public IReadOnlyList<PermissionClaimProjection> GetAvailablePermissionClaims() => [];

        public Task<Result<Unit, AppError>> UpdateUserRolesAsync(Id<AccountReference> userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
            => UpdateUserRolesHandler?.Invoke(userId, roleNames, cancellationToken)
               ?? Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Pagination<RoleProjection>, AppError>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Pagination<RoleProjection>, AppError>.Success(new Pagination<RoleProjection>()));
    }
}
