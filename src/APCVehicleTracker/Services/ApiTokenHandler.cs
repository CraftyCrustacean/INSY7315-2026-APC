using Microsoft.Identity.Web;
using System.Net.Http.Headers;

namespace APCVehicleTracker.Services;

public class ApiTokenHandler : DelegatingHandler
{
    private readonly ITokenAcquisition _tokens;
    private readonly string[] _scopes;

    public ApiTokenHandler(ITokenAcquisition tokens, IConfiguration config)
    {
        _tokens = tokens;
        _scopes = config.GetSection("VehicleApi:Scopes").Get<string[]>()
            ?? throw new InvalidOperationException("VehicleApi:Scopes hasnt been configured");
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAccessTokenForUserAsync(_scopes);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
