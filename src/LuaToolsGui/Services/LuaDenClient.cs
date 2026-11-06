using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LuaToolsGui.Models;

namespace LuaToolsGui.Services;

/// <summary>
/// Small HTTP client for Playden's LAN companion API. Its CookieContainer exists only in memory;
/// it is deliberately never serialized into LuaTools settings or written to disk.
/// </summary>
public sealed class LuaDenClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private HttpClient? _http;
    private string? _baseAddress;

    public bool IsConfigured => _http is not null;

    /// <summary>
    /// Configure the client for a PS5 IPv4 address. Reconfiguring the same endpoint keeps the current
    /// in-memory pairing cookie; changing the endpoint creates a fresh cookie jar.
    /// </summary>
    public void Configure(string hostAddress, int port)
    {
        var host = hostAddress.Trim();
        if (!IPAddress.TryParse(host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Enter the PS5's LAN IPv4 address (for example, 192.168.1.50).", nameof(hostAddress));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "The port must be between 1 and 65535.");

        var address = $"http://{ip}:{port}/";
        if (_http is not null && string.Equals(_baseAddress, address, StringComparison.OrdinalIgnoreCase))
            return;

        _http?.Dispose();
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            // Playden is a direct LAN HTTP service. Do not route this local traffic through a system proxy.
            UseProxy = false,
            AllowAutoRedirect = false
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(address, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(8)
        };
        _baseAddress = address;
    }

    public async Task<bool> GetPairStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendJsonAsync<LuaDenPairStatus>(
            HttpMethod.Get, "api/pair/status", requireFilesAction: false, cancellationToken);
        return result.Paired;
    }

    /// <summary>Ask the PS5 UI to display a fresh six-digit pairing code.</summary>
    public async Task StartPairingAsync(CancellationToken cancellationToken = default)
    {
        _ = await SendJsonAsync<LuaDenPairStart>(
            HttpMethod.Post, "api/pair/start", requireFilesAction: true, cancellationToken);
    }

    /// <summary>Submit the six-digit code shown on the PS5. The response's Set-Cookie header is
    /// captured automatically by the in-memory CookieContainer for subsequent API calls.</summary>
    public async Task ConfirmPairingAsync(string code, CancellationToken cancellationToken = default)
    {
        var path = "api/pair/confirm?code=" + Uri.EscapeDataString(code);
        _ = await SendJsonAsync<LuaDenPairConfirm>(
            HttpMethod.Post, path, requireFilesAction: true, cancellationToken);
    }

    public Task<LuaDenGamesResponse> GetGamesAsync(CancellationToken cancellationToken = default) =>
        SendJsonAsync<LuaDenGamesResponse>(
            HttpMethod.Get, "api/files/games", requireFilesAction: false, cancellationToken);

    public Task<LuaDenGameDetails> GetGameAsync(string gameId, CancellationToken cancellationToken = default) =>
        SendJsonAsync<LuaDenGameDetails>(
            HttpMethod.Get,
            "api/files/game?game=" + Uri.EscapeDataString(gameId),
            requireFilesAction: false,
            cancellationToken);

    /// <summary>List one root or a relative directory within a game.</summary>
    public Task<LuaDenFileListing> ListFilesAsync(
        string gameId, string rootId, string relativePath = "", CancellationToken cancellationToken = default)
    {
        var path = "api/files/list?game=" + Uri.EscapeDataString(gameId)
            + "&root=" + Uri.EscapeDataString(rootId)
            + "&path=" + Uri.EscapeDataString(relativePath);
        return SendJsonAsync<LuaDenFileListing>(
            HttpMethod.Get, path, requireFilesAction: false, cancellationToken);
    }

    /// <summary>Send a LuaTools profile to Playden. Playden imports only setManifestid version pins.</summary>
    public Task<LuaDenProfileImport> ImportLuaProfileAsync(
        uint appId, string luaSource, CancellationToken cancellationToken = default)
    {
        var content = new StringContent(luaSource, Encoding.UTF8, "text/plain");
        return SendJsonAsync<LuaDenProfileImport>(
            HttpMethod.Post,
            "api/files/lua-profile?game=" + appId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            requireFilesAction: true,
            cancellationToken,
            content);
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method, string path, bool requireFilesAction, CancellationToken cancellationToken, HttpContent? content = null)
    {
        var http = _http ?? throw new InvalidOperationException("Set the PS5 address before connecting.");
        using var request = new HttpRequestMessage(method, path);
        request.Content = content;
        // The action header is required for POST routes. Do not send an Origin header: Playden only
        // accepts browser origins that exactly match Host, while this is a native desktop client.
        if (requireFilesAction)
            request.Headers.TryAddWithoutValidation("X-Playden-Action", "files");

        using var response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new LuaDenException(DescribeFailure(response.StatusCode, body), response.StatusCode);

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new JsonException("The server returned an empty JSON response.");
        }
        catch (JsonException ex)
        {
            throw new LuaDenException("Playden returned a response LuaTools could not read.", null, ex);
        }
    }

    private static string DescribeFailure(HttpStatusCode status, string body)
    {
        var detail = body.Trim();
        var message = status switch
        {
            HttpStatusCode.Unauthorized => "This PC is not paired with Playden. Pair it again.",
            HttpStatusCode.Forbidden => "Playden rejected the request. Check that the Playden version supports the companion API.",
            HttpStatusCode.Conflict => "Playden could not apply the requested operation in its current state.",
            HttpStatusCode.TooManyRequests => "Playden is temporarily rate-limiting pairing attempts. Try again shortly.",
            HttpStatusCode.ServiceUnavailable => "Playden is busy setting up a game. Try again when setup finishes.",
            _ => $"Playden returned HTTP {(int)status} ({status})."
        };
        return string.IsNullOrEmpty(detail) ? message : $"{message} {detail}";
    }

    public void Dispose()
    {
        _http?.Dispose();
        _http = null;
        _baseAddress = null;
    }
}

/// <summary>An HTTP or payload error returned by the Playden companion service.</summary>
public sealed class LuaDenException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public LuaDenException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
