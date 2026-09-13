using System.Security.Claims;
using Hagale.Infrastructure.Persistence;

namespace Hagale.Api.Middleware;

public sealed class RequestAuditMiddleware(
    RequestDelegate next,
    ILogger<RequestAuditMiddleware> logger,
    TimeProvider timeProvider)
{
    public async Task InvokeAsync(HttpContext context, HagaleDbContext database)
    {
        if (!ShouldAudit(context.Request))
        {
            await next(context);
            return;
        }

        var unhandledException = false;
        try
        {
            await next(context);
        }
        catch
        {
            unhandledException = true;
            throw;
        }
        finally
        {
            var actorUserId = TryGetActorUserId(context.User);
            var statusCode = unhandledException ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            var auditLog = new AuditLog(
                actorUserId,
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                statusCode,
                context.TraceIdentifier,
                timeProvider.GetUtcNow());

            try
            {
                database.AuditLogs.Add(auditLog);
                await database.SaveChangesAsync(context.RequestAborted);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "No se pudo persistir la auditoría de la solicitud {TraceIdentifier}.", context.TraceIdentifier);
            }
        }
    }

    private static bool ShouldAudit(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !HttpMethods.IsGet(request.Method) &&
        !HttpMethods.IsHead(request.Method) &&
        !HttpMethods.IsOptions(request.Method);

    private static Guid? TryGetActorUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
