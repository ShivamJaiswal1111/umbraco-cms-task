using umbraco_cms_task.Services;
using Umbraco.Cms.Persistence.SqlServer;

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
    .AddUmbracoSqlServerSupport()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseCors("AllowAngular");
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
    var host = context.Request.Host.Host;
    var port = context.Request.Host.Port;

    if (port == 44392)
    {
        await next();
        return;
    }

    string? sitePrefix = host switch
    {
        "sitea.local" => "site-a",
        "siteb.local" => "site-b",
        _ => null
    };

    if (sitePrefix != null)
    {
        var legacyUrl = string.IsNullOrEmpty(path)
            ? $"/{sitePrefix}/home"
            : $"/{sitePrefix}{path}";

        var umbracoContextFactory = context.RequestServices
            .GetRequiredService<Umbraco.Cms.Core.Web.IUmbracoContextFactory>();

        using var contextRef = umbracoContextFactory.EnsureUmbracoContext();
        var contentCache = contextRef.UmbracoContext.Content;

        var match = contentCache?.GetAtRoot()
            .SelectMany(x => x.DescendantsOrSelf())
            .FirstOrDefault(x => x.Value<string>("legacySourceUrl") == legacyUrl);

        if (match != null)
        {
            context.Response.Redirect(match.Url(mode: Umbraco.Cms.Core.Models.PublishedContent.UrlMode.Absolute), permanent: true);
            return;
        }
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

app.MapGet("/rebuild-nucache", (
    Umbraco.Cms.Core.PublishedCache.IPublishedSnapshotService snapshotService,
    Umbraco.Cms.Core.Services.IContentTypeService contentTypeService,
    Umbraco.Cms.Core.Services.IMediaTypeService mediaTypeService,
    ILogger<Program> logger) =>
{
    try
    {
        var contentTypeIds = contentTypeService.GetAll().Select(ct => ct.Id).ToArray();
        var mediaTypeIds = mediaTypeService.GetAll().Select(mt => mt.Id).ToArray();

        snapshotService.Rebuild(contentTypeIds: contentTypeIds, mediaTypeIds: mediaTypeIds);

        logger.LogInformation(
            "Endpoint-triggered NuCache rebuild completed. ContentTypes: {ContentCount}, MediaTypes: {MediaCount}",
            contentTypeIds.Length, mediaTypeIds.Length);

        return Results.Ok($"Rebuild completed - {contentTypeIds.Length} content types, {mediaTypeIds.Length} media types");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Endpoint-triggered NuCache rebuild failed.");
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/fix-culture-names", (
    Umbraco.Cms.Core.Services.IContentService contentService,
    Umbraco.Cms.Core.Services.ILocalizationService languageService,
    ILogger<Program> logger) =>
{
    var results = new List<string>();
    var errors = new List<string>();

    var nodeIds = new[]
    {
        1057, 1062, 1063, 1064, 1066, 1074, 1079, 1080, 1081, 1082, 1083,
        1087, 1088, 1089, 1102, 1103, 1114, 1117, 1158, 1159, 1160, 1161,
        1162, 1163, 1164, 1165, 1166, 1167, 1168, 1169, 1170, 1171, 1172,
        1173, 1174
    };

    var isoCodes = languageService.GetAllLanguages().Select(l => l.IsoCode).ToList();

    foreach (var id in nodeIds)
    {
        try
        {
            var content = contentService.GetById(id);
            if (content == null)
            {
                errors.Add($"Node {id}: not found");
                continue;
            }

            if (!content.ContentType.VariesByCulture())
            {
                results.Add($"Node {id}: invariant, skipped");
                continue;
            }

            var baseName = content.Name;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                errors.Add($"Node {id}: base Name is empty, cannot fix");
                continue;
            }

            foreach (var iso in isoCodes)
            {
                content.SetCultureName(baseName, iso);
            }

            var publishResult = contentService.SaveAndPublish(content, isoCodes.ToArray());

            results.Add(publishResult.Success
                ? $"Node {id} ('{baseName}'): fixed and published"
                : $"Node {id} ('{baseName}'): save failed");
        }
        catch (Exception ex)
        {
            errors.Add($"Node {id}: EXCEPTION - {ex.Message}");
        }
    }

    logger.LogInformation("Culture name fix completed. {SuccessCount} processed, {ErrorCount} errors.",
        results.Count, errors.Count);

    return Results.Ok(new { results, errors });
});

await app.RunAsync();
