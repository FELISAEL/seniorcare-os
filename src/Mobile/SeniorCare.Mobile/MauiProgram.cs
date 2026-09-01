using SeniorCare.Mobile.Services;

namespace SeniorCare.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<SeniorCareApiClient>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
