var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Endpoint de diagnóstico para comprobar inserciones/lectura de trabajadores
// Se define antes del middleware de autenticación para evitar redirecciones.
app.MapGet("/Users/TestTrabajadores", async (Microsoft.Extensions.Configuration.IConfiguration configuration) =>
{
    try
    {
        var conn = configuration.GetConnectionString("DefaultConnection");
        using (var conexion = new MySql.Data.MySqlClient.MySqlConnection(conn))
        {
            await conexion.OpenAsync();
            using (var cmd = new MySql.Data.MySqlClient.MySqlCommand("SELECT COUNT(*) FROM trabajadores", conexion))
            {
                var r = await cmd.ExecuteScalarAsync();
                int count = 0;
                if (r != null && r != System.DBNull.Value) int.TryParse(r.ToString(), out count);
                return Results.Content($"trabajadores count: {count}", "text/plain");
            }
        }
    }
    catch (System.Exception ex)
    {
        var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        return Results.Content($"Error TestTrabajadores: {msg}", "text/plain");
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Middleware de autenticación DESACTIVADO temporalmente.
// Para reactivar, restaura el bloque de middleware que comprueba la cookie "user_email"
// y redirige a /Users/Login. Actualmente todas las rutas son accesibles sin inicio de sesión.
// Middleware simple de autenticación por cookie
app.Use(async (context, next) =>
{
    // Rutas públicas que no requieren autenticación
    var path = context.Request.Path.Value ?? string.Empty;
    var publicPrefixes = new[] { "/users/login", "/users/create", "/users/testbcrypt", "/users/testtrabajadores", "/lib/", "/css/", "/js/", "/favicon.ico", "/home/error", "/api/" };
    // No considerar la raíz ("/") como pública: así la página principal pedirá login si no hay cookie
    bool isPublic = publicPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    if (isPublic)
    {
        await next();
        return;
    }

    // Comprobar cookie simple de sesión (user_email)
    if (!context.Request.Cookies.ContainsKey("user_email") || string.IsNullOrWhiteSpace(context.Request.Cookies["user_email"]))
    {
        // Redirigir a la página de login
        context.Response.Redirect("/Users/Login");
        return;
    }

    await next();
});
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
