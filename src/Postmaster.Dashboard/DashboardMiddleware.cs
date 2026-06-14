using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Postmaster.Dashboard
{
    internal sealed class DashboardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _prefix;
        private readonly DashboardOptions _options;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public DashboardMiddleware(RequestDelegate next, string prefix, DashboardOptions options)
        {
            _next = next;
            _prefix = prefix.TrimEnd('/');
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            // Only intercept paths that start with the configured prefix
            if (!path.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
                || (path.Length > _prefix.Length && path[_prefix.Length] != '/'))
            {
                await _next(context);
                return;
            }

            if (!IsAuthenticated(context))
            {
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Postmaster\"";
                context.Response.StatusCode = 401;
                return;
            }

            var sub = path[_prefix.Length..].TrimStart('/');

            if (IsStaticAsset(sub))
            {
                var ct = sub.EndsWith(".css") ? "text/css" : "application/javascript";
                await ServeEmbeddedFile(context, sub.Replace('/', '.'), ct, replacePlaceholders: false);
                return;
            }

            if (sub.StartsWith("api/", StringComparison.OrdinalIgnoreCase)
                || sub.Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                var apiPath = sub.Length > 4 ? sub[4..].TrimStart('/') : "";
                await HandleApiAsync(context, apiPath);
                return;
            }

            await ServeEmbeddedFile(context, "index.html", "text/html; charset=utf-8");
        }

        private bool IsAuthenticated(HttpContext context)
        {
            if (_options.Username == null) return true;

            var header = context.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
                var colon = credentials.IndexOf(':');
                if (colon < 0) return false;

                return credentials[..colon] == _options.Username
                    && credentials[(colon + 1)..] == (_options.Password ?? "");
            }
            catch { return false; }
        }

        private static async Task HandleApiAsync(HttpContext context, string apiPath)
        {
            var manager = context.RequestServices.GetRequiredService<IOutboxManager>();
            var method = context.Request.Method;

            try
            {
                if (apiPath == "stats" && method == HttpMethods.Get)
                {
                    await WriteJsonAsync(context, await manager.GetStatsAsync(context.RequestAborted));
                    return;
                }

                if (apiPath == "messages" && method == HttpMethods.Get)
                {
                    var q = context.Request.Query;
                    var query = new OutboxQuery
                    {
                        Status = q.TryGetValue("status", out var sv) && int.TryParse(sv, out var si)
                            ? (OutboxMessageStatus)si : null,
                        Channel = q.TryGetValue("channel", out var cv) ? (string?)cv : null,
                        CorrelationId = q.TryGetValue("correlationId", out var civ) ? (string?)civ : null,
                        MetadataContains = q.TryGetValue("metadata", out var mv) ? (string?)mv : null,
                        From = q.TryGetValue("from", out var fv) && DateTime.TryParse(fv, out var fd) ? fd.ToUniversalTime() : null,
                        To = q.TryGetValue("to", out var tv) && DateTime.TryParse(tv, out var td) ? td.ToUniversalTime() : null,
                        Page = q.TryGetValue("page", out var pv) && int.TryParse(pv, out var pi) ? pi : 1,
                        PageSize = q.TryGetValue("pageSize", out var psv) && int.TryParse(psv, out var psi) ? psi : 20,
                    };
                    await WriteJsonAsync(context, await manager.GetAsync(query, context.RequestAborted));
                    return;
                }

                if (apiPath.StartsWith("messages/", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = apiPath["messages/".Length..];
                    var segments = rest.Split('/');

                    if (!Guid.TryParse(segments[0], out var id))
                    {
                        context.Response.StatusCode = 400;
                        return;
                    }

                    if (segments.Length == 1 && method == HttpMethods.Get)
                    {
                        var detail = await manager.GetByIdAsync(id, context.RequestAborted);
                        if (detail == null) { context.Response.StatusCode = 404; return; }
                        await WriteJsonAsync(context, detail);
                        return;
                    }

                    if (segments.Length == 2 && segments[1] == "reset" && method == HttpMethods.Post)
                    {
                        await manager.ResetAsync(id, context.RequestAborted);
                        context.Response.StatusCode = 204;
                        return;
                    }

                    if (segments.Length == 2 && segments[1] == "cancel" && method == HttpMethods.Post)
                    {
                        await manager.CancelAsync(id, context.RequestAborted);
                        context.Response.StatusCode = 204;
                        return;
                    }
                }

                if (apiPath.StartsWith("channels/", StringComparison.OrdinalIgnoreCase) && method == HttpMethods.Post)
                {
                    var rest = apiPath["channels/".Length..];
                    var parts = rest.Split('/');
                    if (parts.Length == 2 && parts[1] == "reset")
                    {
                        await manager.ResetChannelAsync(WebUtility.UrlDecode(parts[0]), context.RequestAborted);
                        context.Response.StatusCode = 204;
                        return;
                    }
                }

                context.Response.StatusCode = 404;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(ex.Message);
            }
        }

        private static bool IsStaticAsset(string sub)
        {
            if (sub == "app.js") return true;
            if (sub.StartsWith("vendor/", StringComparison.OrdinalIgnoreCase)
                && (sub.EndsWith(".js") || sub.EndsWith(".css"))) return true;
            return false;
        }

        private static async Task WriteJsonAsync(HttpContext context, object value)
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(value, JsonOptions));
        }

        private async Task ServeEmbeddedFile(HttpContext context, string resourceSuffix, string contentType, bool replacePlaceholders = true)
        {
            var assembly = typeof(DashboardMiddleware).Assembly;
            var resourceName = $"Postmaster.Dashboard.wwwroot.{resourceSuffix}";
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = contentType;

            if (!replacePlaceholders)
            {
                await stream.CopyToAsync(context.Response.Body);
                return;
            }

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            var effectivePrefix = (_options.PathBase?.TrimEnd('/') ?? "") + _prefix;
            content = content.Replace("{{PREFIX}}", effectivePrefix);
            await context.Response.WriteAsync(content);
        }
    }
}
