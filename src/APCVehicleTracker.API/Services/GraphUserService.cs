using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace APCVehicleTracker.API.Services;

public interface IGraphUserService
{
    Task<string> CreateUserAsync(string firstName, string lastName, string email, string temporaryPassword, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(string objecancellationTokenId, string temporaryPassword, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string objecancellationTokenId, CancellationToken cancellationToken = default);
}

public class GraphUserService : IGraphUserService
{
    private readonly IConfiguration _config;
    private GraphServiceClient? _client;

    public GraphUserService(IConfiguration config) => _config = config;

    private GraphServiceClient Client => _client ??= CreateClient();

    private GraphServiceClient CreateClient()
    {
        var secret = _config["AzureAd:ClientSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("AzureAd:Client secret isnt configured for the api.");

        var credential = new ClientSecretCredential(_config["AzureAd:TenantId"], _config["AzureAd:ClientId"], secret);
        return new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });
    }

    private string Issuer => _config["AzureAd:Domain"] ?? throw new InvalidOperationException("AzureAd:Domain isnt configured.");

    public async Task<string> CreateUserAsync(string firstName, string lastName, string email, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            AccountEnabled = true,
            DisplayName = $"{firstName} {lastName}",
            GivenName = firstName,
            Surname = lastName,
            Identities = new List<ObjectIdentity>
            {
                new() { SignInType = "emailAddress", Issuer = Issuer, IssuerAssignedId = email }
            },
            PasswordProfile = new PasswordProfile
            {
                Password = temporaryPassword,
                ForceChangePasswordNextSignIn = true
            }
        };

        try
        {
            var created = await Client.Users.PostAsync(user, cancellationToken: cancellationToken);
            return created?.Id ?? throw new InvalidOperationException("Graph didnt return the new users ID.");
        }
        catch (ODataError e) when (e.Error?.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new AdminException(StaffAdminErrorType.DuplicateEmail, "An account with this email already exists in Entra.");
        }
    }

    public async Task ResetPasswordAsync(string objecancellationTokenId, string temporaryPassword, CancellationToken cancellationToken = default)
        => await Client.Users[objecancellationTokenId].PatchAsync(new User
        {
            PasswordProfile = new PasswordProfile { Password = temporaryPassword, ForceChangePasswordNextSignIn = true }
        }, cancellationToken: cancellationToken);

    public Task DeleteUserAsync(string objecancellationTokenId, CancellationToken cancellationToken = default)
        => Client.Users[objecancellationTokenId].DeleteAsync(cancellationToken: cancellationToken);
}
