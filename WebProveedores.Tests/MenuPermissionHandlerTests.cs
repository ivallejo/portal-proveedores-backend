using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using WebProveedores.Api.Security;
using WebProveedores.Application.Ports.Inbound.Access;

namespace WebProveedores.Tests;

public sealed class MenuPermissionHandlerTests
{
    private static readonly ClaimsPrincipal Authenticated = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "x")], "test"));

    [Fact]
    public async Task Succeeds_when_the_user_has_one_of_the_options_and_reads_permissions_once()
    {
        var permissions = new FakePermissions("DOCUMENTS");
        var handler = new MenuPermissionHandler(new FakeCurrentUser(), permissions);
        var accounting = new MenuPermissionRequirement(["ACCOUNTING"]);
        var documents = new MenuPermissionRequirement(["ACCOUNTING", "DOCUMENTS"]);
        var context = new AuthorizationHandlerContext([accounting, documents], Authenticated, null);

        await handler.HandleAsync(context);

        Assert.Equal([accounting], context.PendingRequirements);
        Assert.Equal(1, permissions.Calls);
    }

    [Fact]
    public async Task Does_not_query_permissions_for_anonymous_users()
    {
        var permissions = new FakePermissions("DOCUMENTS");
        var handler = new MenuPermissionHandler(new FakeCurrentUser(), permissions);
        var context = new AuthorizationHandlerContext([new MenuPermissionRequirement(["DOCUMENTS"])], new ClaimsPrincipal(new ClaimsIdentity()), null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Equal(0, permissions.Calls);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsPasswordChangeSession => false;
    }

    private sealed class FakePermissions(params string[] codes) : IPermissionService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlySet<string>> PermissionsOfAsync(Guid userId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlySet<string>>(codes.ToHashSet());
        }
    }
}
