using System.Threading.Tasks;
using CourseModuleAPI.DTOs;
using CourseModuleAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourseModuleAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            var resp = await _chatService.ProcessQueryAsync(request);
            return Ok(resp);
        }
    }
}
