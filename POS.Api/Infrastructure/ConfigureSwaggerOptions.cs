using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace POS.Api.Infrastructure;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
        => _provider = provider;

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "SincoPos API",
                Version = description.ApiVersion.ToString(),
                Description = description.IsDeprecated
                    ? """
                        **Esta versión está obsoleta.** Use una versión más reciente.

                        **Roles de acceso** (de mayor a menor permiso):
                        - `Admin` → acceso total
                        - `Supervisor` → gestión de inventario, compras, traslados, devoluciones
                        - `Cajero` → ventas, consultas
                        - `Vendedor` → solo lectura de productos y precios

                        Autenticación vía **Keycloak** (JWT Bearer). Realm: `sincopos`.
                        """
                    : """
                        API del sistema de Punto de Venta SincoPos.

                        **Roles de acceso** (de mayor a menor permiso):
                        - `Admin` → acceso total
                        - `Supervisor` → gestión de inventario, compras, traslados, devoluciones
                        - `Cajero` → ventas, consultas
                        - `Vendedor` → solo lectura de productos y precios

                        Autenticación vía **Keycloak** (JWT Bearer). Realm: `sincopos`.
                        """
            });
        }

        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Token JWT de Keycloak. Obtener en http://localhost:8080/realms/sincopos"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
