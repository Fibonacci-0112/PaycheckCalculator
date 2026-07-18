using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace PaycheckCalculator.Api.Endpoints;

internal static class ActiveUserResolver
{
    public static async Task<IdentityUser?> GetActiveUserAsync(
        ClaimsPrincipal principal,
        UserManager<IdentityUser> users)
    {
        var userId = users.GetUserId(principal);
        if (string.IsNullOrEmpty(userId))
            return null;

        return await users.FindByIdAsync(userId);
    }
}
