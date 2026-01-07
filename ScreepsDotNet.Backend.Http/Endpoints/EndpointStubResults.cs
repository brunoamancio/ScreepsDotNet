namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Http;

internal static class EndpointStubResults
{
    private const string NotImplementedError = "NotImplemented";

    public static IResult NotImplemented(string route)
        => Results.Json(new { error = NotImplementedError, route }, statusCode: StatusCodes.Status501NotImplemented);
}
