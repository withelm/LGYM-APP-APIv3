using System.Reflection;
using FluentAssertions;
using LgymApi.Api;
using LgymApi.Application.Common.Errors;
using LgymApi.Application.Common.Results;
using LgymApi.Application.Pagination;
using LgymApi.Domain.Entities;
using LgymApi.Api.Features.Role.Contracts;
using LgymApi.Api.Features.Role.Controllers;
using LgymApi.Application.Features.Role;
using LgymApi.Application.Features.Role.Models;
using LgymApi.Application.Mapping;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.ValueObjects;
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
          var roleId = Id<LgymApi.Domain.Entities.Role>.New();
          var fakeService = new StubRoleService
          {
              CreateRoleHandler = (_, _, _) => Task.FromResult(Result<RoleResult, AppError>.Success(new RoleResult
              {
                  Id = roleId,
                  Name = "Coach",
                  Description = "desc",
                  PermissionClaims = ManageUserRolesClaim
              }))
          };
          var controller = new RoleController(fakeService, BuildMapper());

          var action = await controller.CreateRole(new UpsertRoleRequest
          {
              Name = "Coach",
              Description = "desc",
              PermissionClaims = ManageUserRolesClaim
          });

          var ok = action as OkObjectResult;
          ok.Should().NotBeNull();
          var dto = ok!.Value as RoleDto;
          
          dto.Should().NotBeNull();
          dto!.Id.Should().Be($"{roleId:N}");
          dto.Name.Should().Be("Coach");
          dto.PermissionClaims.Should().BeEquivalentTo(ManageUserRolesClaim);
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

        update.Should().NotBeNull();
        delete.Should().NotBeNull();
        updateUserRoles.Should().NotBeNull();

        var updateHttpPost = update!.GetCustomAttribute<HttpPostAttribute>();
        var deleteHttpPost = delete!.GetCustomAttribute<HttpPostAttribute>();
        var updateUserRolesHttpPost = updateUserRoles!.GetCustomAttribute<HttpPostAttribute>();

        updateHttpPost.Should().NotBeNull();
        updateHttpPost!.Template.Should().Be("{id}/update");
        deleteHttpPost.Should().NotBeNull();
        deleteHttpPost!.Template.Should().Be("{id}/delete");
        updateUserRolesHttpPost.Should().NotBeNull();
        updateUserRolesHttpPost!.Template.Should().Be("users/{id}/roles");

        update.GetCustomAttribute<HttpPutAttribute>().Should().BeNull();
        delete.GetCustomAttribute<HttpDeleteAttribute>().Should().BeNull();
        updateUserRoles.GetCustomAttribute<HttpPutAttribute>().Should().BeNull();
    }

    private sealed class StubRoleService : IRoleService
    {
        public Func<string, string?, IReadOnlyCollection<string>, Task<Result<RoleResult, AppError>>>? CreateRoleHandler { get; init; }

        public Task<Result<List<RoleResult>, AppError>> GetRolesAsync(CancellationToken cancellationToken = default) 
            => Task.FromResult(Result<List<RoleResult>, AppError>.Success(new List<RoleResult>()));
        
        public Task<Result<RoleResult, AppError>> GetRoleAsync(Id<Domain.Entities.Role> roleId, CancellationToken cancellationToken = default) 
            => Task.FromResult(Result<RoleResult, AppError>.Success(new RoleResult { Id = roleId }));

        public Task<Result<RoleResult, AppError>> CreateRoleAsync(string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
            => CreateRoleHandler?.Invoke(name, description, permissionClaims)
               ?? Task.FromResult(Result<RoleResult, AppError>.Success(new RoleResult { Id = Id<LgymApi.Domain.Entities.Role>.New(), Name = name, Description = description, PermissionClaims = permissionClaims.ToList() }));

        public Task<Result<Unit, AppError>> UpdateRoleAsync(Id<Domain.Entities.Role> roleId, string name, string? description, IReadOnlyCollection<string> permissionClaims, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Unit, AppError>> DeleteRoleAsync(Id<Domain.Entities.Role> roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public List<PermissionClaimLookupResult> GetAvailablePermissionClaims()
            => new();

        public Task<Result<Unit, AppError>> UpdateUserRolesAsync(Id<Domain.Entities.User> userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Unit, AppError>.Success(Unit.Value));

        public Task<Result<Pagination<RoleResult>, AppError>> GetRolesPaginatedAsync(FilterInput filterInput, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Pagination<RoleResult>, AppError>.Success(new Pagination<RoleResult>()));
    }
}
