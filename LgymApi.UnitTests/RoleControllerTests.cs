using System.Reflection;
using LgymApi.Api;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Api.Features.Role.Controllers;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.Role.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;

namespace LgymApi.UnitTests;

[TestFixture]
public sealed class RoleControllerTests
{
    [Test]
    public async Task CreateRole_ReturnsMappedRoleDto()
    {
        var fakeService = new StubRoleService
        {
            CreateRoleHandler = (_, _, _) => Task.FromResult(new RoleResult
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Coach",
                Description = "desc",
                PermissionClaims = ["users.roles.manage"]
            })
        };
        var controller = new RoleController(fakeService, BuildMapper());

        var action = await controller.CreateRole(new UpsertRoleRequest
        {
            Name = "Coach",
            Description = "desc",
            PermissionClaims = ["users.roles.manage"]
        });

        var ok = action as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var dto = ok!.Value as RoleDto;
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Id, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
        Assert.That(dto.Name, Is.EqualTo("Coach"));
        Assert.That(dto.PermissionClaims, Is.EqualTo(new[] { "users.roles.manage" }));
    }

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddApplicationMapping(typeof(Program).Assembly, typeof(IMappingProfile).Assembly);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMapper>();
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

        var updateHttpPost = update!.GetCustomAttribute<HttpPostAttribute>();
        var deleteHttpPost = delete!.GetCustomAttribute<HttpPostAttribute>();
        var updateUserRolesHttpPost = updateUserRoles!.GetCustomAttribute<HttpPostAttribute>();

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

    private sealed class StubRoleService : IRoleService
    {
        public Func<string, string?, IReadOnlyCollection<string>, Task<RoleResult>>? CreateRoleHandler { get; init; }

        public Task<List<RoleResult>> GetRolesAsync() => Task.FromResult(new List<RoleResult>());
        public Task<RoleResult> GetRoleAsync(Guid roleId) => Task.FromResult(new RoleResult { Id = roleId });

        public Task<RoleResult> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims)
            => CreateRoleHandler?.Invoke(name, description, permissionClaims)
               ?? Task.FromResult(new RoleResult { Id = Guid.NewGuid(), Name = name, Description = description, PermissionClaims = permissionClaims.ToList() });

        public Task UpdateRoleAsync(Guid roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims)
            => Task.CompletedTask;

        public Task DeleteRoleAsync(Guid roleId)
            => Task.CompletedTask;

        public List<PermissionClaimLookupResult> GetAvailablePermissionClaims()
            => new();

        public Task UpdateUserRolesAsync(Guid userId, IReadOnlyCollection<string> roleNames)
            => Task.CompletedTask;
    }
}
