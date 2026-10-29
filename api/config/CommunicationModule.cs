using SeniorCare.Api.App.Controllers.Communication;
using SeniorCare.Api.App.Services.Communication;

namespace SeniorCare.Api.Config;

public static class CommunicationModule
{
    public static IServiceCollection AddCommunicationModule(
        this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            GoogleMeetOptions.FromConfiguration(
                provider.GetRequiredService<IConfiguration>()));

        services.AddHttpClient<GoogleMeetClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));

        services.AddSingleton(provider =>
            JaasTokenService.FromConfiguration(
                provider.GetRequiredService<IConfiguration>()));

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