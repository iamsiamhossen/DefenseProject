using KYAUCPChapter.Infrastructure.Identity;
using KYAUCPChapter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace KYAUCPChapter.API.Security;

public static class SecurityServiceCollectionExtensions
{
    public const string AuthCookieName = "__Host-KYAUCPChapter.Auth";
    public const string AntiforgeryCookieName = "__Host-KYAUCPChapter.Antiforgery";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.Lockout.AllowedForNewUsers = true;
                PasswordPolicy.Apply(options.Password);
            })
            .AddPasswordValidator<MaximumPasswordLengthValidator>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.Events.OnRedirectToLogin = context => WriteStatus(context.Response, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => WriteStatus(context.Response, StatusCodes.Status403Forbidden);
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.Student, policy => policy.RequireRole(Roles.Student))
            .AddPolicy(AuthorizationPolicies.Organizer, policy => policy.RequireRole(Roles.Organizer));

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(allowedOrigins)
            .AllowCredentials()
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
            .WithHeaders("Content-Type", AntiforgeryHeaderName)));

        return services;
    }

    private static Task WriteStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
