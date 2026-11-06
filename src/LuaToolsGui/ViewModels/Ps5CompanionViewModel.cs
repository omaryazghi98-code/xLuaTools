using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuaToolsGui.Models;
using LuaToolsGui.Services;

namespace LuaToolsGui.ViewModels;

/// <summary>
/// Playden companion: connect, pair, browse the game catalog, and navigate file roots.
/// This milestone intentionally exposes no upload, delete, install, or launch actions.
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
    [ObservableProperty] private LuaDenRoot? _selectedRoot;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPathLabel))]
    [NotifyPropertyChangedFor(nameof(HasParentDirectory))]
    private string _currentPath = "";
    [ObservableProperty] private bool _hasListing;
    [ObservableProperty] private bool _listingTruncated;
    [ObservableProperty] private uint _unshownEntries;

    public ObservableCollection<LuaDenGame> Games { get; } = [];
    public ObservableCollection<LuaDenFileEntry> Entries { get; } = [];

    public string CurrentPathLabel => string.IsNullOrEmpty(CurrentPath) ? "/" : "/" + CurrentPath;
    public bool HasParentDirectory => !string.IsNullOrEmpty(CurrentPath);

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

    partial void OnSelectedGameChanged(LuaDenGame? value)
    {
        SelectedGameDetails = null;
        SelectedRoot = null;
        ClearListing();
    }

    [RelayCommand]
    private Task ConnectAsync() => RunBusyAsync(async () =>
    {
        ConfigureClient();
        IsPaired = await _client.GetPairStatusAsync();
        if (!IsPaired)
        {
            Games.Clear();
            SelectedGameDetails = null;
            ClearListing();
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
            ClearListing();
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
        SelectedRoot = null;
        ClearListing();
    });

    [RelayCommand]
    private Task OpenRootAsync(LuaDenRoot? root) => RunBusyAsync(async () =>
    {
        if (!IsPaired || SelectedGame is null || root is null || !root.Exists) return;
        ConfigureClient();
        SelectedRoot = root;
        await LoadDirectoryCoreAsync("");
    });

    [RelayCommand]
    private Task OpenEntryAsync(LuaDenFileEntry? entry) => RunBusyAsync(async () =>
    {
        if (!IsPaired || SelectedGame is null || SelectedRoot is null || entry is null || !entry.Directory)
            return;

        ConfigureClient();
        var nextPath = string.IsNullOrEmpty(CurrentPath)
            ? entry.Name
            : CurrentPath + "/" + entry.Name;
        await LoadDirectoryCoreAsync(nextPath);
    });

    [RelayCommand]
    private Task GoUpAsync() => RunBusyAsync(async () =>
    {
        if (!HasParentDirectory || SelectedGame is null || SelectedRoot is null) return;
        ConfigureClient();
        var separator = CurrentPath.LastIndexOf('/');
        var parent = separator < 0 ? "" : CurrentPath[..separator];
        await LoadDirectoryCoreAsync(parent);
    });

    [RelayCommand]
    private Task RefreshDirectoryAsync() => RunBusyAsync(async () =>
    {
        if (SelectedGame is null || SelectedRoot is null || !IsPaired) return;
        ConfigureClient();
        await LoadDirectoryCoreAsync(CurrentPath);
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
        SelectedRoot = null;
        ClearListing();

        if (!catalog.Known)
            StatusText = Resources.Strings.Ps5_Status_LibraryUnknown;
        else if (catalog.SettingUp)
            StatusText = Resources.Strings.Ps5_Status_SettingUp;
        else
            StatusText = string.Format(CultureInfo.CurrentCulture, Resources.Strings.Ps5_Status_Loaded, Games.Count);
    }

    private async Task LoadDirectoryCoreAsync(string path)
    {
        if (SelectedGame is null || SelectedRoot is null) return;
        var listing = await _client.ListFilesAsync(SelectedGame.Id, SelectedRoot.Id, path);

        Entries.Clear();
        foreach (var entry in listing.Entries)
            Entries.Add(entry);

        CurrentPath = path;
        HasListing = listing.Exists;
        ListingTruncated = listing.Truncated;
        UnshownEntries = listing.Unshown;
    }

    private void ClearListing()
    {
        Entries.Clear();
        CurrentPath = "";
        HasListing = false;
        ListingTruncated = false;
        UnshownEntries = 0;
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
                SelectedRoot = null;
                ClearListing();
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
