namespace ScreepsDotNet.Backend.Http.Endpoints;

using System;
using System.Linq;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Authentication;
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

        app.MapGet(ApiRoutes.AuthMe, (ICurrentUserAccessor userAccessor) => {
            var profile = userAccessor.CurrentUser ?? throw new InvalidOperationException("User context is not available.");
            return Results.Ok(AuthMeResponse.From(profile));
        })
           .RequireTokenAuthentication()
           .WithName(AuthMeEndpointName);
    }

    private static async Task<IResult> HandleSteamTicketAsync(SteamTicketRequest request, IUserRepository userRepository, ITokenService tokenService,
                                                              IOptions<AuthOptions> authOptions, CancellationToken cancellationToken)
    {
        var options = authOptions.Value;

        if ((request.UseNativeAuth && !options.UseNativeAuth) || (!request.UseNativeAuth && options.UseNativeAuth))
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

}
