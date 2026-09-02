using SeniorCare.Api.App.Controllers.Communication;
using SeniorCare.Api.App.Services.Communication;

namespace SeniorCare.Api.Config;

public static class CommunicationModule
{
    public static IServiceCollection AddCommunicationModule(
        this IServiceCollection services)
    {
        services.AddSingleton<VideoRoomService>();

        return services;
    }

    public static IEndpointRouteBuilder MapCommunicationModule(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapVideoRoomsController();

        return endpoints;
    }
}