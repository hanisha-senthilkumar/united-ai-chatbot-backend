using System.Threading.Tasks;
using CourseModuleAPI.DTOs;

namespace CourseModuleAPI.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Process the user query and return a ChatResponse. The service will attempt to answer from database content.
        /// </summary>
        Task<ChatResponse> ProcessQueryAsync(ChatRequest request);
    }
}
