using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hagale.Api.Middleware;
using Hagale.Api.Realtime;
using Hagale.Application.Drivers;
using Hagale.Application.Rides;
using Hagale.Infrastructure.Authentication;
using Hagale.Infrastructure.DependencyInjection;
using Hagale.Infrastructure.Identity;
using Hagale.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la configuración JWT.");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Configura Jwt:SigningKey en secretos de usuario o variables de entorno con al menos 32 bytes.");
}

builder.Services.AddHagaleInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRideRealtimeNotifier, SignalRRideRealtimeNotifier>();
builder.Services.AddSingleton<IDriverApplicationRealtimeNotifier, SignalRDriverApplicationRealtimeNotifier>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.Events = new JwtBearerEvents
        {
            // SignalR usa WebSocket en el teléfono. El encabezado Authorization
            // no se puede enviar durante el handshake nativo, por eso se acepta
            // el token únicamente en la ruta autenticada de este hub.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments(RideEventsHub.HubPath))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // En producción HÁGALE queda detrás de un proxy local HTTPS y el puerto de
    // Kestrel no se expone públicamente. El proxy debe ser el único que pueda
    // hablar con la API por el puerto interno.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
var isDemoEnvironment = app.Environment.IsEnvironment("Demo");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
}

// La sesión de prueba se publica por HTTP para que el teléfono pueda abrirla
// dentro de la red local. En producción real se fuerza HTTPS; en Demo el
// proveedor gratuito entrega HTTPS desde su proxy externo.
if (!app.Environment.IsDevelopment() && !isDemoEnvironment)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    // Google Identity Services recomienda permitir popups del proveedor de
    // identidad sin aislarlos por completo del origen de HÁGALE.
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseDefaultFiles();
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-store, max-age=0"
    });
}
else
{
    app.UseStaticFiles();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestAuditMiddleware>();

if (app.Environment.IsDevelopment() || isDemoEnvironment)
{
    await app.Services.InitializeDevelopmentDatabaseAsync();
    await app.Services.EnsureConfiguredAdministratorAsync(builder.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();
app.MapHub<RideEventsHub>(RideEventsHub.HubPath);

app.Run();

public partial class Program;
