using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuaToolsGui.Models;
using LuaToolsGui.Services;

namespace LuaToolsGui.ViewModels;

/// <summary>
/// First milestone of the Playden companion: connect, pair, and read the console's game catalog.
/// No upload, delete, install, or launch actions are exposed here.
/// </summary>
public partial class Ps5CompanionViewModel : ObservableObject
{
    private readonly LuaDenClient _client;
    private readonly SettingsService _settings;

    [ObservableProperty] private string _hostAddress;
    [ObservableProperty] private string _port;
    [ObservableProperty] private string _confirmationCode = "";
    [ObservableProperty] private string _statusText = Resources.Strings.Ps5_Status_NotConnected;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPaired;
    [ObservableProperty] private LuaDenGame? _selectedGame;
    [ObservableProperty] private LuaDenGameDetails? _selectedGameDetails;

    public ObservableCollection<LuaDenGame> Games { get; } = [];

    public Ps5CompanionViewModel(LuaDenClient client, SettingsService settings)
    {
        _client = client;
        _settings = settings;
        _hostAddress = settings.LuaDenHost ?? "";
        _port = settings.LuaDenPort.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Called when the page is first loaded; reconnects only when an address was saved.</summary>
    public Task OnViewLoadedAsync() =>
        string.IsNullOrWhiteSpace(HostAddress) ? Task.CompletedTask : ConnectAsync();

    [RelayCommand]
    private Task ConnectAsync() => RunBusyAsync(async () =>
    {
        ConfigureClient();
        IsPaired = await _client.GetPairStatusAsync();
        if (!IsPaired)
        {
            Games.Clear();
            SelectedGameDetails = null;
            StatusText = Resources.Strings.Ps5_Status_NotPaired;
            return;
        }

        await LoadGamesCoreAsync();
    });

    [RelayCommand]
    private Task StartPairingAsync() => RunBusyAsync(async () =>
    {
        ConfigureClient();
        await _client.StartPairingAsync();
        StatusText = Resources.Strings.Ps5_Status_AwaitingCode;
    });

    [RelayCommand]
    private Task ConfirmPairingAsync() => RunBusyAsync(async () =>
    {
        ConfigureClient();
        var code = ConfirmationCode.Trim();
        if (code.Length != 6 || code.Any(c => c < '0' || c > '9'))
            throw new InvalidOperationException(Resources.Strings.Ps5_Error_Code);

        await _client.ConfirmPairingAsync(code);
        IsPaired = await _client.GetPairStatusAsync();
        if (!IsPaired)
            throw new InvalidOperationException(Resources.Strings.Ps5_Error_Pairing);

        ConfirmationCode = "";
        await LoadGamesCoreAsync();
    });

    [RelayCommand]
    private Task RefreshGamesAsync() => RunBusyAsync(async () =>
    {
        ConfigureClient();
        IsPaired = await _client.GetPairStatusAsync();
        if (!IsPaired)
        {
            Games.Clear();
            SelectedGameDetails = null;
            StatusText = Resources.Strings.Ps5_Status_NotPaired;
            return;
        }

        await LoadGamesCoreAsync();
    });

    [RelayCommand]
    private Task LoadSelectedGameAsync() => RunBusyAsync(async () =>
    {
        if (!IsPaired || SelectedGame is null) return;
        ConfigureClient();
        SelectedGameDetails = await _client.GetGameAsync(SelectedGame.Id);
    });

    private void ConfigureClient()
    {
        var host = HostAddress.Trim();
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException(Resources.Strings.Ps5_Error_HostRequired);
        if (!IPAddress.TryParse(host, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            throw new InvalidOperationException(Resources.Strings.Ps5_Error_InvalidHost);

        if (!int.TryParse(Port, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            throw new InvalidOperationException(Resources.Strings.Ps5_Error_InvalidPort);

        _client.Configure(host, port);
        _settings.LuaDenHost = host;
        _settings.LuaDenPort = port;
    }

    private async Task LoadGamesCoreAsync()
    {
        var catalog = await _client.GetGamesAsync();
        Games.Clear();
        foreach (var game in catalog.Games)
            Games.Add(game);

        SelectedGame = null;
        SelectedGameDetails = null;

        if (!catalog.Known)
            StatusText = Resources.Strings.Ps5_Status_LibraryUnknown;
        else if (catalog.SettingUp)
            StatusText = Resources.Strings.Ps5_Status_SettingUp;
        else
            StatusText = string.Format(CultureInfo.CurrentCulture, Resources.Strings.Ps5_Status_Loaded, Games.Count);
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await operation();
        }
        catch (LuaDenException ex)
        {
            ErrorMessage = ex.Message;
            if (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                IsPaired = false;
                Games.Clear();
                SelectedGameDetails = null;
            }
        }
        catch (OperationCanceledException)
        {
            IsPaired = false;
            ErrorMessage = Resources.Strings.Ps5_Error_Timeout;
        }
        catch (HttpRequestException)
        {
            IsPaired = false;
            ErrorMessage = Resources.Strings.Ps5_Error_Connection;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
