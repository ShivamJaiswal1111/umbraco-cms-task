using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/contact")]
public class ContactApiController : ControllerBase
{
    public class ContactRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Message { get; set; } = "";
    }

    [HttpPost]
    public IActionResult Submit([FromBody] ContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Please fill in all fields." });
        }

        Console.WriteLine($"New contact form submission (via API): {request.Name}, {request.Email}, {request.Message}");

        return Ok(new { message = "Thanks! Your message has been sent." });
    }
}