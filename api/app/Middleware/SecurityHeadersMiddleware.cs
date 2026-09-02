using Microsoft.AspNetCore.Http;

namespace SeniorCare.Api.App.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
            headers.TryAdd(
                "Referrer-Policy",
                "strict-origin-when-cross-origin"
            );
            headers.TryAdd(
                "Permissions-Policy",
                "camera=(self), microphone=(self), geolocation=()"
            );

            return Task.CompletedTask;
        });

        await next(context);
    }
}