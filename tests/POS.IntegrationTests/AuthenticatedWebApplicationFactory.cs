using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        builder.ConfigureTestServices(services =>
        {
            // Remover autenticación JWT existente
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.RemoveAll<IAuthenticationHandlerProvider>();

            // Configurar TestAuthHandler como autenticación principal
            services.AddAuthentication(defaultScheme: TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    options => { });

            // Configurar políticas de autorización para aceptar TestAuth
            services.AddAuthorization(options =>
            {
                // Reconfigurar las políticas existentes para aceptar TestAuth
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName)
                    .Build();

                // Políticas de roles con verificación de rol
                options.AddPolicy("Admin", policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole("admin") // Verificar rol admin
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));

                options.AddPolicy("Supervisor", policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole("supervisor", "admin") // Supervisor o Admin
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));

                options.AddPolicy("Cajero", policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole("cajero", "supervisor", "admin") // Cajero, Supervisor o Admin
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName));
            });
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
