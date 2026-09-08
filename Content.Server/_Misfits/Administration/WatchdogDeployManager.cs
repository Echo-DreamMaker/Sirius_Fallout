using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Log;

namespace Content.Server._Misfits.Administration;

/// <summary>
/// Server-side bridge to the SS14.Watchdog instance update endpoint.
/// </summary>
public sealed class WatchdogDeployManager : IPostInjectInit
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private readonly HttpClient _http = new();
    private ISawmill _log = default!;
    private readonly object _requestLock = new();
    private Task<bool>? _pendingRequest;

    void IPostInjectInit.PostInject()
    {
        _log = _logManager.GetSawmill("watchdog-deploy");
    }

    public Task<bool> RequestDeployAsync()
    {
        // Prevent button double-clicks and repeated console invocations from issuing
        // concurrent update checks to the watchdog.
        lock (_requestLock)
        {
            if (_pendingRequest != null)
                return _pendingRequest;

            var request = RequestDeployInnerAsync();
            _pendingRequest = request;
            _ = ClearCompletedRequestAsync(request);
            return request;
        }
    }

    /// <summary>
    /// Clears only the request this call created. This runs after <see cref="_pendingRequest"/>
    /// has been assigned, including when configuration validation fails synchronously.
    /// </summary>
    private async Task ClearCompletedRequestAsync(Task<bool> request)
    {
        try
        {
            await request;
        }
        finally
        {
            lock (_requestLock)
            {
                if (ReferenceEquals(_pendingRequest, request))
                    _pendingRequest = null;
            }
        }
    }

    private async Task<bool> RequestDeployInnerAsync()
    {
        try
        {
            var token = _configuration.GetCVar(CCVars.WatchdogDeployApiToken);
            var key = _configuration.GetCVar(CVars.WatchdogKey);
            var baseUrl = _configuration.GetCVar(CVars.WatchdogBaseUrl);

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(key))
            {
                _log.Warning("Deploy request refused: watchdog deploy token or instance key is not configured.");
                return false;
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                _log.Warning("Deploy request refused: watchdog base URL is invalid.");
                return false;
            }

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{token}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, $"instances/{Uri.EscapeDataString(key)}/update"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _log.Warning("Watchdog deploy request failed with HTTP status {Status}.", response.StatusCode);
                return false;
            }

            _log.Info("Watchdog deploy check requested successfully.");
            return true;
        }
        catch (HttpRequestException e)
        {
            _log.Warning("Watchdog deploy request could not reach the watchdog: {Error}", e.Message);
            return false;
        }
        catch (Exception e)
        {
            // The command must not crash the invoking admin's console task. Do not include
            // request headers or configuration values here: they contain the API token.
            _log.Error("Watchdog deploy request failed unexpectedly: {Error}", e.Message);
            return false;
        }
    }
}
