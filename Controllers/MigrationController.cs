using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using umbraco_cms_task.Services;

namespace umbraco_cms_task.Controllers
{
    [Route("api/migrate")]
    public class MigrationController : Controller
    {
        private readonly ContentMigrationService _migrationService;
        private readonly IContentService _contentService;

        public MigrationController(
            ContentMigrationService migrationService,
            IContentService contentService)
        {
            _migrationService = migrationService;
            _contentService = contentService;
        }

        [HttpGet("run")]
        public IActionResult Run()
        {
            // Get root level nodes
            var rootNodes = _contentService.GetRootContent();

            var siteA  = rootNodes.FirstOrDefault(c => c.Name == "Home");
            var siteB  = rootNodes.FirstOrDefault(c => c.Name == "Uniphar Retail Home");

            // Merged Site ID — taken directly from your info panel
            var merged = _contentService.GetById(1117);

            if (siteA == null)
                return BadRequest("Could not find 'Home' node. Check the name in Content.");

            if (siteB == null)
                return BadRequest("Could not find 'Uniphar Retail Home' node. Check the name in Content.");

            if (merged == null)
                return BadRequest("Could not find Merged Site node with ID 1117.");

            _migrationService.Migrate(siteA.Id, siteB.Id, merged.Id);

            return Ok("Migration completed successfully. Check your Merged Site node in Content.");
        }
        [HttpGet("republish")]
        public IActionResult Republish()
        {
            var mergedRoot = _contentService.GetRootContent()
                .FirstOrDefault(c => c.Name == "Merged Page");

            if (mergedRoot == null)
                return BadRequest("Merged Page root not found");

            var children = _contentService.GetPagedDescendants(mergedRoot.Id, 0, 500, out long total);
            int count = 0;

            foreach (var node in children)
            {
                _contentService.SaveAndPublish(node);
                count++;
            }

            return Ok($"Republished {count} nodes");
        }

        [HttpGet("debug")]
        public IActionResult Debug()
        {
            var rootNodes = _contentService.GetRootContent();
            var siteA = rootNodes.FirstOrDefault(c => c.Name == "Home");

            if (siteA == null)
                return BadRequest("Home node not found");

            var result = new
            {
                Name = siteA.Name,
                ContentTypeAlias = siteA.ContentType.Alias,
                Properties = siteA.Properties.Select(p => new
                {
                    Alias = p.Alias,
                    Value = p.GetValue()?.ToString() ?? "(empty)"
                })
            };

            return Ok(result);
        }
    }
}