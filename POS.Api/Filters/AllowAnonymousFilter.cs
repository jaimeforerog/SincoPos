using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace POS.Api.Filters;

/// <summary>
/// Filtro que permite acceso anónimo en desarrollo
/// </summary>
public class AllowAnonymousFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Permitir todo en desarrollo
        return Task.CompletedTask;
    }
}
