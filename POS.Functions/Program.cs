using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Services.Erp;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(config =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        // AppDbContext tiene dos constructores. DI usa el completo solo si puede resolver TODOS sus parámetros.
        // Registramos ambas dependencias para que use el constructor completo con _empresaProvider != null
        // y EmpresaId = null, lo que hace que los query filters de multi-tenant pasen todas las filas.
        services.AddSingleton<IHttpContextAccessor, NullHttpContextAccessor>();
        services.AddScoped<ICurrentEmpresaProvider, BackgroundEmpresaProvider>();

        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' no encontrada.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<ErpSincoOptions>(config.GetSection(ErpSincoOptions.SectionName));

        // ERP client: mock en dev (BaseUrl vacío), real en prod.
        // En local.settings.json, "ErpSinco__BaseUrl" mapea a config["ErpSinco:BaseUrl"].
        var erpBaseUrl = config["ErpSinco:BaseUrl"];
        if (string.IsNullOrEmpty(erpBaseUrl))
        {
            services.AddSingleton<IErpClient, MockErpClient>();
        }
        else
        {
            services.AddHttpClient<IErpClient, SincoErpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ErpSincoOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl);
                client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            });
        }
    })
    .Build();

await host.RunAsync();

// ICurrentEmpresaProvider sin contexto HTTP — EmpresaId null hace pasar todos los query filters.
internal sealed class BackgroundEmpresaProvider : ICurrentEmpresaProvider
{
    public int? EmpresaId { get; set; } = null;
}

internal sealed class NullHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = null;
}
