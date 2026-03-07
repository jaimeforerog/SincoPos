using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace POS.IntegrationTests;

/// <summary>
/// xUnit Collection para tests con autenticación
/// </summary>
[CollectionDefinition("POS-Auth")]
public class PosAuthCollection : ICollectionFixture<AuthenticatedWebApplicationFactory> { }

/// <summary>
/// xUnit Collection para tests con autenticación que deben ejecutarse secuencialmente
/// </summary>
[CollectionDefinition("POS-Auth-Sequential", DisableParallelization = true)]
public class PosAuthSequentialCollection : ICollectionFixture<AuthenticatedWebApplicationFactory> { }

/// <summary>
/// Factory extendida que soporta autenticación de prueba para tests de auditoría.
/// </summary>
public class AuthenticatedWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Modo estricto: sin X-Test-User → NoResult (401), para tests de seguridad
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigureAll<TestAuthHandlerOptions>(
                options => options.DefaultEmail = null);
        });
    }

    /// <summary>
    /// Crea un HttpClient configurado para autenticarse como el usuario especificado.
    /// </summary>
    /// <param name="email">Email del usuario a simular (ejemplo: admin@sincopos.com)</param>
    /// <returns>HttpClient con header X-Test-User configurado</returns>
    public HttpClient CreateAuthenticatedClient(string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", email);
        return client;
    }
}
