using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ShooterRecordsLab.Api.Authentication;

internal static class JwtAuthentication
{
    public const string WritePolicy = "MatchesWrite";

    public static IServiceCollection AddRecordsAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<JwtSettings>().BindConfiguration("Authentication:Jwt")
            .Validate(settings => (environment.IsDevelopment() && settings.IsEmpty) || settings.IsValid(), "JWT configuration is incomplete or unsafe.")
            .Validate(settings => !environment.IsProduction() || !settings.ValidationOnly, "Validation credentials are not allowed in Production.")
            .ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, configured) =>
            {
                var settings = configured.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = settings.IsValid() ? new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKeyBase64)) : null,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IncludeTokenOnFailedValidation = false,
                    LogValidationExceptions = false
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await WriteProblem(context.HttpContext, 401, "Authentication required", "A valid bearer token is required.");
                    },
                    OnForbidden = context => WriteProblem(context.HttpContext, 403, "Permission required", "The matches.write scope is required.")
                };
            });
        services.AddAuthorization(options => options.AddPolicy(WritePolicy, policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(context => context.User.FindAll("scope").Any(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains("matches.write", StringComparer.Ordinal)))));
        return services;
    }

    private static async Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
