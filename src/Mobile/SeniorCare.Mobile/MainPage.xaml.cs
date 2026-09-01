using System.Collections.ObjectModel;
using SeniorCare.Mobile.Models;
using SeniorCare.Mobile.Services;

namespace SeniorCare.Mobile;

public partial class MainPage : ContentPage
{
    private readonly SeniorCareApiClient _apiClient;

    public MainPage(SeniorCareApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        BindingContext = this;

        if (_apiClient.IsAuthenticated)
        {
            ShowDashboard();
        }
    }

    public ObservableCollection<AlertItem> Alerts { get; } = [];
    public ObservableCollection<VideoRoom> VideoRooms { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_apiClient.IsAuthenticated)
        {
            await RefreshAsync();
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs eventArgs)
    {
        LoginMessage.Text = string.Empty;
        SetBusy(true);

        try
        {
            var user = await _apiClient.LoginAsync(
                UsernameEntry.Text?.Trim() ?? string.Empty,
                PasswordEntry.Text ?? string.Empty);
            WelcomeLabel.Text = $"Bienvenido, {user.DisplayName}";
            PasswordEntry.Text = string.Empty;
            ShowDashboard();
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            LoginMessage.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRefreshing(object? sender, EventArgs eventArgs)
    {
        try
        {
            await RefreshAsync();
        }
        finally
        {
            AlertsRefresh.IsRefreshing = false;
        }
    }

    private async void OnAcknowledgeClicked(object? sender, EventArgs eventArgs)
    {
        await UpdateAlertFromButtonAsync(sender, "acknowledged");
    }

    private async void OnResolveClicked(object? sender, EventArgs eventArgs)
    {
        await UpdateAlertFromButtonAsync(sender, "resolved");
    }

    private async Task UpdateAlertFromButtonAsync(object? sender, string status)
    {
        if (sender is not Button button
            || button.CommandParameter is not Guid alertId)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _apiClient.UpdateAlertAsync(alertId, status);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("SeniorCare", exception.Message, "Aceptar");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnJoinVideoClicked(object? sender, EventArgs eventArgs)
    {
        if (sender is not Button button
            || button.CommandParameter is not string url
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        await Launcher.Default.OpenAsync(uri);
    }

    private void OnLogoutClicked(object? sender, EventArgs eventArgs)
    {
        _apiClient.Logout();
        Alerts.Clear();
        VideoRooms.Clear();
        DashboardView.IsVisible = false;
        LoginView.IsVisible = true;
    }

    private async Task RefreshAsync()
    {
        SetBusy(true);
        try
        {
            var alerts = await _apiClient.GetActiveAlertsAsync();
            var rooms = await _apiClient.GetVideoRoomsAsync();

            Alerts.Clear();
            foreach (var alert in alerts)
            {
                Alerts.Add(alert);
            }

            VideoRooms.Clear();
            foreach (var room in rooms)
            {
                VideoRooms.Add(room);
            }

            AlertCountLabel.Text = Alerts.Count.ToString();
            EmptyLabel.IsVisible = Alerts.Count == 0;
            WelcomeLabel.Text =
                $"Bienvenido, {_apiClient.CurrentUser?.DisplayName ?? "cuidador"}";
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("No se pudo actualizar", exception.Message, "Aceptar");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowDashboard()
    {
        LoginView.IsVisible = false;
        DashboardView.IsVisible = true;
        WelcomeLabel.Text =
            $"Bienvenido, {_apiClient.CurrentUser?.DisplayName ?? "cuidador"}";
    }

    private void SetBusy(bool value)
    {
        BusyIndicator.IsVisible = value;
        BusyIndicator.IsRunning = value;
    }
}

