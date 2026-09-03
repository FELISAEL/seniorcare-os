using Microsoft.Extensions.Configuration;
using SeniorCare.Api.App.Models.Communication;
using SeniorCare.Api.App.Services.Communication;

namespace SeniorCare.Api.Tests.Services.Communication;

public sealed class VideoRoomServiceTests
{
    [Fact]
    public void CreateOrGet_CreatesActiveRoomWithExpectedData()
    {
        var service = CreateService("https://video.test/");
        var residentId = Guid.NewGuid();

        var room = service.CreateOrGet(
            new CreateVideoRoomRequest(
                residentId,
                "  María López  "));

        Assert.Equal(residentId, room.ResidentId);
        Assert.Equal("María López", room.ResidentName);
        Assert.Equal("active", room.Status);
        Assert.StartsWith(
            $"seniorcare-{residentId:N}-",
            room.RoomCode);
        Assert.Equal(
            $"https://video.test/{room.RoomCode}",
            room.RoomUrl);
    }

    [Fact]
    public void CreateOrGet_WithExistingActiveRoom_ReturnsSameRoom()
    {
        var service = CreateService("https://video.test/");
        var residentId = Guid.NewGuid();

        var first = service.CreateOrGet(
            new CreateVideoRoomRequest(residentId, "María López"));

        var second = service.CreateOrGet(
            new CreateVideoRoomRequest(residentId, "María López"));

        Assert.Equal(first.Id, second.Id);
        Assert.Single(service.ListActiveForResident(residentId));
    }

    [Fact]
    public void Close_ExistingRoom_ChangesStatusToClosed()
    {
        var service = CreateService("https://video.test/");
        var room = service.CreateOrGet(
            new CreateVideoRoomRequest(
                Guid.NewGuid(),
                "María López"));

        var closed = service.Close(room.Id);

        Assert.NotNull(closed);
        Assert.Equal("closed", closed!.Status);
        Assert.Empty(service.ListActive());
    }

    [Fact]
    public void Close_UnknownRoom_ReturnsNull()
    {
        var service = CreateService("https://video.test/");

        Assert.Null(service.Close(Guid.NewGuid()));
    }

    [Fact]
    public void CreateOrGet_AfterClosingRoom_CreatesNewRoom()
    {
        var service = CreateService("https://video.test/");
        var residentId = Guid.NewGuid();
        var request = new CreateVideoRoomRequest(
            residentId,
            "María López");

        var first = service.CreateOrGet(request);
        service.Close(first.Id);

        var second = service.CreateOrGet(request);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("active", second.Status);
    }

    private static VideoRoomService CreateService(string baseUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["VIDEO_PROVIDER_BASE_URL"] = baseUrl
                })
            .Build();

        return new VideoRoomService(configuration);
    }
}
