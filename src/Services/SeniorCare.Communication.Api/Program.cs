using SeniorCare.Communication.Api.Controllers.Video;
using SeniorCare.Communication.Api.Services.Video;
using SeniorCare.Shared.Auth;
using SeniorCare.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSeniorCareServiceDefaults("seniorcare-communication-api");
builder.Services.AddSeniorCareAuthentication(builder.Configuration);
builder.Services.AddSingleton<VideoRoomService>();

var app = builder.Build();
app.UseSeniorCareServiceDefaults();

app.MapVideoRoomsController();

app.Run();
