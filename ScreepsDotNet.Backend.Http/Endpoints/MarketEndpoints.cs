namespace ScreepsDotNet.Backend.Http.Endpoints;

using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Routing;

internal static class MarketEndpoints
{
    private const string OrdersIndexEndpointName = "GetMarketOrdersIndex";
    private const string OrdersEndpointName = "GetMarketOrders";
    private const string MyOrdersEndpointName = "GetMarketMyOrders";
    private const string StatsEndpointName = "GetMarketStats";

    public static void Map(WebApplication app)
    {
        MapProtectedGet(app, ApiRoutes.Game.Market.OrdersIndex, OrdersIndexEndpointName);
        MapProtectedGet(app, ApiRoutes.Game.Market.Orders, OrdersEndpointName);
        MapProtectedGet(app, ApiRoutes.Game.Market.MyOrders, MyOrdersEndpointName);
        MapProtectedGet(app, ApiRoutes.Game.Market.Stats, StatsEndpointName);
    }

    private static void MapProtectedGet(WebApplication app, string route, string endpointName)
        => app.MapGet(route, () => EndpointStubResults.NotImplemented(route))
              .RequireTokenAuthentication()
              .WithName(endpointName);
}
