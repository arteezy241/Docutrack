using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DocuTrack.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public EmailService(IConfiguration config)
        {
            _config = config;
            _http = new HttpClient();
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var apiKey = _config["Resend:ApiKey"];
            var from = _config["Resend:From"];

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = from,
                to = new[] { toEmail },
                subject = subject,
                html = body
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("https://api.resend.com/emails", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Resend error: {responseBody}");
        }
    }
}