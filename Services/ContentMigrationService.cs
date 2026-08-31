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

            int siteAHomeId = MigrateHomePage(siteAParentId, mergedParentId, mergedType, "site-a");
            if (siteAHomeId > 0)
                MigrateChildren(siteAParentId, siteAHomeId, mergedType, "site-a");

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

            if (AlreadyMigrated(mergedParentId, migratedName))
            {
                return _contentService.GetPagedChildren(mergedParentId, 0, 200, out _)
                    .First(c => c.Name == migratedName).Id;
            }

            var newPage = _contentService.Create(migratedName, mergedParentId, mergedType);

            // set the culture name explicitly
            newPage.SetCultureName(migratedName, "en-US");

            // vary by culture
            newPage.SetValue("title",           homePage.GetValue<string>("title", culture: "en-US") ?? "", culture: "en-US");
            newPage.SetValue("body",            ExtractBodyMarkup(homePage.GetValue<string>("body", culture: "en-US")), culture: "en-US");
            newPage.SetValue("intro",           "", culture: "en-US");

            // invariant
            newPage.SetValue("siteName",        homePage.GetValue<string>("siteName") ?? "");
            newPage.SetValue("sEOTitle",        homePage.GetValue<string>("sEOTitle") ?? "");
            newPage.SetValue("metaDescription", homePage.GetValue<string>("metaDescription") ?? "");
            newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/home");

            _contentService.SaveAndPublish(newPage, culture: "en-US");
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
                newPage.SetCultureName(migratedName, "en-US");
                var docTypeAlias = child.ContentType.Alias;

                switch (docTypeAlias)
                {
                    case "contactPage":
                        newPage.SetValue("title",           child.Name,                                                        culture: "en-US");
                        newPage.SetValue("intro",           child.GetValue<string>("intro", culture: "en-US") ?? "",           culture: "en-US");
                        newPage.SetValue("body",            "",                                                                culture: "en-US");
                        newPage.SetValue("siteName",        "");
                        newPage.SetValue("sEOTitle",        child.GetValue<string>("sEOTitle") ?? "");
                        newPage.SetValue("metaDescription", child.GetValue<string>("metaDescription") ?? "");
                        newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/contact");
                        break;

                    case "blogListing":
                        newPage.SetValue("title",           child.Name,                                                        culture: "en-US");
                        newPage.SetValue("body",            ExtractBodyMarkup(child.GetValue<string>("body", culture: "en-US")), culture: "en-US");
                        newPage.SetValue("intro",           child.GetValue<string>("intro", culture: "en-US") ?? "",           culture: "en-US");
                        newPage.SetValue("siteName",        "");
                        newPage.SetValue("sEOTitle",        child.GetValue<string>("sEOTitle") ?? "");
                        newPage.SetValue("metaDescription", child.GetValue<string>("metaDescription") ?? "");
                        newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/blog-posts");
                        _contentService.SaveAndPublish(newPage, culture: "en-US");
                        MigrateBlogPosts(child.Id, newPage.Id, mergedType, sitePrefix);
                        continue;

                    default:
                        newPage.SetValue("title",           child.Name,                                                        culture: "en-US");
                        newPage.SetValue("body",            ExtractBodyMarkup(child.GetValue<string>("body", culture: "en-US")), culture: "en-US");
                        newPage.SetValue("intro",           child.GetValue<string>("intro", culture: "en-US") ?? "",           culture: "en-US");
                        newPage.SetValue("siteName",        "");
                        newPage.SetValue("sEOTitle",        child.GetValue<string>("sEOTitle") ?? "");
                        newPage.SetValue("metaDescription", child.GetValue<string>("metaDescription") ?? "");
                        newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/{child.Name.ToLower().Replace(" ", "-")}");
                        break;
                }

                _contentService.SaveAndPublish(newPage, culture: "en-US");
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

                var newPage = _contentService.Create(migratedName, blogListingMergedId, mergedType);
                newPage.SetCultureName(migratedName, "en-US");

                newPage.SetValue("title",           post.GetValue<string>("title", culture: "en-US") ?? post.Name, culture: "en-US");
                newPage.SetValue("body",            ExtractBodyMarkup(post.GetValue<string>("body", culture: "en-US")),  culture: "en-US");
                newPage.SetValue("intro",           post.GetValue<string>("intro", culture: "en-US") ?? "",              culture: "en-US");
                newPage.SetValue("siteName",        "");
                newPage.SetValue("sEOTitle",        post.GetValue<string>("sEOTitle") ?? "");
                newPage.SetValue("metaDescription", post.GetValue<string>("metaDescription") ?? "");
                newPage.SetValue("legacySourceUrl", $"/{sitePrefix}/blog-posts/{post.Name.ToLower().Replace(" ", "-")}");

                _contentService.SaveAndPublish(newPage, culture: "en-US");
            }
        }

        public void SeedBrandedContent()
        {
            var rootNodes = _contentService.GetRootContent();

            var siteA = rootNodes.FirstOrDefault(c => c.Name == "Home");
            var siteB = rootNodes.FirstOrDefault(c => c.Name == "Uniphar Retail Home");

            if (siteA != null) SeedSiteA(siteA);
            if (siteB != null) SeedSiteB(siteB);
        }

        private void SeedSiteA(IContent root)
        {
            root.SetValue("title",           "Your Health, Our Priority", culture: "en-US");
            root.SetValue("siteName",        "Uniphar Pharmacy");
            root.SetValue("sEOTitle",        "Uniphar Pharmacy — Your Health, Our Priority");
            root.SetValue("metaDescription", "Uniphar Pharmacy provides trusted pharmaceutical care across Ireland and the UK.");
            _contentService.SaveAndPublish(root, culture: "en-US");

            var children = _contentService.GetPagedChildren(root.Id, 0, 100, out _);
            var contact = children.FirstOrDefault(c => c.ContentType.Alias == "contactPage");
            if (contact != null)
            {
                contact.SetValue("intro",           "Have a question about your prescription or pharmacy services? We'd love to hear from you.", culture: "en-US");
                contact.SetValue("sEOTitle",        "Contact — Uniphar Pharmacy");
                contact.SetValue("metaDescription", "Get in touch with Uniphar Pharmacy for all your healthcare needs.");
                _contentService.SaveAndPublish(contact, culture: "en-US");
            }
        }

        private void SeedSiteB(IContent root)
        {
            root.SetValue("title",           "Trusted Retail Healthcare", culture: "en-US");
            root.SetValue("siteName",        "Uniphar Retail");
            root.SetValue("sEOTitle",        "Uniphar Retail — Trusted Retail Healthcare");
            root.SetValue("metaDescription", "Uniphar Retail connects communities with trusted health products across Ireland and the UK.");
            _contentService.SaveAndPublish(root, culture: "en-US");

            var children = _contentService.GetPagedChildren(root.Id, 0, 100, out _);
            var contact = children.FirstOrDefault(c => c.ContentType.Alias == "contactPage");
            if (contact != null)
            {
                contact.SetValue("intro",           "Want to know more about our retail health products? Get in touch with our team.", culture: "en-US");
                contact.SetValue("sEOTitle",        "Contact — Uniphar Retail");
                contact.SetValue("metaDescription", "Contact Uniphar Retail for pharmacy and healthcare retail enquiries.");
                _contentService.SaveAndPublish(contact, culture: "en-US");
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