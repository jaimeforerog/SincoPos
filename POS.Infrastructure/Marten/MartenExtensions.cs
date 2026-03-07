using JasperFx.Events.Projections;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Events.Inventario;
using POS.Infrastructure.Data;
using POS.Infrastructure.Projections;

namespace POS.Infrastructure.Marten;

public static class MartenExtensions
{
    public static IServiceCollection AddMartenStore(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' not found in configuration.");

        // NOTA: Se llama AddMarten al FINAL de este método para minimizar el impacto
        // de BuildServiceProvider. Todos los servicios de Infrastructure deben estar
        // registrados ANTES de llamar AddMartenStore.

        services.AddMarten(opts =>
        {
            opts.Connection(connectionString);

            // Event Store en schema 'events'
            opts.Events.DatabaseSchemaName = "events";

            // Registrar tipos de eventos - Inventario (unico modulo con Event Sourcing)
            opts.Events.AddEventType<EntradaCompraRegistrada>();
            opts.Events.AddEventType<DevolucionProveedorRegistrada>();
            opts.Events.AddEventType<AjusteInventarioRegistrado>();
            opts.Events.AddEventType<SalidaVentaRegistrada>();
            opts.Events.AddEventType<StockMinimoActualizado>();

            // Auto-create schema solo en desarrollo
            if (isDevelopment)
            {
                opts.DatabaseSchemaName = "pos";
                opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.CreateOrUpdate;
            }
        })
        .UseLightweightSessions();

        // MITIGACIÓN del anti-patrón BuildServiceProvider:
        // ConfigureMarten se ejecuta DESPUÉS de AddMarten, permitiendo acceso
        // al IServiceProvider del host (ya completamente configurado)
        services.ConfigureMarten((sp, opts) =>
        {
            // Proyeccion inline: eventos de inventario -> tablas stock/lotes EF Core
            // El IServiceProvider aquí es del host, no de BuildServiceProvider
            opts.Projections.Add(
                new InventarioProjection(sp),
                ProjectionLifecycle.Inline);
        });

        return services;
    }

    public static IServiceCollection AddAppDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' not found in configuration.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(60);
            });
        });

        return services;
    }
}
