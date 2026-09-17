using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIGERSA.Application.Operations;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Caching;
using SIGERSA.Infrastructure.Caching;
using SIGERSA.Domain.Security;
using SIGERSA.Domain.Storage;
using SIGERSA.Infrastructure.Configuration;
using SIGERSA.Infrastructure.Email;
using SIGERSA.Infrastructure.Persistence;
using SIGERSA.Infrastructure.Reports;
using SIGERSA.Infrastructure.Security;
using SIGERSA.Infrastructure.Storage;

namespace SIGERSA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Se requiere Database:ConnectionString.")
            .Validate(options => options.DefaultCommandTimeoutSeconds > 0, "El timeout de base de datos debe ser positivo.")
            .ValidateOnStart();

        services.AddOptions<SupabaseOptions>()
            .Bind(configuration.GetSection(SupabaseOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out _), "Supabase:Url debe ser una URL absoluta.")
            .Validate(options => SupabaseOptions.IsServerCredential(options.Key),
                "Supabase:Key debe ser una clave secreta sb_secret_ válida o un JWT legacy con rol service_role; no use la clave publicable/anon del frontend.")
            .Validate(options => options.MaxFileSizeBytes > 0, "El tamaño máximo debe ser positivo.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Se requiere Authentication:Jwt:Issuer.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Se requiere Authentication:Jwt:Audience.")
            .Validate(options => options.SigningKey.Length >= 32, "La clave JWT debe contener al menos 32 caracteres.")
            .Validate(options => options.AccessTokenMinutes > 0 && options.ResetTokenMinutes > 0, "Las vigencias JWT deben ser positivas.")
            .ValidateOnStart();

        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host), "Se requiere Smtp:Host.")
            .Validate(options => !options.Enabled || options.Port > 0, "Smtp:Port debe ser positivo.")
            .Validate(options => !options.Enabled || MailAddress.TryCreate(options.FromAddress, out _), "Smtp:FromAddress debe ser válido.")
            .Validate(options => Uri.TryCreate(options.ApplicationUrl, UriKind.Absolute, out _), "Smtp:ApplicationUrl debe ser una URL absoluta.")
            .Validate(options => Uri.TryCreate(options.PasswordRecoveryUrl, UriKind.Absolute, out _), "Smtp:PasswordRecoveryUrl debe ser una URL absoluta.")
            .Validate(options => !options.Enabled || string.IsNullOrWhiteSpace(options.Username) == string.IsNullOrWhiteSpace(options.Password), "Smtp:Username y Smtp:Password deben configurarse juntos.")
            .ValidateOnStart();

        var databaseOptions = configuration
            .GetRequiredSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>()
            ?? throw new InvalidOperationException("No se encontró la configuración Database.");

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(databaseOptions.ConnectionString)
        {
            SearchPath = "\"SIGERSA\"",
            CommandTimeout = databaseOptions.DefaultCommandTimeoutSeconds,
            ApplicationName = "SIGERSA"
        };

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionStringBuilder.ConnectionString));
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        services.AddMemoryCache();
        services.AddSingleton<ICacheStore, InMemoryCacheStore>();

        var supabaseOptions = configuration
            .GetRequiredSection(SupabaseOptions.SectionName)
            .Get<SupabaseOptions>()
            ?? throw new InvalidOperationException("No se encontró la configuración Supabase.");

        services.AddSingleton(_ => new Supabase.Client(
            supabaseOptions.Url,
            supabaseOptions.Key,
            new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = false,
                AutoRefreshToken = false
            }));

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IParametersControlRepository, ParametersControlRepository>();
        services.AddScoped<IAllItemsRepository, AllItemsRepository>();
        services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.AddScoped<IEvidenceRepository, EvidenceRepository>();
        services.AddScoped<IEstablishmentRepository, EstablishmentRepository>();
        services.AddScoped<IEvaluationWorkflowRepository, EvaluationWorkflowRepository>();
        services.AddScoped<IInspectionRequestRepository, InspectionRequestRepository>();
        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<ISchedulingRepository, SchedulingRepository>();
        services.AddScoped<ICorrectionRepository, CorrectionRepository>();
        services.AddScoped<IOperationalRepository, OperationalRepository>();
        services.AddScoped<ISupportingDocumentRepository, SupportingDocumentRepository>();
        services.AddScoped<IFileStorage, SupabaseStorageAdapter>();
        services.AddSingleton<IInstitutionalPdfRenderer, ChromiumInstitutionalPdfRenderer>();
        services.AddSingleton<IPasswordService, BcryptPasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<ISupabaseMfaGateway, SupabaseMfaGateway>();

        return services;
    }
}
