using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIGERSA.Application.Authentication;
using SIGERSA.Application.Evaluations;
using SIGERSA.Application.Evidences;
using SIGERSA.Application.InspectionTemplates;
using SIGERSA.Application.Parameters;
using SIGERSA.Application.Risk;

namespace SIGERSA.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        services.AddOptions<AuthFlowOptions>()
            .Bind(configuration.GetSection(AuthFlowOptions.SectionName))
            .Validate(options => options.MaximumLoginAttempts > 0, "El máximo de intentos de acceso debe ser positivo.")
            .Validate(options => options.LockoutMinutes > 0, "La duración del bloqueo debe ser positiva.")
            .Validate(options => options.OtpLifetimeMinutes > 0, "La vigencia del OTP debe ser positiva.")
            .Validate(options => options.MaximumOtpAttempts > 0, "El máximo de intentos OTP debe ser positivo.")
            .Validate(options => options.RefreshTokenDays > 0, "La vigencia del refresh token debe ser positiva.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ParametersService>();
        services.AddScoped<AllItemsService>();
        services.AddScoped<EvidenceService>();
        services.AddScoped<InspectionRiskService>();
        services.AddScoped<EvaluationWorkflowService>();
        return services;
    }
}
