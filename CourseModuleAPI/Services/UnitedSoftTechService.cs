using CourseModuleAPI.Data;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace CourseModuleAPI.Services
{
    public class UnitedSoftTechService
    {
        private readonly HttpClient _httpClient;

        public UnitedSoftTechService(
            HttpClient httpClient,
            CourseModuleDbContext context)
        {
            _httpClient = httpClient;
        }

        public async Task<int> FetchAndSaveModulesAsync()
        {
            string url =
                "https://www.unitedsofttech.co.in/CourseDetails?category=it&id=web-development-advanced";

            string html = await _httpClient.GetStringAsync(url);

            Console.WriteLine("HTML LENGTH = " + html.Length);

            // Search for the word "Module"
            var matches = Regex.Matches(
                html,
                @"Module",
                RegexOptions.IgnoreCase);

            Console.WriteLine(
                "MODULE WORD COUNT = " + matches.Count);

            // Print a small part around Module
            foreach (Match match in matches.Take(10))
            {
                int start = Math.Max(0, match.Index - 150);
                int length = Math.Min(500, html.Length - start);

                Console.WriteLine(
                    "----- MODULE CONTENT -----");

                Console.WriteLine(
                    html.Substring(start, length));
            }

            return 0;
        }
    }
}