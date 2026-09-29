using System.Collections.Generic;

namespace CourseModuleAPI.DTOs
{
    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Query { get; set; }
        public string Answer { get; set; }
        public string Course { get; set; }
        public List<object> Sources { get; set; } = new List<object>();
    }
}
