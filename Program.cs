using umbraco_cms_task.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
            "http://localhost:4200",
            "https://localhost:4200",
            "http://sitea.local:4200",
            "https://sitea.local:4200",
            "http://siteb.local:4200",
            "https://siteb.local:4200",
            "http://merged.local:4200",
            "https://merged.local:4200"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

builder.Services.AddScoped<ContentMigrationService>();
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseCors("AllowAngular");
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
    var host = context.Request.Host.Host;
    var port = context.Request.Host.Port;

    // only redirect on port 4200 (Angular), not 44392 (Umbraco Razor)
    if (port == 44392)
    {
        await next();
        return;
    }

    var redirectMap = new Dictionary<(string host, string path), string>
    {
        // site-a redirects → merged
        { ("sitea.local", ""), "https://merged.local:44392/home-site-a" },
        { ("sitea.local", "/blog-posts"), "https://merged.local:44392/home-site-a/blog-posts-site-a" },
        { ("sitea.local", "/blog-posts/blog-post-1"), "https://merged.local:44392/home-site-a/blog-posts-site-a/blog-post-1-site-a" },
        { ("sitea.local", "/blog-posts/blog-post-2"), "https://merged.local:44392/home-site-a/blog-posts-site-a/blog-post-2-site-a" },
        { ("sitea.local", "/contact"), "https://merged.local:44392/home-site-a/contact-site-a" },

        // site-b redirects → merged
        { ("siteb.local", ""), "https://merged.local:44392/uniphar-retail-home-site-b" },
        { ("siteb.local", "/blog-posts"), "https://merged.local:44392/uniphar-retail-home-site-b/blog-posts-site-b" },
        { ("siteb.local", "/blog-posts/blog-1"), "https://merged.local:44392/uniphar-retail-home-site-b/blog-posts-site-b/blog-1-site-b" },
        { ("siteb.local", "/contact"), "https://merged.local:44392/uniphar-retail-home-site-b/contact-site-b" },
    };

    if (redirectMap.TryGetValue((host, path), out var newUrl))
    {
        context.Response.Redirect(newUrl, permanent: true);
        return;
    }

    await next();
});
app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
        u.EndpointRouteBuilder.MapControllers();
    });

await app.RunAsync();