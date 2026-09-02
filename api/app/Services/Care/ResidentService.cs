using System.Security.Claims;
using SeniorCare.Api.App.Models.Care;
using SeniorCare.Api.App.Repositories.Care;
using SeniorCare.Api.App.Core.Auth;

namespace SeniorCare.Api.App.Services.Care;

public sealed class ResidentService(ICareRepository residents)
{
    public Task<IReadOnlyList<Resident>> ListAsync(
        CancellationToken cancellationToken = default) =>
        residents.ListResidentsAsync(cancellationToken);

    public async Task<IReadOnlyList<Resident>> ListForCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.IsCareTeam())
        {
            return await residents.ListResidentsAsync(cancellationToken);
        }

        var residentId = principal.GetResidentId();
        if (!residentId.HasValue)
        {
            return Array.Empty<Resident>();
        }

        var resident = await residents.FindResidentByIdAsync(
            residentId.Value,
            cancellationToken);

        return resident is null
            ? Array.Empty<Resident>()
            : new[] { resident };
    }

    public Task<Resident?> FindByIdAsync(
        Guid residentId,
        CancellationToken cancellationToken = default) =>
        residents.FindResidentByIdAsync(residentId, cancellationToken);

    public Task<Resident?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default) =>
        residents.FindResidentByUsernameAsync(username, cancellationToken);

    public Task<Resident> CreateAsync(
        CreateResidentRequest request,
        CancellationToken cancellationToken = default) =>
        residents.CreateResidentAsync(request, cancellationToken);
}
