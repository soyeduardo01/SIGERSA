using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SIGERSA.Application.Users;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Api.Controllers;

[ApiController]
[Route("api/v1/registrations")]
[AllowAnonymous]
public sealed class RegistrationsController(
    PublicRegistrationService service,
    IOptions<SupabaseOptions> storageOptions) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_291_456)]
    public async Task<ActionResult<PublicRegistrationResult>> Register(
        [FromForm] PublicRegistrationForm form,
        CancellationToken cancellationToken)
    {
        if (form.AuthorizationLetter is null)
            throw new ArgumentException("La carta de autorización es obligatoria.");

        await using var content = form.AuthorizationLetter.OpenReadStream();
        var result = await service.RegisterAsync(
            new PublicRegistrationRequest(
                form.NombreCompleto,
                form.TipoIdentificacion,
                form.Identificacion,
                form.Correo,
                form.Telefono,
                form.Rol,
                form.Password,
                form.TermsAccepted),
            storageOptions.Value.DefaultBucketName,
            form.AuthorizationLetter.FileName,
            form.AuthorizationLetter.ContentType,
            form.AuthorizationLetter.Length,
            content,
            cancellationToken);
        return Created($"/api/v1/registrations/{result.Id}", result);
    }
}

public sealed class PublicRegistrationForm
{
    public string NombreCompleto { get; init; } = string.Empty;
    public string TipoIdentificacion { get; init; } = string.Empty;
    public string Identificacion { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string? Telefono { get; init; }
    public string Rol { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool TermsAccepted { get; init; }
    public IFormFile? AuthorizationLetter { get; init; }
}
