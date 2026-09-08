using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SIGERSA.Domain.Repositories;
using SIGERSA.Domain.Storage;
using SIGERSA.Infrastructure.Configuration;
using SIGERSA.Infrastructure.Persistence;
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
            .Validate(options => !string.IsNullOrWhiteSpace(options.Key), "Se requiere Supabase:Key.")
            .Validate(options => options.MaxFileSizeBytes > 0, "El tamaño máximo debe ser positivo.")
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
        services.AddScoped<IFileStorage, SupabaseStorageAdapter>();

        return services;
    }
}
