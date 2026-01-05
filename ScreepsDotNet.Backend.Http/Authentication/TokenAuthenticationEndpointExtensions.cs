namespace ScreepsDotNet.Backend.Http.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class TokenAuthenticationEndpointExtensions
{
    public static TBuilder RequireTokenAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new TokenAuthenticationFilter());
        return builder;
    }

    private sealed class TokenAuthenticationFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var cancellationToken = httpContext.RequestAborted;

            var tokenService = httpContext.RequestServices.GetRequiredService<ITokenService>();
            var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();
            var userAccessor = httpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>();

            if (!httpContext.Request.Headers.TryGetValue(AuthHeaderNames.Token, out var tokenValues))
                return CreateUnauthorizedResult();

            var token = tokenValues.ToString();
            if (string.IsNullOrWhiteSpace(token))
                return CreateUnauthorizedResult();

            var userId = await tokenService.ResolveUserIdAsync(token, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(userId))
                return CreateUnauthorizedResult();

            var profile = await userRepository.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
                return CreateUnauthorizedResult();

            userAccessor.SetCurrentUser(profile);

            var refreshedToken = await tokenService.IssueTokenAsync(userId, cancellationToken).ConfigureAwait(false);
            httpContext.Response.Headers[AuthHeaderNames.Token] = refreshedToken;

            return await next(context).ConfigureAwait(false);
        }

        private static IResult CreateUnauthorizedResult()
            => Results.Json(new ErrorResponse(AuthResponseMessages.Unauthorized), statusCode: StatusCodes.Status401Unauthorized);
    }
}
