using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityModel;
using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Services;

public sealed class ApplicationProfileService(UserManager<ApplicationUser> userManager) : IProfileService
{
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subjectId = context.Subject.GetSubjectId();
        var user = await userManager.FindByIdAsync(subjectId);
        if (user?.UserName == null)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Name, user.UserName),
            new Claim(JwtClaimTypes.PreferredUserName, user.UserName),
            new Claim(JwtClaimTypes.EmailVerified, user.EmailConfirmed.ToString().ToLowerInvariant(), ClaimValueTypes.Boolean),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtClaimTypes.Email, user.Email));
        }

        context.AddRequestedClaims(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var subjectId = context.Subject.GetSubjectId();
        var user = await userManager.FindByIdAsync(subjectId);

        context.IsActive = user != null;
    }
}
