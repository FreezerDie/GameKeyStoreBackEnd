using Microsoft.AspNetCore.Authorization;
using GameKeyStore.Services;

namespace GameKeyStore.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly PermissionService _permissionService;

        public PermissionAuthorizationHandler(PermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, 
            PermissionRequirement requirement)
        {
            // Check if user has the required permission
            if (await _permissionService.UserHasPermissionAsync(context.User, requirement.Resource, requirement.Action))
            {
                context.Succeed(requirement);
            }
        }
    }
}
