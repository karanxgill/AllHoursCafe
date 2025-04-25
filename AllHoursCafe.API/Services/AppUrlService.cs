using Microsoft.Extensions.Configuration;

namespace AllHoursCafe.API.Services
{
    public class AppUrlService
    {
        private readonly string _baseUrl;

        public AppUrlService(IConfiguration configuration)
        {
            // Get the base URL from configuration, or use a default if not found
            _baseUrl = configuration["AppSettings:BaseUrl"] ?? "http://localhost:5002";
        }

        public string GetBaseUrl()
        {
            return _baseUrl;
        }

        public string GetUrl(string relativePath)
        {
            // Ensure the relative path starts with a slash
            if (!relativePath.StartsWith("/"))
            {
                relativePath = "/" + relativePath;
            }

            return _baseUrl + relativePath;
        }
    }
}
