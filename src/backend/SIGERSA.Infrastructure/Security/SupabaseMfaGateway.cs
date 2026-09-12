using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Infrastructure.Security;

public sealed class SupabaseMfaGateway(
    HttpClient httpClient,
    IOptions<SupabaseOptions> options) : ISupabaseMfaGateway
{
    private readonly SupabaseOptions _options = options.Value;

    public async Task<Guid> EnsureUserAsync(
        Guid? supabaseUserId,
        string email,
        string password,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        if (supabaseUserId is { } existingId)
        {
            await UpdateUserCoreAsync(existingId, email, password, fullName, cancellationToken);
            return existingId;
        }

        using var request = CreateRequest(HttpMethod.Post, "auth/v1/admin/users");
        request.Content = JsonContent.Create(new
        {
            email,
            password,
            email_confirm = true,
            user_metadata = new { full_name = fullName, application = "SIGERSA" }
        });
        using var response = await SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await ReadUserIdAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict)
        {
            var recovered = await FindUserByEmailAsync(email, cancellationToken);
            if (recovered is { } recoveredId)
            {
                await UpdateUserCoreAsync(recoveredId, email, password, fullName, cancellationToken);
                return recoveredId;
            }
        }

        throw ProviderError(response.StatusCode);
    }

    public Task UpdateUserAsync(
        Guid supabaseUserId,
        string? email,
        string? password,
        CancellationToken cancellationToken = default) =>
        UpdateUserCoreAsync(supabaseUserId, email, password, null, cancellationToken);

    public async Task<Guid> ValidateAal2TokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 16_384)
            throw new UnauthorizedAccessException("El comprobante MFA no es válido.");

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
            throw new UnauthorizedAccessException("El comprobante MFA no es válido.");
        var token = handler.ReadJwtToken(accessToken);
        if (token.Claims.FirstOrDefault(claim => claim.Type == "aal")?.Value != "aal2")
            throw new UnauthorizedAccessException("Debe completar el segundo factor de autenticación.");

        using var request = CreateRequest(HttpMethod.Get, "auth/v1/user", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("El comprobante MFA expiró o no es válido.");
        return await ReadUserIdAsync(response, cancellationToken);
    }

    private async Task UpdateUserCoreAsync(
        Guid userId,
        string? email,
        string? password,
        string? fullName,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, $"auth/v1/admin/users/{userId:D}");
        var attributes = new Dictionary<string, object?> { ["email_confirm"] = true };
        if (!string.IsNullOrWhiteSpace(email)) attributes["email"] = email;
        if (!string.IsNullOrWhiteSpace(password)) attributes["password"] = password;
        if (!string.IsNullOrWhiteSpace(fullName))
            attributes["user_metadata"] = new { full_name = fullName, application = "SIGERSA" };
        request.Content = JsonContent.Create(attributes);
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw ProviderError(response.StatusCode);
    }

    private async Task<Guid?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, "auth/v1/admin/users?page=1&per_page=1000");
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw ProviderError(response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("users", out var users)) return null;
        foreach (var user in users.EnumerateArray())
        {
            if (!user.TryGetProperty("email", out var userEmail) ||
                !string.Equals(userEmail.GetString(), email, StringComparison.OrdinalIgnoreCase)) continue;
            if (user.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? bearer = null)
    {
        var baseUri = new Uri(_options.Url.TrimEnd('/') + "/", UriKind.Absolute);
        var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        request.Headers.TryAddWithoutValidation("apikey", _options.Key);
        request.Headers.Authorization = new("Bearer", bearer ?? _options.Key);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new MfaProviderUnavailableException(
                "No fue posible comunicarse con el servicio MFA de Supabase.", exception);
        }
    }

    private static async Task<Guid> ReadUserIdAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("id", out var id) &&
            Guid.TryParse(id.GetString(), out var parsed)) return parsed;
        if (document.RootElement.TryGetProperty("user", out var user) &&
            user.TryGetProperty("id", out id) && Guid.TryParse(id.GetString(), out parsed)) return parsed;
        throw new MfaProviderUnavailableException("Supabase devolvió una identidad MFA no válida.");
    }

    private static MfaProviderUnavailableException ProviderError(HttpStatusCode status) =>
        new($"Supabase no pudo completar la operación MFA (estado {(int)status}).");
}
