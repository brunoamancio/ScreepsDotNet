namespace ScreepsDotNet.Backend.Http.Endpoints;

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Helpers;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static partial class RegisterEndpoints
{
    private const string InvalidEmailMessage = "invalid email";
    private const string InvalidUsernameMessage = "invalid username";
    private const string UsernameAlreadySetMessage = "username already set";
    private const string ExistsMessage = "exists";

    private const string CheckEmailEndpointName = "RegisterCheckEmail";
    private const string CheckUsernameEndpointName = "RegisterCheckUsername";
    private const string SetUsernameEndpointName = "RegisterSetUsername";

    public static void Map(WebApplication app)
    {
        MapCheckEmail(app);
        MapCheckUsername(app);
        MapSetUsername(app);
    }

    private static void MapCheckEmail(WebApplication app)
    {
        app.MapGet(ApiRoutes.Register.CheckEmail,
                   async ([FromQuery] string? email,
                          IUserRepository userRepository,
                          CancellationToken cancellationToken) => {
                              if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email))
                                  return Results.BadRequest(new ErrorResponse(InvalidEmailMessage));

                              var exists = await userRepository.EmailExistsAsync(email, cancellationToken).ConfigureAwait(false);
                              return exists
                                  ? Results.BadRequest(new ErrorResponse(ExistsMessage))
                                  : Results.Ok(UserResponseFactory.CreateEmpty());
                          })
           .WithName(CheckEmailEndpointName);
    }

    private static void MapCheckUsername(WebApplication app)
    {
        app.MapGet(ApiRoutes.Register.CheckUsername,
                   async ([FromQuery] string? username,
                          IUserRepository userRepository,
                          CancellationToken cancellationToken) => {
                              if (string.IsNullOrWhiteSpace(username) || !UsernameRegex().IsMatch(username))
                                  return Results.BadRequest(new ErrorResponse(InvalidUsernameMessage));

                              var exists = await userRepository.UsernameExistsAsync(username, cancellationToken).ConfigureAwait(false);
                              return exists
                                  ? Results.BadRequest(new ErrorResponse(ExistsMessage))
                                  : Results.Ok(UserResponseFactory.CreateEmpty());
                          })
           .WithName(CheckUsernameEndpointName);
    }

    private static void MapSetUsername(WebApplication app)
    {
        app.MapPost(ApiRoutes.Register.SetUsername,
                    async ([FromBody] SetUsernameRequest request,
                           ICurrentUserAccessor userAccessor,
                           IUserRepository userRepository,
                           CancellationToken cancellationToken) => {
                               var user = UserEndpointGuards.RequireUser(userAccessor, "User context is not available.");
                               var username = request.Username?.Trim();

                               if (string.IsNullOrWhiteSpace(username) || !UsernameRegex().IsMatch(username))
                                   return Results.BadRequest(new ErrorResponse(InvalidUsernameMessage));

                               if (!string.IsNullOrWhiteSpace(request.Email) && !EmailRegex().IsMatch(request.Email))
                                   return Results.BadRequest(new ErrorResponse(InvalidEmailMessage));

                               var result = await userRepository.SetUsernameAsync(user.Id, username, request.Email, cancellationToken).ConfigureAwait(false);
                               return result switch
                               {
                                   SetUsernameResult.Success => Results.Ok(UserResponseFactory.CreateEmpty()),
                                   SetUsernameResult.UserNotFound => Results.NotFound(new ErrorResponse("user not found")),
                                   SetUsernameResult.UsernameAlreadySet => Results.BadRequest(new ErrorResponse(UsernameAlreadySetMessage)),
                                   SetUsernameResult.UsernameExists => Results.BadRequest(new ErrorResponse(InvalidUsernameMessage)),
                                   _ => Results.BadRequest(new ErrorResponse(InvalidUsernameMessage))
                               };
                           })
           .RequireTokenAuthentication()
           .WithName(SetUsernameEndpointName);
    }

    [GeneratedRegex(@"^[\w\d\-\.\+&]+\@[\w\d\-\.&]+\.[\w\d\-\.&]{2,}$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex("^[a-zA-Z0-9_-]{3,}$", RegexOptions.Compiled)]
    private static partial Regex UsernameRegex();
}
