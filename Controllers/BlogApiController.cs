using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;

[ApiController]
[Route("api/blog")]
public class BlogApiController : ControllerBase
{
    private readonly IContentService _contentService;

    public BlogApiController(IContentService contentService)
    {
        _contentService = contentService;
    }

    public class CreateBlogPostRequest
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public DateTime PublishDate { get; set; }
        public string Body { get; set; } = "";
        public Guid ParentListingId { get; set; }
    }

    [HttpPost]
    public IActionResult CreateBlogPost([FromBody] CreateBlogPostRequest request)
    {
        var parent = _contentService.GetById(request.ParentListingId);
        if (parent == null)
            return NotFound(new { error = "Blog listing not found." });

        var newPost = _contentService.Create(request.Title, parent.Id, "blogPost");
        newPost.SetValue("title", request.Title);
        newPost.SetValue("author", request.Author);
        newPost.SetValue("publishDate", request.PublishDate);
        newPost.SetValue("body", request.Body);

        var result = _contentService.SaveAndPublish(newPost);
        if (!result.Success)
            return StatusCode(500, new { error = "Failed to create and publish." });

        return Ok(new { message = "Blog post created.", id = newPost.Key });
    }
}