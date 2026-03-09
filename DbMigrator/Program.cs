using Microsoft.EntityFrameworkCore;
using POS.Infrastructure.Data;

var connectionString = "Host=sincopos-pg.postgres.database.azure.com;Port=5432;Database=sincopos;Username=sincopos_admin;Password=P@ssw0rdS1ncoP0s2026!;Ssl Mode=Require;Trust Server Certificate=true;Maximum Pool Size=100;Minimum Pool Size=5;Connection Idle Lifetime=300;Include Error Detail=True;";

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var context = new AppDbContext(optionsBuilder.Options, null);

Console.WriteLine("Applying missing migration AddUsuarioSucursalesTable...");

var sql = @"
    DROP TABLE IF EXISTS public.usuario_sucursales CASCADE;

    CREATE TABLE public.usuario_sucursales (
        usuario_id integer NOT NULL,
        sucursal_id integer NOT NULL,
        asignado_por text,
        fecha_asignacion timestamp with time zone NOT NULL,
        CONSTRAINT ""PK_usuario_sucursales"" PRIMARY KEY (usuario_id, sucursal_id),
        CONSTRAINT ""FK_usuario_sucursales_sucursales_SucursalId"" FOREIGN KEY (sucursal_id) REFERENCES public.sucursales (""Id"") ON DELETE CASCADE,
        CONSTRAINT ""FK_usuario_sucursales_usuarios_UsuarioId"" FOREIGN KEY (usuario_id) REFERENCES public.usuarios (id) ON DELETE CASCADE
    );

    CREATE INDEX ""IX_usuario_sucursales_SucursalId"" ON public.usuario_sucursales (sucursal_id);
";

try 
{
    await context.Database.ExecuteSqlRawAsync(sql);
    Console.WriteLine("Table usuario_sucursales successfully created in Azure PostgreSQL.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

