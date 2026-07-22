using System.Security.Cryptography;
using AracParki.Application.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AracParki.Web.Infrastructure;

public static class SecurityHeadersExtensions
{
    public const string CspNonceItemKey = "CspNonce";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var seo = app.ApplicationServices.GetRequiredService<IOptions<SeoSettings>>().Value;
        var connectSrc = env.IsDevelopment()
            ? "connect-src 'self' ws://localhost:* ws://127.0.0.1:* wss://localhost:* wss://127.0.0.1:*"
            : "connect-src 'self'";
        if (seo.HasGoogleAnalytics)
        {
            connectSrc += " https://www.google-analytics.com https://analytics.google.com https://www.googletagmanager.com";
        }

        connectSrc += "; ";

        var scriptSrcExtra = seo.HasGoogleAnalytics
            ? " https://www.googletagmanager.com"
            : string.Empty;

        return app.Use(async (context, next) =>
        {
            var nonceBytes = RandomNumberGenerator.GetBytes(16);
            var nonce = Convert.ToBase64String(nonceBytes);
            context.Items[CspNonceItemKey] = nonce;

            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'{scriptSrcExtra}; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com data:; " +
                "img-src 'self' data: blob: https:; " +
                connectSrc +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";

            await next();
        });
    }

    public static string? GetCspNonce(this HttpContext httpContext) =>
        httpContext.Items.TryGetValue(CspNonceItemKey, out var value)
            ? value as string
            : null;
}
