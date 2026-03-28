using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});

builder.Services.AddHttpClient("gateway")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All
    });

var routes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["/api/auth"] = "http://identity-api:8080",
    ["/api/users"] = "http://users-api:8080",
    ["/api/products"] = "http://products-api:8080",
    ["/api/cart"] = "http://cart-api:8080",
    ["/api/orders"] = "http://ordering-api:8080",
    ["/api/payments"] = "http://payments-api:8080",
    ["/api/shipments"] = "http://shipping-api:8080"
};

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    service = "gateway",
    routes = routes.Keys.OrderBy(x => x)
}));

app.MapMethods("/{**path}", new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }, async Task (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
    var route = routes
        .OrderByDescending(x => x.Key.Length)
        .FirstOrDefault(x => context.Request.Path.StartsWithSegments(x.Key, StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrWhiteSpace(route.Key))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { message = "No downstream route matched the request." });
        return;
    }

    var targetUri = new Uri($"{route.Value}{context.Request.Path}{context.Request.QueryString}");
    var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    foreach (var header in context.Request.Headers)
    {
        if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
        {
            requestMessage.Content ??= new StreamContent(context.Request.Body);
            requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    if (context.Request.ContentLength > 0)
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
        if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
        {
            requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
        }
    }

    var response = await httpClientFactory.CreateClient("gateway")
        .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

    context.Response.StatusCode = (int)response.StatusCode;

    foreach (var header in response.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in response.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    context.Response.Headers.Remove("transfer-encoding");
    await response.Content.CopyToAsync(context.Response.Body);
});

app.Run();
