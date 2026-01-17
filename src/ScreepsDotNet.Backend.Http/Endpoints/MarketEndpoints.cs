namespace ScreepsDotNet.Backend.Http.Endpoints;

using Microsoft.AspNetCore.Mvc;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints.Helpers;
using ScreepsDotNet.Backend.Http.Endpoints.Models;
using ScreepsDotNet.Backend.Http.Routing;

internal static class MarketEndpoints
{
    private const string OrdersIndexEndpointName = "GetMarketOrdersIndex";
    private const string OrdersEndpointName = "GetMarketOrders";
    private const string MyOrdersEndpointName = "GetMarketMyOrders";
    private const string StatsEndpointName = "GetMarketStats";
    private const string InvalidParamsMessage = "invalid params";
#pragma warning disable IDE0051, IDE0052 // Used in attribute parameters
    private const string ResourceTypeQueryName = "resourceType";
#pragma warning restore IDE0051, IDE0052
    private const string MissingUserContextMessage = "User context is not available.";

    public static void Map(WebApplication app)
    {
        MapOrdersIndex(app);
        MapOrders(app);
        MapMyOrders(app);
        MapStats(app);
    }

    private static void MapOrdersIndex(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Market.OrdersIndex,
                   async (IMarketOrderRepository repository, CancellationToken cancellationToken) => {
                       var summaries = await repository.GetActiveOrderIndexAsync(cancellationToken).ConfigureAwait(false);
                       var payload = MarketResponseFactory.CreateSummaryList(summaries);
                       return Results.Ok(payload);
                   })
           .RequireTokenAuthentication()
           .WithName(OrdersIndexEndpointName);
    }

    private static void MapOrders(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Market.Orders,
                   async ([FromQuery(Name = ResourceTypeQueryName)] string? resourceType,
                          IMarketOrderRepository repository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidResourceType(resourceType))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var orders = await repository.GetActiveOrdersByResourceAsync(resourceType!, cancellationToken).ConfigureAwait(false);
                              var payload = MarketResponseFactory.CreateOrderList(orders);
                              return Results.Ok(payload);
                          })
           .RequireTokenAuthentication()
           .WithName(OrdersEndpointName);
    }

    private static void MapMyOrders(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Market.MyOrders,
                   async (ICurrentUserAccessor userAccessor,
                          IMarketOrderRepository repository,
                          CancellationToken cancellationToken) => {
                              var user = UserEndpointGuards.RequireUser(userAccessor, MissingUserContextMessage);
                              var orders = await repository.GetOrdersByUserAsync(user.Id, cancellationToken).ConfigureAwait(false);
                              var payload = MarketResponseFactory.CreateOrderList(orders);
                              return Results.Ok(payload);
                          })
           .RequireTokenAuthentication()
           .WithName(MyOrdersEndpointName);
    }

    private static void MapStats(WebApplication app)
    {
        app.MapGet(ApiRoutes.Game.Market.Stats,
                   async ([FromQuery(Name = ResourceTypeQueryName)] string? resourceType,
                          IMarketStatsRepository repository,
                          CancellationToken cancellationToken) => {
                              if (!IsValidResourceType(resourceType))
                                  return Results.BadRequest(new ErrorResponse(InvalidParamsMessage));

                              var stats = await repository.GetStatsAsync(resourceType!, cancellationToken).ConfigureAwait(false);
                              var payload = MarketResponseFactory.CreateStats(stats);
                              return Results.Ok(payload);
                          })
           .RequireTokenAuthentication()
           .WithName(StatsEndpointName);
    }

    private static bool IsValidResourceType(string? resourceType)
        => !string.IsNullOrWhiteSpace(resourceType);
}
