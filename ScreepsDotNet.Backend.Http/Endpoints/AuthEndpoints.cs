namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Linq;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class AuthEndpoints
{
    private const string SteamTicketEndpointName = "PostAuthSteamTicket";
    private const string AuthMeEndpointName = "GetAuthMe";

    public static void Map(WebApplication app)
    {
        app.MapPost(ApiRoutes.AuthSteamTicket, HandleSteamTicketAsync)
           .WithName(SteamTicketEndpointName);

        app.MapGet(ApiRoutes.AuthMe, HandleAuthMeAsync)
           .WithName(AuthMeEndpointName);
    }

    private static async Task<IResult> HandleSteamTicketAsync(SteamTicketRequest request, IUserRepository userRepository, ITokenService tokenService,
                                                              IOptions<AuthOptions> authOptions, CancellationToken cancellationToken)
    {
        var options = authOptions.Value;

        if (request.UseNativeAuth && !options.UseNativeAuth ||
            (!request.UseNativeAuth && options.UseNativeAuth))
            return Results.BadRequest(new ErrorResponse(AuthResponseMessages.UnsupportedAuthMethod));

        var ticketMatch = options.Tickets.FirstOrDefault(ticket =>
            string.Equals(ticket.Ticket, request.Ticket, StringComparison.Ordinal));

        if (ticketMatch is null)
            return Results.BadRequest(new ErrorResponse(AuthResponseMessages.CouldNotAuthenticate));

        var profile = await userRepository.GetProfileAsync(ticketMatch.UserId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return Results.BadRequest(new ErrorResponse(AuthResponseMessages.CouldNotAuthenticate));

        var token = await tokenService.IssueTokenAsync(ticketMatch.UserId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(new SteamTicketResponse(token, ticketMatch.SteamId));
    }

    private static async Task<IResult> HandleAuthMeAsync(HttpContext context, IUserRepository userRepository,
                                                         ITokenService tokenService, CancellationToken cancellationToken)
    {
        if (!context.Request.Headers.TryGetValue(AuthHeaderNames.Token, out var tokenValues))
            return Unauthorized();

        var token = tokenValues.ToString();
        var userId = await tokenService.ResolveUserIdAsync(token, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var profile = await userRepository.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return Unauthorized();

        var refreshedToken = await tokenService.IssueTokenAsync(userId, cancellationToken).ConfigureAwait(false);
        context.Response.Headers[AuthHeaderNames.Token] = refreshedToken;

        return Results.Ok(AuthMeResponse.From(profile));
    }

    private static IResult Unauthorized()
        => Results.Json(new ErrorResponse(AuthResponseMessages.Unauthorized), statusCode: StatusCodes.Status401Unauthorized);
}
