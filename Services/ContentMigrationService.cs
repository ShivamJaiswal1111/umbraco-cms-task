using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace umbraco_cms_task.Services
{
    public class ContentMigrationService
    {
        private readonly IContentService _contentService;
        private readonly IContentTypeService _contentTypeService;

        public ContentMigrationService(
            IContentService contentService,
            IContentTypeService contentTypeService)
        {
            _contentService = contentService;
            _contentTypeService = contentTypeService;
        }

        public void Migrate(int siteAParentId, int siteBParentId, int mergedParentId)
        {
            var mergedType = _contentTypeService.Get("mergedPage");

            if (mergedType == null)
                throw new Exception("mergedPage document type not found. Check the alias in Settings.");

            // Site A — children nest under the home node
            int siteAHomeId = MigrateHomePage(siteAParentId, mergedParentId, mergedType, "site-a");
            if (siteAHomeId > 0)
                MigrateChildren(siteAParentId, siteAHomeId, mergedType, "site-a");

            // Site B — children nest under the home node
            int siteBHomeId = MigrateHomePage(siteBParentId, mergedParentId, mergedType, "site-b");
            if (siteBHomeId > 0)
                MigrateChildren(siteBParentId, siteBHomeId, mergedType, "site-b");
        }

        private int MigrateHomePage(int sourceId, int mergedParentId,
            IContentType mergedType, string sitePrefix)
        {
            var homePage = _contentService.GetById(sourceId);
            if (homePage == null) return -1;

            string migratedName = $"{homePage.Name} ({sitePrefix})";

            // Already migrated — return existing node's ID so children can nest under it
            if (AlreadyMigrated(mergedParentId, migratedName))
            {
                return _contentService.GetPagedChildren(mergedParentId, 0, 200, out _)
                    .First(c => c.Name == migratedName).Id;
            }

            var newPage = _contentService.Create(migratedName, mergedParentId, mergedType);

            newPage.SetValue("title",           homePage.GetValue<string>("title") ?? "");
            newPage.SetValue("body",            ExtractBodyMarkup(homePage.GetValue<string>("body")));
            newPage.SetValue("siteName",        homePage.GetValue<string>("siteName") ?? "");
            newPage.SetValue("intro",           "");
            newPage.SetValue("sEOTitle",        homePage.GetValue<string>("sEOTitle") ?? "");
            newPage.SetValue("metaDescription", homePage.GetValue<string>("metaDescription") ?? "");
            newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/home");

            _contentService.SaveAndPublish(newPage);

            return newPage.Id;
        }

        private void MigrateChildren(int sourceParentId, int mergedParentId,
            IContentType mergedType, string sitePrefix)
        {
            var children = _contentService.GetPagedChildren(sourceParentId, 0, 100, out _);

            foreach (var child in children)
            {
                string migratedName = $"{child.Name} ({sitePrefix})";
                if (AlreadyMigrated(mergedParentId, migratedName)) continue;

                var newPage = _contentService.Create(migratedName, mergedParentId, mergedType);
                var docTypeAlias = child.ContentType.Alias;

                switch (docTypeAlias)
                {
                    case "contactPage":
                        newPage.SetValue("title",           child.Name);
                        newPage.SetValue("intro",           child.GetValue<string>("intro") ?? "");
                        newPage.SetValue("body",            "");
                        newPage.SetValue("siteName",        "");
                        newPage.SetValue("sEOTitle",        child.GetValue<string>("sEOTitle") ?? "");
                        newPage.SetValue("metaDescription", child.GetValue<string>("metaDescription") ?? "");
                        newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/contact");
                        break;

                    case "blogListing":
                        newPage.SetValue("title",           child.Name);
                        newPage.SetValue("body",            ExtractBodyMarkup(child.GetValue<string>("body")));
                        newPage.SetValue("intro",           child.GetValue<string>("intro") ?? "");
                        newPage.SetValue("siteName",        "");
                        newPage.SetValue("sEOTitle",        child.GetValue<string>("sEOTitle") ?? "");
                        newPage.SetValue("metaDescription", child.GetValue<string>("metaDescription") ?? "");
                        newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/blog-posts");
                        _contentService.SaveAndPublish(newPage);
                        // Blog posts nest under this blog listing node
                        MigrateBlogPosts(child.Id, newPage.Id, mergedType, sitePrefix);
                        continue;

                    default:
                        newPage.SetValue("title",           child.Name);
                        newPage.SetValue("body",            ExtractBodyMarkup(child.GetValue<string>("body")));
                        newPage.SetValue("intro",           child.GetValue<string>("intro") ?? "");
                        newPage.SetValue("siteName",        "");
                        newPage.SetValue("sEOTitle",        child.GetValue<string>("sEOTitle") ?? "");
                        newPage.SetValue("metaDescription", child.GetValue<string>("metaDescription") ?? "");
                        newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/{child.Name.ToLower().Replace(" ", "-")}");
                        break;
                }

                _contentService.SaveAndPublish(newPage);
            }
        }

        private void MigrateBlogPosts(int blogListingId, int blogListingMergedId,
            IContentType mergedType, string sitePrefix)
        {
            var blogPosts = _contentService.GetPagedChildren(blogListingId, 0, 100, out _);

            foreach (var post in blogPosts)
            {
                string migratedName = $"{post.Name} ({sitePrefix})";
                if (AlreadyMigrated(blogListingMergedId, migratedName)) continue;

                // Create post under the merged blog listing node
                var newPage = _contentService.Create(migratedName, blogListingMergedId, mergedType);

                newPage.SetValue("title",           post.GetValue<string>("title") ?? post.Name);
                newPage.SetValue("body",            ExtractBodyMarkup(post.GetValue<string>("body")));
                newPage.SetValue("intro",           post.GetValue<string>("intro") ?? "");
                newPage.SetValue("siteName",        "");
                newPage.SetValue("sEOTitle",        post.GetValue<string>("sEOTitle") ?? "");
                newPage.SetValue("metaDescription", post.GetValue<string>("metaDescription") ?? "");
                newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/blog-posts/{post.Name.ToLower().Replace(" ", "-")}");

                _contentService.SaveAndPublish(newPage);
            }
        }

        private bool AlreadyMigrated(int parentId, string name)
        {
            var existing = _contentService.GetPagedChildren(parentId, 0, 200, out _);
            return existing.Any(c => c.Name == name);
        }

        private string ExtractBodyMarkup(string? rawValue)
        {
            if (string.IsNullOrEmpty(rawValue)) return "";
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(rawValue);
                if (json.RootElement.TryGetProperty("markup", out var markup))
                    return markup.GetString() ?? rawValue;
            }
            catch { }
            return rawValue;
        }
    }
}